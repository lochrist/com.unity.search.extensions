using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEngine.Search;
using UnityEditor.Search.Providers;

namespace UnityEditor.Search.Collections
{
    enum ResizingWindow
    {
        None,
        Left,
        Right,
        Bottom,
        Gripper
    }

    [Icon("Icons/QuickSearch/ListView.png")]
    //[Overlay(typeof(SceneView), "Collections", defaultLayout: false)]
    class SearchCollectionOverlay : Overlay, ISearchCollectionView, IHasCustomMenu
    {
        static class InnerStyles
        {
            public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
            public static GUIContent createContent = EditorGUIUtility.IconContent("CreateAddNew");
            public static GUIStyle toolbarCreateAddNewDropDown = new GUIStyle("ToolbarCreateAddNewDropDown")
            {
                fixedWidth = 32f,
                fixedHeight = 0,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(4, 4, 4, 4)
            };
        }

        const string SearchCollectionUserSettingsFilePath = "UserSettings/Search.collections";
        static int RESIZER_CONTROL_ID = "SearchCollectionOverlayResizer".GetHashCode();

        string m_SearchText;
        TreeViewState m_TreeViewState;
        List<SearchCollection> m_Collections;
        SearchCollectionTreeView m_TreeView;
        IMGUIContainer m_CollectionContainer;
        ResizingWindow m_Resizing = ResizingWindow.None;

        public string searchText
        {
            get => m_SearchText;
            set
            {
                if (!string.Equals(value, m_SearchText, StringComparison.Ordinal))
                {
                    m_SearchText = value;
                    UpdateView();
                }
            }
        }

        public ICollection<SearchCollection> collections => m_Collections;

        public SearchCollectionOverlay()
        {
            layout = Layout.Panel;

            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            if (m_Collections == null)
                m_Collections = LoadCollections();

            m_TreeView = new SearchCollectionTreeView(m_TreeViewState, this);
        }


        public override VisualElement CreatePanelContent()
        {
            rootVisualElement.pickingMode = PickingMode.Position;
            m_CollectionContainer = new IMGUIContainer(OnGUI);
            m_CollectionContainer.style.width = EditorPrefs.GetFloat("SCO_Width", 250f);
            m_CollectionContainer.style.height = EditorPrefs.GetFloat("SCO_Height", 350f);
            return m_CollectionContainer;
        }

        private void OnGUI()
        {
            if (!displayed)
                return;

            var evt = Event.current;
            HandleShortcuts(evt);
            HandleOverlayResize(evt);

            DrawToolbar(evt);
            DrawTreeView();
        }

        private void DrawToolbar(Event evt)
        {
            var toolbarRect = EditorGUILayout.GetControlRect(false, 21f, GUIStyle.none, GUILayout.ExpandWidth(true));
            var buttonStackRect = HandleButtons(evt, toolbarRect);

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.None && evt.character == '\r')
                return;

            toolbarRect.xMin = buttonStackRect.xMax + 2f;
            var searchTextRect = toolbarRect;
            searchTextRect = EditorStyles.toolbarSearchField.margin.Remove(searchTextRect);
            searchTextRect.xMax += 1f;
            searchTextRect.y += Mathf.Round((toolbarRect.height - searchTextRect.height) / 2f - 2f);

            var hashForSearchField = "CollectionsSearchField".GetHashCode();
            var searchFieldControlID = GUIUtility.GetControlID(hashForSearchField, FocusType.Passive, searchTextRect);
            m_TreeView.searchString = EditorGUI.ToolbarSearchField(
                searchFieldControlID,
                searchTextRect,
                m_TreeView.searchString,
                EditorStyles.toolbarSearchField,
                string.IsNullOrEmpty(m_TreeView.searchString) ? GUIStyle.none : EditorStyles.toolbarSearchFieldCancelButton);

            DrawButtons(buttonStackRect, evt);
        }

        private Rect HandleButtons(Event evt, Rect toolbarRect)
        {
            Rect rect = toolbarRect;
            rect = InnerStyles.toolbarCreateAddNewDropDown.margin.Remove(rect);
            rect.xMax = rect.xMin + InnerStyles.toolbarCreateAddNewDropDown.fixedWidth;
            rect.y += (toolbarRect.height - rect.height) / 2f - 5f;
            rect.height += 2f;

            bool mouseOver = rect.Contains(evt.mousePosition);
            if (evt.type == EventType.MouseDown && mouseOver)
            {
                GUIUtility.hotControl = 0;

                var menu = new GenericMenu();
                AddCollectionMenus(menu);
                menu.ShowAsContext();
                evt.Use();
            }

            return rect;
        }

        void DrawButtons(in Rect buttonStackRect, in Event evt)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            bool mouseOver = buttonStackRect.Contains(evt.mousePosition);
            InnerStyles.toolbarCreateAddNewDropDown.Draw(buttonStackRect, InnerStyles.createContent, mouseOver, false, false, false);
        }

        private void HandleOverlayResize(Event evt)
        {
            if (evt.type == EventType.MouseUp && m_Resizing != ResizingWindow.None)
            {
                GUIUtility.hotControl = 0;
                m_Resizing = ResizingWindow.None;
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == RESIZER_CONTROL_ID)
            {
                switch (m_Resizing)
                {
                    case ResizingWindow.Left:
                        var mousePositionUnclipped = evt.mousePosition;//GUIClip.UnclipToWindow(evt.mousePosition);
                        var diff = rootVisualElement.style.left.value.value - mousePositionUnclipped.x;
                        rootVisualElement.style.left = mousePositionUnclipped.x;
                        m_CollectionContainer.style.width = m_CollectionContainer.style.width.value.value + diff;
                        break;
                    case ResizingWindow.Right:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        break;
                    case ResizingWindow.Bottom:
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    case ResizingWindow.Gripper:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    default:
                        return;
                }

                EditorPrefs.SetFloat("SCO_Width", m_CollectionContainer.style.width.value.value);
                EditorPrefs.SetFloat("SCO_Height", m_CollectionContainer.style.height.value.value);
                evt.Use();
            }
            else
            {
                const float resizeGripperSize = 3;
                var width = m_CollectionContainer.style.width.value.value;
                var height = m_CollectionContainer.style.height.value.value;

                var leftResizeRect = new Rect(0, 0, resizeGripperSize, height);
                var rightResizeRect = new Rect(width - resizeGripperSize, 0, resizeGripperSize, height - resizeGripperSize * 2);
                var bottomResizeRect = new Rect(0, height - resizeGripperSize, width - resizeGripperSize * 2, resizeGripperSize);
                var bottomRightResizeRect = new Rect(width - resizeGripperSize * 2, height - resizeGripperSize * 2, resizeGripperSize * 2, resizeGripperSize * 2);

                EditorGUIUtility.AddCursorRect(leftResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(rightResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(bottomResizeRect, MouseCursor.ResizeVertical);
                EditorGUIUtility.AddCursorRect(bottomRightResizeRect, MouseCursor.ResizeUpLeft);
                
                if (evt.type == EventType.MouseDown)
                {
                    if (bottomRightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Gripper;
                    else if (leftResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Left;
                    else if (rightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Right;
                    else if (bottomResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Bottom;
                    else
                        return;

                    if (m_Resizing != ResizingWindow.None && evt.isMouse)
                    {
                        GUIUtility.hotControl = RESIZER_CONTROL_ID;
                        evt.Use();
                    }
                }
            }
        }

        private List<SearchCollection> LoadCollections()
        {
            var collections = new SearchCollections();
            if (!System.IO.File.Exists(SearchCollectionUserSettingsFilePath))
                return collections.collections;

            var collectionsData = System.IO.File.ReadAllText(SearchCollectionUserSettingsFilePath);
            if (string.IsNullOrEmpty(collectionsData))
                return collections.collections;
            EditorJsonUtility.FromJsonOverwrite(collectionsData, collections);
            return collections.collections;
        }

        public void SaveCollections()
        {
            System.IO.File.WriteAllText(SearchCollectionUserSettingsFilePath, EditorJsonUtility.ToJson(new SearchCollections(m_Collections), prettyPrint: true));
        }

        void HandleShortcuts(Event evt)
        {
            if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.F5)
            {
                m_TreeView.Reload();
                evt.Use();
            }
        }

        void DrawTreeView()
        {
            var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(treeViewRect);
        }

        public void AddCollectionMenus(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("New collection"), false, NewCollection);
            menu.AddItem(new GUIContent("Load collection..."), false, LoadCollection);
        }

        private void NewCollection()
        {
            m_TreeView.Add(new SearchCollection("Collection"));
        }

        IEnumerable<SearchItem> EnumerateCollections(SearchContext context, SearchProvider provider)
        {
            foreach (var q in SearchQueryAsset.savedQueries)
            {
                if (!string.IsNullOrWhiteSpace(context.searchQuery) && !SearchUtils.MatchSearchGroups(context, $"{q.displayName} {q.description}", true))
                    continue;

                var providerIds = q.GetProviderIds().ToArray();
                if (providerIds.Length > 0 && !providerIds.Contains("scene") && !providerIds.Contains("asset"))
                    continue;

                yield return provider.CreateItem(context, q.guid, q.displayName[0], q.displayName, null, q.thumbnail, q);
            }
        }

        private void LoadCollection()
        {
            var collectionProvider = new SearchProvider("collections", "Collections", EnumerateCollections)
            {
                fetchDescription = FetchQueryDescription
            };
            var searchContext = SearchService.CreateContext(collectionProvider);
            var flags = SearchViewFlags.CompactView | SearchViewFlags.DisableInspectorPreview;
            SearchService.ShowPicker(new SearchViewState(searchContext, flags)
            {
                title = "collections",
                selectHandler = SelectCollection
            });
        }

        private string FetchQueryDescription(SearchItem item, SearchContext context)
        {
            var q = (SearchQueryAsset)item.data;
            if (item.options.HasAny(SearchItemOptions.Compacted))
                return q.displayName;
            return item.description;
        }

        private void SelectCollection(SearchItem selectedItem, bool canceled)
        {
            if (canceled)
                return;

            var searchQuery = (SearchQueryAsset)selectedItem.data;
            if (!searchQuery)
                return;

            m_TreeView.Add(new SearchCollection(searchQuery));
            SaveCollections();
        }

        void UpdateView()
        {
            m_TreeView.searchString = m_SearchText;
        }

        public void OpenContextualMenu()
        {
            var menu = new GenericMenu();

            AddCollectionMenus(menu);

            menu.ShowAsContext();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddSeparator("");
            AddCollectionMenus(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Refresh"), false, () => m_TreeView.Reload());
            menu.AddItem(new GUIContent("Save"), false, () => SaveCollections());
            menu.AddSeparator("");
        }

        [SearchActionsProvider]
        internal static IEnumerable<SearchAction> ActionHandlers()
        {
            yield return new SearchAction("scene", "isolate", EditorGUIUtility.LoadIcon("Isolate"), "Isolate selected object(s)", IsolateObjects);
            yield return new SearchAction("scene", "lock", EditorGUIUtility.LoadIcon("Locked"), "Lock selected object(s)", LockObjects);
        }

        private static void LockObjects(SearchItem[] items)
        {
            var svm = SceneVisibilityManager.instance;
            var objects = items.Select(e => e.ToObject<GameObject>()).Where(g => g).ToArray();
            if (objects.Length == 0)
                return;
            if (svm.IsPickingDisabled(objects[0]))
                svm.EnablePicking(objects, includeDescendants: true);
            else
                svm.DisablePicking(objects, includeDescendants: true);
        }

        private static void IsolateObjects(SearchItem[] items)
        {
            var svm = SceneVisibilityManager.instance;
            if (svm.IsCurrentStageIsolated())
                svm.ExitIsolation();
            else
                svm.Isolate(items.Select(e => e.ToObject<GameObject>()).Where(g=>g).ToArray(), includeDescendants: true);
        }
    }
}
