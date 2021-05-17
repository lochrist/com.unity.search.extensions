#if DEPENDS_ON_INTERNAL_APIS
// TODO:
// 1- Add a new flags to saved search query to mark them as collection.
//   a. Only load search query asset marked as collections.
// 2- Add support to create search query asset with a custom list of search items.

// PICKER ISSUES:
// - Hide toolbar/search field/button
// - Allow to toggle panels
// - Do not always center the picker view
// - Allow to completely override the picker title (do not keep Select ...)
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    class SearchCollectionWindow : EditorWindow, ISearchCollectionView
    {
        #if USE_SEARCH_TABLE
        static class InnerStyles
        {
            public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
        }
        #endif

        SearchCollectionTreeView m_TreeView;

        [SerializeField] string m_SearchText;
        [SerializeField] TreeViewState m_TreeViewState;
        [SerializeField] List<SearchCollection> m_Collections;

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
        public ISet<string> fieldNames => EnumerateFieldNames();

        private ISet<string> EnumerateFieldNames()
        {
            var names = new HashSet<string>();
            foreach (var c in m_Collections)
                foreach (var e in c.items)
                    names.UnionWith(e.GetFieldNames());
            return names;
        }

        void OnEnable()
        {
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            if (m_Collections == null)
                m_Collections = LoadCollections();

            m_TreeView = new SearchCollectionTreeView(m_TreeViewState, this);

            #if USE_SEARCH_TABLE
            titleContent.image = InnerStyles.collectionIcon.image;
            #endif
        }

        void OnDisable()
        {
            SaveWindowCollections();
        }

        private List<SearchCollection> LoadCollections()
        {
            var collectionPaths = EditorPrefs.GetString("SearchCollections", "")
                .Split(new [] { ";;;" }, StringSplitOptions.RemoveEmptyEntries);
            var collection = collectionPaths
                .Select(p => AssetDatabase.LoadAssetAtPath<SearchQueryAsset>(p))
                .Where(p => p)
                .Select(sq => new SearchCollection(sq));
            return new List<SearchCollection>(collection);
        }

        private void SaveWindowCollections()
        {
            var collectionPaths = string.Join(";;;", m_Collections.Select(c => AssetDatabase.GetAssetPath(c.query)));
            EditorPrefs.SetString("SearchCollections", collectionPaths);
        }

        void OnGUI()
        {
            var evt = Event.current;
            HandleShortcuts(evt);
            DrawTreeView();
        }

        void HandleShortcuts(Event evt)
        {
            if (evt.type == EventType.KeyUp && evt.keyCode == KeyCode.F5)
            {
                m_TreeView.Reload();
                evt.Use();
            }
            else if (!docked && evt.type == EventType.KeyUp && evt.keyCode == KeyCode.Escape)
            {
                evt.Use();
                Close();
            }

            if (evt.type == EventType.Used)
                Repaint();
        }

        void DrawTreeView()
        {
            var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
            m_TreeView.OnGUI(treeViewRect);
        }

        public void AddCollectionMenus(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Load collection..."), false, LoadCollection);
        }

        private void LoadCollection()
        {
			#if USE_SEARCH_PICKER
            var context = SearchService.CreateContext(SearchService.GetObjectProviders(), $"t:{nameof(SearchQuery)}");
            SearchService.ShowPicker(context, SelectCollection, 
                trackingHandler: _ => { }, 
                title: "search collection",
                defaultWidth: 300, defaultHeight: 500, itemSize: 0);
			#else
			ObjectSelector.get.Show(null, typeof(SearchQuery), null, false, null, OnObjectSelectorClosed, null);
			#endif
        }

		private void OnObjectSelectorClosed(UnityEngine.Object obj)
		{
			if (obj is SearchQueryAsset searchQuery)
				m_TreeView.Add(new SearchCollection(searchQuery));
		}

		#if USE_SEARCH_PICKER
		private void SelectCollection(SearchItem selectedItem, bool canceled)
        {
            if (canceled)
                return;

            var searchQuery = selectedItem.ToObject<SearchQuery>();
            if (!searchQuery)
                return;
            
            m_TreeView.Add(new SearchCollection(searchQuery));
        }
		#endif

        void UpdateView()
        {
            m_TreeView.searchString = m_SearchText;
        }

        [MenuItem("Window/Search/Collections")]
        public static void ShowWindow()
        {
            SearchCollectionWindow wnd = GetWindow<SearchCollectionWindow>();
            wnd.titleContent = new GUIContent("Collections");
        }

        public void OpenContextualMenu()
        {
            var menu = new GenericMenu();

            AddCollectionMenus(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Refresh"), false, () => m_TreeView.Reload());
            menu.ShowAsContext();
        }
    }
}
#endif
