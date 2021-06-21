#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.Search.Collections;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
	[Serializable]
	class DependencyTreeViewState : TreeViewState, ISearchCollectionView
	{
		[SerializeField] List<SearchCollection> m_Collections;

		public DependencyTreeViewState(string filter)
		{
			m_Collections = new List<SearchCollection>();
			m_Collections.Add(new SearchCollection(AssetDatabase.LoadAssetAtPath<SearchQueryAsset>("Assets/Search/Queries/Rocks & Trees.asset")));
		}

// 		protected override TreeViewItem BuildRoot()
// 		{
// 			var id = 1;
// 			var root = new TreeViewItem { id = id++, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
// 
// 			var obj = Selection.activeObject;
// 			if (!obj)
// 				return root;
// 			var assetPath = AssetDatabase.GetAssetPath(obj);
// 			if (string.IsNullOrEmpty(assetPath))
// 				return root;
// 			var depProvider = SearchService.GetProvider("dep");
// 			using (var context = SearchService.CreateContext(depProvider, $"{m_Filter}=\"{assetPath}\""))
// 			using (var request = SearchService.Request(context, SearchFlags.Synchronous))
// 			{
// 				foreach (var r in request)
// 				{
// 					if (r == null)
// 						continue;
// 					var path = AssetDatabase.GUIDToAssetPath(r.id);
// 					root.AddChild(new TreeViewItem(id++, 0, path));
// 				}
// 			}
// 			return root;
// 		}

		public string searchText { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public ICollection<SearchCollection> collections => m_Collections;

		public void AddCollectionMenus(GenericMenu menu)
		{
			throw new NotImplementedException();
		}

		public void OpenContextualMenu()
		{
			throw new NotImplementedException();
		}

		public void SaveCollections()
		{
			throw new NotImplementedException();
		}
	}

	class DependencyManager : EditorWindow
	{
		SearchField m_SearchField;
		SearchCollectionTreeView m_DependencyTreeViewIns;
		SearchCollectionTreeView m_DependencyTreeViewOuts;

		[SerializeField] string m_SearchText;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyTreeViewState m_DependencyTreeViewStateIns;
		[SerializeField] DependencyTreeViewState m_DependencyTreeViewStateOuts;

		internal void OnEnable()
		{
			m_SearchField = new SearchField();
			m_SearchText = m_SearchText ?? AssetDatabase.GetAssetPath(Selection.activeObject);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_DependencyTreeViewStateIns = m_DependencyTreeViewStateIns ?? new DependencyTreeViewState("to");
			m_DependencyTreeViewStateOuts = m_DependencyTreeViewStateOuts ?? new DependencyTreeViewState("from");

			m_DependencyTreeViewIns = new SearchCollectionTreeView(m_DependencyTreeViewStateIns, m_DependencyTreeViewStateIns);
			m_DependencyTreeViewOuts = new SearchCollectionTreeView(m_DependencyTreeViewStateOuts, m_DependencyTreeViewStateOuts);
			m_Splitter.host = this;

			Selection.selectionChanged += OnSelectionChanged;
		}

		internal void OnDisable()
		{
			Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			m_DependencyTreeViewIns.Reload();
			m_DependencyTreeViewOuts.Reload();
			m_SearchText = AssetDatabase.GetAssetPath(Selection.activeObject);

			titleContent.text = System.IO.Path.GetFileNameWithoutExtension(m_SearchText);
			titleContent.image = AssetDatabase.GetCachedIcon(m_SearchText);
		}

		internal void OnGUI()
		{
			m_Splitter.Init(200f);
			var evt = Event.current;

			using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
			{
				using (new GUILayout.HorizontalScope(Styles.searchReportField))
				{
					var searchTextRect = m_SearchField.GetLayoutRect(m_SearchText, position.width, (Styles.toolbarButton.fixedWidth + Styles.toolbarButton.margin.left) + Styles.toolbarButton.margin.right);
					var searchClearButtonRect = Styles.searchFieldBtn.margin.Remove(searchTextRect);
					searchClearButtonRect.xMin = searchClearButtonRect.xMax - 20f;

					if (Event.current.type == EventType.MouseUp && searchClearButtonRect.Contains(Event.current.mousePosition))
						m_SearchText = string.Empty;

					var previousSearchText = m_SearchText;
					if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.None || Event.current.character != '\r')
					{
						m_SearchText = m_SearchField.Draw(searchTextRect, m_SearchText, Styles.searchField);
						if (!string.Equals(previousSearchText, m_SearchText, StringComparison.Ordinal))
						{
							// TODO: update search
						}
					}

					if (!string.IsNullOrEmpty(m_SearchText))
					{
						if (GUI.Button(searchClearButtonRect, Icons.clear, Styles.searchFieldBtn))
							m_SearchText = string.Empty;
						EditorGUIUtility.AddCursorRect(searchClearButtonRect, MouseCursor.Arrow);
					}

					EditorGUIUtility.AddCursorRect(searchClearButtonRect, MouseCursor.Arrow);
				}

				EditorGUILayout.BeginHorizontal();
				var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1)));
				m_Splitter.Draw(evt, treeViewRect);
				m_DependencyTreeViewIns.OnGUI(treeViewRect);

				treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				m_DependencyTreeViewOuts.OnGUI(treeViewRect);
				EditorGUILayout.EndHorizontal();

				if (evt.type == EventType.Repaint)
				{
					GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
										EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 1f, 0f);
				}
			}
		}

		[MenuItem("Window/Search/Dependency Manager", priority = 5679)]
		public static void OpenNew()
		{
			var win = CreateWindow<DependencyManager>();
			win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 800));
			win.Show();
		}
	}
}
#endif
