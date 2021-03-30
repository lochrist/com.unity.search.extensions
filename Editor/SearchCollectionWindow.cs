using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using SearchField = UnityEditor.Search.SearchField;
using System.Linq;

public class SearchCollectionWindow : EditorWindow, ISearchView
{
    SearchCollectionTreeView m_TreeView;

    [SerializeField] string m_SearchText;
    [SerializeField] bool m_FocusSearchField = true;
    [SerializeField] TreeViewState m_TreeViewState;

    public ISearchList results => throw new NotSupportedException();
    public SearchContext context => throw new NotSupportedException();

    public DisplayMode displayMode => DisplayMode.List;
    public float itemIconSize { get => 0f; set => throw new NotSupportedException(); }
    public bool multiselect { get => true; set => throw new NotSupportedException(); }

    public Action<SearchItem, bool> selectCallback => throw new NotSupportedException();
    public Func<SearchItem, bool> filterCallback => throw new NotSupportedException();
    public Action<SearchItem> trackingCallback => throw new NotSupportedException();

    public SearchSelection selection
    {
        get
        {
            return new SearchSelection(m_TreeView.GetSelection()
                .Select(idx => m_TreeView.GetRows()[idx] as SearchCollectionTreeView.SearchTreeViewItem)
                .Where(e => e != null)
                .Select(e => e.item));
        }
    }

    void OnEnable()
    {
        if (m_TreeViewState == null)
            m_TreeViewState = new TreeViewState();

        m_TreeView = new SearchCollectionTreeView(m_TreeViewState);
    }

    void OnGUI()
    {
        var evt = Event.current;
        HandleShortcuts(evt);
        using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
        {
            using (new GUILayout.HorizontalScope(Styles.toolbar))
            {
                if (DrawSearchField())
                    UpdateView();
            }

            DrawTreeView();
        }
    }

    void HandleShortcuts(Event evt)
    {
        if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.F5)
        {
            m_TreeView.Reload();
            evt.Use();
        }
        else if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.Escape)
        {
            evt.Use();
            Close();
        }
        else
        {
            FocusSearchField();
        }

        if (evt.type == EventType.Used)
            Repaint();
    }

    void DrawTreeView()
    {
        var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
        m_TreeView.OnGUI(treeViewRect);
    }

    bool DrawSearchField()
    {
        var searchTextRect = SearchField.GetRect(m_SearchText, position.width, 10f);
        var searchClearButtonRect = Styles.searchFieldBtn.margin.Remove(searchTextRect);
        searchClearButtonRect.xMin = searchClearButtonRect.xMax - SearchField.s_CancelButtonWidth;

        EditorGUIUtility.AddCursorRect(searchClearButtonRect, MouseCursor.Arrow);
        if (Event.current.type == EventType.MouseUp && searchClearButtonRect.Contains(Event.current.mousePosition))
        {
            ClearSearch();
            return true;
        }

        var previousSearchText = m_SearchText;
        if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.None || Event.current.character != '\r')
        {
            m_SearchText = SearchField.Draw(searchTextRect, m_SearchText, Styles.searchField);
            if (!string.Equals(previousSearchText, m_SearchText, StringComparison.Ordinal))
                return true;
        }

        if (string.IsNullOrEmpty(m_SearchText))
            return false;
        
        return GUI.Button(searchClearButtonRect, Icons.clear, Styles.searchFieldBtn);
    }

    void Update()
    {
        if (focusedWindow == this && SearchField.UpdateBlinkCursorState(EditorApplication.timeSinceStartup))
            Repaint();
    }

    void ClearSearch()
    {
        m_SearchText = "";
        m_FocusSearchField = true;
        UpdateView();
        GUI.changed = true;
        GUI.FocusControl(null);
        GUIUtility.ExitGUI();
    }

    void UpdateView()
    {
        m_TreeView.searchString = m_SearchText;
    }

    void FocusSearchField()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (m_FocusSearchField)
        {
            SearchField.Focus();
            m_FocusSearchField = false;
        }
    }

    [MenuItem("Window/Search/Collections")]
    public static void ShowWindow()
    {
        SearchCollectionWindow wnd = GetWindow<SearchCollectionWindow>();
        wnd.titleContent = new GUIContent("Collections");
    }

    public void SetSelection(params int[] selection)
    {
        throw new NotImplementedException();
    }

    public void AddSelection(params int[] selection)
    {
        throw new NotImplementedException();
    }

    public void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.MoveLineEnd)
    {
        SetSearchText(searchText, moveCursor, -1);
    }

    public void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition)
    {
        m_SearchText = searchText;
        UpdateView();
    }

    public void Refresh(RefreshFlags reason = RefreshFlags.Default)
    {
        m_TreeView.Reload();
        Repaint();
    }

    public void ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch = true)
    {
        throw new NotImplementedException();
    }

    public void ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition)
    {
        throw new NotImplementedException();
    }

    public void SelectSearch()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}

class SearchCollectionTreeView : TreeView
{
    class CollectionTreeViewItem : SearchTreeViewItem
    {        
        readonly SearchQuery m_SearchQueryAsset;
        readonly SearchCollectionTreeView m_TreeView;
        public CollectionTreeViewItem(SearchCollectionTreeView treeView, SearchContext context, SearchItem item)
            : base(context, item)
        {
            m_TreeView = treeView ?? throw new ArgumentNullException(nameof(treeView));

            if (!GlobalObjectId.TryParse(item.id, out var gid))
                throw new ArgumentException($"Invalid search query item {item.id}", nameof(item));

            m_SearchQueryAsset = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as SearchQuery;
            if (m_SearchQueryAsset == null)
                throw new ArgumentException($"Cannot find search query asset {gid}", nameof(item));

            displayName = m_SearchQueryAsset.name;
            icon = item.GetThumbnail(context, cacheThumbnail: false);
            children = new List<TreeViewItem>();

            FetchItems();
        }

        public void FetchItems()
        {
            var context = SearchService.CreateContext(m_SearchQueryAsset.providerIds, m_SearchQueryAsset.text);
            SearchService.Request(context, (context, items) =>
            {
                foreach (var item in items)
                    AddChild(new SearchTreeViewItem(context, item));
            },
            context =>
            {
                UpdateLabel();
                m_TreeView.UpdateCollections();
                context?.Dispose();
            });
        }

        private void UpdateLabel()
        {
            displayName = $"{m_SearchQueryAsset.name} ({children.Count})";
        }

        public override void Select()
        {
            // Do nothing
        }

        public override bool CanStartDrag()
        {
            return false;
        }
    }

    public class SearchTreeViewItem : TreeViewItem
    {
        static int s_NextId = 10000;

        SearchItem m_SearchItem;
        public SearchItem item => m_SearchItem;

        public SearchTreeViewItem(SearchContext context, SearchItem item)
            : base(s_NextId++, 1, item.GetLabel(context))
        {
            m_SearchItem = item;
            icon = item.GetThumbnail(context, cacheThumbnail: false);
        }

        public virtual void Select()
        {
            m_SearchItem.provider?.trackSelection?.Invoke(m_SearchItem, m_SearchItem.context);
        }

        public virtual void Open()
        {
            var defaultAction = m_SearchItem.provider?.actions.FirstOrDefault();
            ExecuteAction(defaultAction, new [] { m_SearchItem });
        }

        public virtual void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            var currentSelection = new[] { m_SearchItem };
            foreach (var action in m_SearchItem.provider.actions.Where(a => a.enabled(currentSelection)))
            {
                var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
                menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection));
            }

            menu.ShowAsContext();
        }

        private void ExecuteAction(SearchAction action, SearchItem[] currentSelection)
        {
            if (action == null)
                return;
            if (action.handler != null)
                action.handler(m_SearchItem);
            else if (action.execute != null)
                action.execute(currentSelection);
        }

        public virtual bool CanStartDrag()
        {
            return m_SearchItem.provider?.startDrag != null;
        }

        public UnityEngine.Object GetObject()
        {
            return m_SearchItem.provider?.toObject(m_SearchItem, typeof(UnityEngine.Object));
        }
    }

    public SearchCollectionTreeView(TreeViewState treeViewState)
        : base(treeViewState)
    {
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = int.MinValue, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
        FetchSearchQueries(root);
        return root;
    }

    protected override IList<TreeViewItem> BuildRows(TreeViewItem rowItem)
    {
        EditorApplication.tick -= DelayedUpdateCollections;
        return base.BuildRows(rowItem);
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);
        if (selectedIds.Count == 0)
            return;

        if (FindItem(selectedIds.Last(), rootItem) is SearchTreeViewItem stvi)
            stvi.Select();
    }

    protected override void DoubleClickedItem(int id)
    {
        if (FindItem(id, rootItem) is SearchTreeViewItem stvi)
            stvi.Open();
    }

    protected override void ContextClickedItem(int id)
    {
        if (FindItem(id, rootItem) is SearchTreeViewItem stvi)
            stvi.OpenContextualMenu();
    }

    protected override bool CanStartDrag(CanStartDragArgs args)
    {
        if (args.draggedItem is SearchTreeViewItem stvi)
            return stvi.CanStartDrag();
        return false;
    }

    protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
    {
        var items = args.draggedItemIDs.Select(id => FindItem(id, rootItem) as SearchTreeViewItem).Where(i => i != null);
        var selectedObjects = items.Select(e => e.GetObject()).Where(o => o).ToArray();
        if (selectedObjects.Length == 0)
            return;
        var paths = selectedObjects.Select(i => AssetDatabase.GetAssetPath(i)).ToArray();
        Utils.StartDrag(selectedObjects, paths, string.Join(", ", items.Select(e => e.displayName)));
    }

    void FetchSearchQueries(TreeViewItem root)
    {
        SearchService.Request("p:t=SearchQuery", (context, items) => OnIncomingQueries(context, items, root), _ => UpdateCollections());
    }

    void OnIncomingQueries(SearchContext context, IEnumerable<SearchItem> items, TreeViewItem root)
    {
        foreach (var item in items)
            root.AddChild(new CollectionTreeViewItem(this, context, item));
    }

    public void UpdateCollections()
    {
        EditorApplication.tick -= DelayedUpdateCollections;
        EditorApplication.tick += DelayedUpdateCollections;
    }

    private void DelayedUpdateCollections()
    {
        EditorApplication.tick -= DelayedUpdateCollections;
        BuildRows(rootItem);
        Repaint();
    }
}
