using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{ 
    class SearchCollectionTreeViewItem : SearchTreeViewItem
    {
        public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");

        readonly SearchCollection m_Collection;
        SearchAction[] m_Actions;
        public SearchCollection collection => m_Collection;
        Action m_AutomaticUpdate;
        bool m_NeedsRefresh;
        Matrix4x4 m_LastCameraPos;

        public SearchCollectionTreeViewItem(SearchCollectionTreeView treeView, SearchCollection collection)
            : base(treeView)
        {
            m_Collection = collection ?? throw new ArgumentNullException(nameof(collection));

            displayName = m_Collection.name;
            children = new List<TreeViewItem>();
            icon = m_Collection.icon ?? (collectionIcon.image as Texture2D);                

            FetchItems();
        }

        public override string GetLabel()
        {
            return $"{(m_AutomaticUpdate != null ? "!" : "")}{m_Collection.name} ({Utils.FormatCount((ulong)children.Count)})";
        }

        private void AddObjects(IEnumerable<UnityEngine.Object> objs)
        {
            foreach (var obj in objs)
            {
                if (!obj)
                    continue;
                AddChild(new SearchObjectTreeViewItem(m_TreeView, obj));
            }
        }

        public void FetchItems()
        {
            AddObjects(m_Collection.objects);

            if (m_Collection.query == null)
                return;

            var context = SearchService.CreateContext(m_Collection.query.GetProviderIds(), m_Collection.query.searchText);
            foreach (var item in m_Collection.items)
                AddChild(new SearchTreeViewItem(m_TreeView, context, item));
            SearchService.Request(context, (_, items) =>
            {
                foreach (var item in items)
                {
                    if (m_Collection.items.Add(item))
                        AddChild(new SearchTreeViewItem(m_TreeView, context, item));
                }
            },
            _ =>
            {
                m_TreeView.UpdateCollections();
                context?.Dispose();
            });
        }

        public override void Select()
        {
            // Do nothing
        }

        public override void Open()
        {
			m_TreeView.SetExpanded(id, !m_TreeView.IsExpanded(id));
        }

        public override bool CanStartDrag()
        {
            return false;
        }

        public override void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Refresh"), false, () => Refresh());
            menu.AddItem(new GUIContent("Automatic Update"), m_AutomaticUpdate != null, () => ToggleAutomaticUpdate());

            var selection = Selection.objects;
            if (selection.Length > 0)
            {
                menu.AddSeparator("");
                if (selection.Length == 1)
                    menu.AddItem(new GUIContent($"Add {selection[0].name}"), false, AddSelection);
                else
                    menu.AddItem(new GUIContent($"Add selection ({selection.Length} objects)"), false, AddSelection);
            }

            var shv = SceneHierarchyWindow.lastInteractedHierarchyWindow;
            if (shv && shv.hasSearchFilter)
                menu.AddItem(new GUIContent($"Add filtered objects"), false, () => AddFilteredItems(shv));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set Color"), false, SelectColor);
            menu.AddItem(new GUIContent("Set Icon"), false, SetIcon);
            if (m_Collection.query is SearchQueryAsset sqa)
                menu.AddItem(new GUIContent("Edit"), false, () => SearchQueryAsset.Open(sqa.GetInstanceID()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rename"), false, () => m_TreeView.BeginRename(this));
            menu.AddItem(new GUIContent("Remove"), false, () => m_TreeView.Remove(this, m_Collection));

            menu.ShowAsContext();
        }

        private void AddFilteredItems(SceneHierarchyWindow shv)
        {
            var oset = new HashSet<UnityEngine.Object>();
            var sh = shv.sceneHierarchy;
            foreach (var tvi in sh.treeView.data.GetRows())
            {
                if (tvi is GameObjectTreeViewItem gtvi && gtvi.objectPPTR)
                    oset.Add(gtvi.objectPPTR);
            }
            AddObjectsToTree(oset.ToArray());
            shv.ClearSearchFilter();
        }

        private void AddSelection()
        {
            AddObjectsToTree(Selection.objects);
        }

        internal void AddObjectsToTree(UnityEngine.Object[] objects)
        {
            m_Collection.AddObjects(objects);
            AddObjects(objects);
            m_TreeView.UpdateCollections();
            m_TreeView.SaveCollections();
        }

        private void SetIcon()
        {
            SearchQuery.ShowQueryIconPicker((newIcon, canceled) =>
            {
                if (canceled)
                    return;
                icon = m_Collection.icon = newIcon;
                m_TreeView.SaveCollections();
                m_TreeView.Repaint();
            });
        }

        internal void DrawActions(in Rect rowRect, in GUIStyle style)
        {
            var buttonRect = rowRect;
            buttonRect.y += 2f;
            buttonRect.xMin = buttonRect.xMax - 22f;

            var buttonCount = 0;
            var items = GetSceneItems();
            foreach (var a in GetActions())
            {
                if (a.execute == null || !a.content.image)
                    continue;

                if (items.Count == 0 || (a.enabled != null && !a.enabled(items)))
                    continue;
                if (GUI.Button(buttonRect, a.content, style))
                    ExecuteAction(a, items);
                buttonRect.x -= 20f;
                buttonCount++;
            }
        }

        IEnumerable<SearchAction> GetActions()
        {
            if (m_Actions == null)
            {
                var sceneProvider = SearchService.GetProvider("scene");
                m_Actions = sceneProvider.actions.Reverse<SearchAction>().ToArray();
            }
            return m_Actions;
        }

        private IReadOnlyCollection<SearchItem> GetSceneItems()
        {
            var items = new HashSet<SearchItem>();
            var sceneProvider = SearchService.GetProvider("scene");
            foreach (var c in children)
            {
                if (c is SearchObjectTreeViewItem otvi && otvi.GetObject() is GameObject go)
                    items.Add(Providers.SceneProvider.AddResult(null, sceneProvider, go));
                else if (c is SearchTreeViewItem tvi && string.Equals(tvi.item.provider.type, "scene", StringComparison.Ordinal))
                    items.Add(tvi.item);
            }
            return items;
        }

        private void ExecuteAction(in SearchAction a, IReadOnlyCollection<SearchItem> items)
        {
            a.execute(items.ToArray());
        }

        private void SelectColor()
        {
            var c = collection.color;
            ColorPicker.Show(SetColor, new Color(c.r, c.g, c.b, 1.0f), false, false);
        }

        private void SetColor(Color color)
        {
            m_Collection.color = color;
            EditorApplication.delayCall -= m_TreeView.SaveCollections;
            EditorApplication.delayCall += m_TreeView.SaveCollections;
        }

        public void Refresh()
        {
            children.Clear();
            m_Collection.items.Clear();
            FetchItems();
        }

        private void ToggleAutomaticUpdate()
        {
            if (m_AutomaticUpdate != null)
                ClearAutoRefresh();
            else
                SetAutoRefresh();
        }

        public void AutoRefresh()
        {
            var camPos = SceneView.lastActiveSceneView?.camera.transform.localToWorldMatrix ?? Matrix4x4.identity;
            if (camPos != m_LastCameraPos)
            {
                NeedsRefresh();
                m_LastCameraPos = camPos;
            }

            if (m_NeedsRefresh)
                Refresh();
            if (m_AutomaticUpdate != null)
                SetAutoRefresh();
        }

        private void ClearAutoRefresh()
        {
            if (m_AutomaticUpdate!=null)
            {
                m_AutomaticUpdate();
                m_AutomaticUpdate = null;
            }
            m_NeedsRefresh = false;
            ObjectChangeEvents.changesPublished -= OnObjectChanged;
        }

        public void SetAutoRefresh()
        {
            ClearAutoRefresh();
            ObjectChangeEvents.changesPublished += OnObjectChanged;
            m_AutomaticUpdate = Utils.CallDelayed(AutoRefresh, 0.9d);
        }

        private void NeedsRefresh() => m_NeedsRefresh = true;
        private void OnObjectChanged(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; ++i)
            {
                var eventType = stream.GetEventType(i);
                switch (eventType)
                {
                    case ObjectChangeKind.ChangeScene:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        NeedsRefresh();
                        break;
                }
            }
        }
    }
}
