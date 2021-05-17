#if DEPENDS_ON_INTERNAL_APIS
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.Search.Collections
{ 
    class SearchCollectionTreeViewItem : SearchTreeViewItem
    {
        #if USE_SEARCH_TABLE
        public static readonly GUIContent collectionIcon = EditorGUIUtility.IconContent("ListView");
        #else
        public static readonly GUIContent collectionIcon = GUIContent.none;
        #endif

        readonly SearchCollection m_Collection;
        public SearchCollection collection => m_Collection;

        public SearchCollectionTreeViewItem(SearchCollectionTreeView treeView, SearchCollection collection)
            : base(treeView)
        {
            m_Collection = collection ?? throw new ArgumentNullException(nameof(collection));

            icon = m_Collection.query.icon != null ? m_Collection.query.icon : (collectionIcon.image as Texture2D);
            displayName = m_Collection.query.name;
            children = new List<TreeViewItem>();

            FetchItems();
        }

        public void FetchItems()
        {
            var context = SearchService.CreateContext(m_Collection.query.providerIds, m_Collection.query.text);
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
                UpdateLabel();
                m_TreeView.UpdateCollections();
                context?.Dispose();
            });
        }

        private void UpdateLabel()
        {
            displayName = $"{m_Collection.query.name} ({children.Count})";
        }

        public override void Select()
        {
            // Do nothing
        }

        public override void Open()
        {
			SearchQueryAsset.Open(m_Collection.query.GetInstanceID());
        }

        public override bool CanStartDrag()
        {
            return false;
        }

        public override void OpenContextualMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Refresh"), false, () => Refresh());
            menu.AddSeparator("");
			#if USE_SEARCH_TABLE
            menu.AddItem(new GUIContent("Set Color"), false, SelectColor);
			#endif
            menu.AddItem(new GUIContent("Open"), false, () => Open());
            menu.AddItem(new GUIContent("Edit"), false, () => Selection.activeObject = m_Collection.query);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () => m_TreeView.Remove(this, m_Collection));

            menu.ShowAsContext();
        }
        
        #if USE_SEARCH_TABLE
        private void SelectColor()
        {
            var c = collection.color;
            ColorPicker.Show(SetColor, new Color(c.r, c.g, c.b, 1.0f), false, false);
        }

        private void SetColor(Color color)
        {
            m_Collection.color = color;
        }

        #endif

        public void Refresh()
        {
            children.Clear();
            m_Collection.items.Clear();
            FetchItems();
        }

        public override bool DrawRow(Rect rowRect)
        {
            return true;
        }
    }
}
#endif
