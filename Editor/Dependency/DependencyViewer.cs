#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	class DependencyViewer : EditorWindow
	{
		[SearchExpressionEvaluator]
		public static IEnumerable<SearchItem> Selection(SearchExpressionContext c)
		{
			return TaskEvaluatorManager.EvaluateMainThread<SearchItem>(CreateItemsFromSelection);
		}

		private static void CreateItemsFromSelection(Action<SearchItem> yielder)
		{
			foreach (var obj in UnityEditor.Selection.objects)
			{
				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(assetPath))
					yielder(EvaluatorUtils.CreateItem(assetPath));
				else
					yielder(EvaluatorUtils.CreateItem(obj.GetInstanceID()));
			}
		}

		SearchField m_SearchField;
		[SerializeField] string m_SearchText;

		DependencyView m_ViewLeft;
		DependencyView m_ViewRight;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyState m_StateLeft;
		[SerializeField] DependencyState m_StateRight;

		[SerializeField] List<DependencyState> m_States;
		[NonSerialized] Stack<List<DependencyState>> m_History;
		[NonSerialized] List<DependencyView> m_Views;

		[Serializable]
		class DependencyState : IDisposable, ISerializationCallbackReceiver
		{
			private SearchQuery m_Query;
			[NonSerialized] private SearchTable m_TableConfig;

			public string guid => m_Query.guid;
			public SearchContext context => m_Query.viewState.context;
			public SearchTable tableConfig => m_TableConfig;

			public DependencyState(string name, string filter)
			{
				var context = SearchService.CreateContext(new [] { "expression", "dep" }, $"{filter}=selection{{}}");
				m_TableConfig = new SearchTable(Guid.NewGuid().ToString("N"), name, GetDefaultColumns());
				m_Query = new SearchQuery()
				{
					name = name,
					viewState = new SearchViewState(context),
					displayName = name,
					tableConfig = m_TableConfig
				};
			}

			private IEnumerable<SearchColumn> GetDefaultColumns()
			{
				return ItemSelectors.Enumerate(null);
			}

			public void Dispose()
			{
				m_Query.viewState?.context?.Dispose();
			}

			public void OnBeforeSerialize()
			{
			}

			public void OnAfterDeserialize()
			{
				if (m_TableConfig == null)
					m_TableConfig = m_Query.tableConfig;
				m_TableConfig.InitFunctors();
			}
		}

		class DependencyView : ISearchView, ITableView
		{
			public string filter;
			public PropertyTable table;
			public readonly DependencyState state;

			HashSet<SearchItem> m_Items;

			public DependencyView(DependencyState state)
			{
				this.state = state;
				m_Items = new HashSet<SearchItem>();
				Reload();
			}

			public void OnGUI(Rect rect)
			{
				table?.OnGUI(rect);
			}

			private void BuildTable()
			{
				table = new PropertyTable(state.guid, this);
			}

			public void Reload()
			{
				m_Items.Clear();
				SearchService.Request(state.context, (c, items) => m_Items.UnionWith(items), _ => BuildTable());
			}

			// ISearchView

			public SearchSelection selection => throw new NotImplementedException();
			public ISearchList results => throw new NotImplementedException();
			public SearchContext context => state.context;
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


			// ITableView

			public void AddColumn(Vector2 mousePosition, int activeColumnIndex) => throw new NotImplementedException();
			public void AddColumns(IEnumerable<SearchColumn> descriptors, int activeColumnIndex) => throw new NotImplementedException();
			public void SetupColumns(IEnumerable<SearchItem> elements = null) => throw new NotImplementedException();
			public void RemoveColumn(int activeColumnIndex) => throw new NotImplementedException();
			public void SwapColumns(int columnIndex, int swappedColumnIndex) => throw new NotImplementedException();
			public IEnumerable<SearchItem> GetRows() => throw new NotImplementedException();
			public SearchTable GetSearchTable() => throw new NotImplementedException();
			public void SetSelection(IEnumerable<SearchItem> items) => Debug.LogWarning("SetSelection");
			public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn) => throw new NotImplementedException();
			public bool OpenContextualMenu(Event evt, SearchItem item) => throw new NotImplementedException();

			public void UpdateColumnSettings(int columnIndex, MultiColumnHeaderState.Column columnSettings)
			{
				var searchColumn = state.tableConfig.columns[columnIndex];
				searchColumn.width = columnSettings.width;
				searchColumn.content = columnSettings.headerContent;
				searchColumn.options &= ~SearchColumnFlags.TextAligmentMask;
				switch (columnSettings.headerTextAlignment)
				{
					case TextAlignment.Left:
						searchColumn.options |= SearchColumnFlags.TextAlignmentLeft;
						break;
					case TextAlignment.Center:
						searchColumn.options |= SearchColumnFlags.TextAlignmentCenter;
						break;
					case TextAlignment.Right:
						searchColumn.options |= SearchColumnFlags.TextAlignmentRight;
						break;
				}
			}

			public IEnumerable<SearchItem> GetElements()
			{
				return m_Items;
			}

			public IEnumerable<SearchColumn> GetColumns()
			{
				return state.tableConfig.columns;
			}

			public void SetDirty()
			{
			}
		}

		internal void OnEnable()
		{
			m_SearchField = new SearchField();
			m_SearchText = m_SearchText ?? AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_StateLeft = m_StateLeft ?? new DependencyState("Uses", "from");
			m_StateRight = m_StateRight ?? new DependencyState("Used By", "to");

			m_ViewLeft = new DependencyView(m_StateLeft);
			m_ViewRight = new DependencyView(m_StateRight);
			m_Splitter.host = this;

			UnityEditor.Selection.selectionChanged += OnSelectionChanged;
		}

		internal void OnDisable()
		{
			UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			m_ViewLeft.Reload();
			m_ViewRight.Reload();
			m_SearchText = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);

			titleContent.text = System.IO.Path.GetFileNameWithoutExtension(m_SearchText);
			titleContent.image = AssetDatabase.GetCachedIcon(m_SearchText);

			Repaint();
		}

		internal void OnGUI()
		{
			m_Splitter.Init(position.width / 2.0f);
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
				m_ViewLeft.OnGUI(treeViewRect);

				treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				m_ViewRight.OnGUI(treeViewRect);
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
			win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 400));
			win.Show();
		}
	}
}
#endif
