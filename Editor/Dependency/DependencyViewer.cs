#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.Search.Collections;

namespace UnityEditor.Search
{
	class DependencyViewer : EditorWindow
	{
		[SearchExpressionEvaluator(SearchExpressionEvaluationHints.ThreadNotSupported)]
		public static IEnumerable<SearchItem> Selection(SearchExpressionContext c)
		{
			foreach (var obj in UnityEditor.Selection.objects)
			{
				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(assetPath))
					yield return EvaluatorUtils.CreateItem(assetPath);
				else
					yield return EvaluatorUtils.CreateItem(obj.GetInstanceID());
			}
		}

		[Serializable]
		class DependencyTreeViewState : TreeViewState, ISearchCollectionView
		{
			[SerializeField] List<SearchCollection> m_Collections;

			public DependencyTreeViewState(string name, string filter)
			{
				m_Collections = new List<SearchCollection>();
				m_Collections.Add(new SearchCollection(name, $"{filter}=selection{{}}", "expression", "dep"));
			}

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

		SearchField m_SearchField;
		SearchCollectionTreeView m_DependencyTreeViewIns;
		SearchCollectionTreeView m_DependencyTreeViewOuts;

		[SerializeField] string m_SearchText;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyTreeViewState m_DependencyTreeViewStateIns;
		[SerializeField] DependencyTreeViewState m_DependencyTreeViewStateOuts;

		[SerializeField] List<DependencyState> m_States;
		[NonSerialized] Stack<List<DependencyState>> m_History;
		[NonSerialized] List<DependencyView> m_Views;		

		[Serializable]
		class DependencyState : SearchQuery
		{
		}

		class DependencyView : ISearchView
		{
			public string filter;
			public PropertyTable table;
			public DependencyState state;

			public SearchSelection selection => throw new NotImplementedException();
			public ISearchList results => throw new NotImplementedException();
			public SearchContext context => throw new NotImplementedException();
			public float itemIconSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
			public DisplayMode displayMode => throw new NotImplementedException();
			public bool multiselect { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
			public Rect position => throw new NotImplementedException();
			public Action<SearchItem, bool> selectCallback => throw new NotImplementedException();
			public Func<SearchItem, bool> filterCallback => throw new NotImplementedException();
			public Action<SearchItem> trackingCallback => throw new NotImplementedException();

			public void AddSelection(params int[] selection) => throw new NotImplementedException();
			public void Close() => throw new NotImplementedException();
			public void Dispose() => throw new NotImplementedException();
			public void ExecuteAction(SearchAction action, SearchItem[] items, bool endSearch = true) => throw new NotImplementedException();
			public void ExecuteSelection() => throw new NotImplementedException();
			public void Focus() => throw new NotImplementedException();
			public void FocusSearch() => throw new NotImplementedException();
			public void Refresh(RefreshFlags reason = RefreshFlags.Default) => throw new NotImplementedException();
			public void Repaint() => throw new NotImplementedException();
			public void SelectSearch() => throw new NotImplementedException();
			public void SetSearchText(string searchText, TextCursorPlacement moveCursor = TextCursorPlacement.MoveLineEnd) => throw new NotImplementedException();
			public void SetSearchText(string searchText, TextCursorPlacement moveCursor, int cursorInsertPosition) => throw new NotImplementedException();
			public void SetSelection(params int[] selection) => throw new NotImplementedException();
			public void ShowItemContextualMenu(SearchItem item, Rect contextualActionPosition) => throw new NotImplementedException();
		}

		internal void OnEnable()
		{
			m_SearchField = new SearchField();
			m_SearchText = m_SearchText ?? AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_DependencyTreeViewStateIns = m_DependencyTreeViewStateIns ?? new DependencyTreeViewState("Uses", "from");
			m_DependencyTreeViewStateOuts = m_DependencyTreeViewStateOuts ?? new DependencyTreeViewState("Used By", "to");

			m_DependencyTreeViewIns = new SearchCollectionTreeView(m_DependencyTreeViewStateIns, m_DependencyTreeViewStateIns);
			m_DependencyTreeViewOuts = new SearchCollectionTreeView(m_DependencyTreeViewStateOuts, m_DependencyTreeViewStateOuts);
			m_Splitter.host = this;

			UnityEditor.Selection.selectionChanged += OnSelectionChanged;
		}

		internal void OnDisable()
		{
			UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			m_DependencyTreeViewIns.Reload();
			m_DependencyTreeViewOuts.Reload();
			m_SearchText = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);

			titleContent.text = System.IO.Path.GetFileNameWithoutExtension(m_SearchText);
			titleContent.image = AssetDatabase.GetCachedIcon(m_SearchText);

			m_DependencyTreeViewIns.ExpandAll();
			m_DependencyTreeViewOuts.ExpandAll();
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

		[MenuItem("Window/Search/Dependency Viewer", priority = 5679)]
		public static void OpenNew()
		{
			var win = CreateWindow<DependencyViewer>();
			win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 800));
			win.Show();
		}
	}
}
#endif
