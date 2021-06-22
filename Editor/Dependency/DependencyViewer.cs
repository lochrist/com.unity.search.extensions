#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[EditorWindowTitle(icon = "UnityEditor.FindDependencies", title ="Dependency Viewer")]
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

		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyViewerState m_CurrentState;
		List<DependencyViewerState> m_History;
		int m_HistoryCursor = -1;
		List<DependencyTableView> m_Views;

		[Serializable]
		class DependencyViewerState
		{
			public string dependencyTitle;
			public string windowTitle;
			public Texture2D icon;
			public List<DependencyState> states;

			public DependencyViewerState(string title, IEnumerable<DependencyState> states = null)
			{
				dependencyTitle = title;
				windowTitle = title;
				this.states = states != null ? states.ToList() : new List<DependencyState>();
			}
		}

		[Serializable]
		class DependencyState : ISerializationCallbackReceiver
		{
			[SerializeField] private string m_Name;
			[SerializeField] private SearchQuery m_Query;
			[NonSerialized] private SearchTable m_TableConfig;

			public string guid => m_Query.guid;
			public SearchContext context => m_Query.viewState.context;
			public SearchTable tableConfig => m_TableConfig;

			public DependencyState(string name, SearchContext context, SearchTable tableConfig)
			{				
				m_Name = name;				
				m_TableConfig = tableConfig ?? new SearchTable(Guid.NewGuid().ToString("N"), name, GetDefaultColumns());
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
				var defaultDepFlags = SearchColumnFlags.CanSort;
				yield return new SearchColumn(m_Name, "label", "Name", null, defaultDepFlags);
				yield return new SearchColumn("Type", "type", null, defaultDepFlags | SearchColumnFlags.IgnoreSettings) { width = 60 };
				yield return new SearchColumn("Size", "size", "size", null, defaultDepFlags);
				yield return new SearchColumn("Used #", "usedByCount", null, defaultDepFlags);
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
				m_TableConfig?.InitFunctors();
			}
		}

		
		class DependencyTableView : ITableView
		{
			public string filter;
			public PropertyTable table;
			public readonly DependencyState state;

			HashSet<SearchItem> m_Items;

			public SearchContext context => state.context;

			public DependencyTableView(DependencyState state)
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

			// ITableView

			public void AddColumn(Vector2 mousePosition, int activeColumnIndex) => throw new NotImplementedException();
			public void AddColumns(IEnumerable<SearchColumn> descriptors, int activeColumnIndex) => throw new NotImplementedException();
			public void SetupColumns(IEnumerable<SearchItem> elements = null) => throw new NotImplementedException();
			public void RemoveColumn(int activeColumnIndex) => throw new NotImplementedException();
			public void SwapColumns(int columnIndex, int swappedColumnIndex) => throw new NotImplementedException();
			public IEnumerable<SearchItem> GetRows() => throw new NotImplementedException();
			public SearchTable GetSearchTable() => throw new NotImplementedException();
			public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn) => throw new NotImplementedException();
			public bool OpenContextualMenu(Event evt, SearchItem item) => throw new NotImplementedException();

			public void SetSelection(IEnumerable<SearchItem> items)
			{
				var firstItem = items.FirstOrDefault();
				if (firstItem != null)
					Utils.PingAsset(SearchUtils.GetAssetPath(firstItem));
			}

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

				SearchColumnSettings.Save(searchColumn);
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
			titleContent = new GUIContent("Dependency Viewer", EditorGUIUtility.LoadIcon("UnityEditor.FindDependencies"));
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_CurrentState = m_CurrentState ?? new DependencyViewerState("No Dependencies");
			m_History = new List<DependencyViewerState>();
			m_Splitter.host = this;
			UnityEditor.Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.delayCall += UpdateSelection;
		}

		static DependencyViewerState BuildSelectionTrackingState(DependencyViewerState previousState)
		{
			if (UnityEditor.Selection.objects.Length == 0)
				return new DependencyViewerState("No Selection");

			var selectedPaths = new List<string>();
			var singleAssetPath = "";
			foreach (var obj in UnityEditor.Selection.objects)
			{
				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(assetPath))
				{
					selectedPaths.Add("\"" + assetPath + "\"");
					if (singleAssetPath == "")
						singleAssetPath = assetPath;
				}
				else
					selectedPaths.Add(obj.GetInstanceID().ToString());
			}

			var providers = new[] { "expression", "dep" };
			var selectedPathsStr = string.Join(",", selectedPaths);
			var title = System.IO.Path.GetFileNameWithoutExtension(singleAssetPath);
			if (UnityEditor.Selection.objects.Length > 1)
			{								
				title = $"{UnityEditor.Selection.objects.Length} Objects selected";
			}

			var state = new DependencyViewerState(title, new [] {
				new DependencyState("Uses", SearchService.CreateContext(providers, $"from=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 1 ? previousState.states[0].tableConfig : null),
				new DependencyState("Used By", SearchService.CreateContext(providers, $"to=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 2 ? previousState.states[1].tableConfig : null)
			});
			if (singleAssetPath != "")
				state.icon = AssetDatabase.GetCachedIcon(singleAssetPath) as Texture2D;
			return state;
		}

		List<DependencyTableView> BuildViews(DependencyViewerState state)
		{
			return state.states.Select(s => new DependencyTableView(s)).ToList();
		}

		internal void OnDisable()
		{
			UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			if (UnityEditor.Selection.objects.Length == 0)
				return;
			UpdateSelection();
			Repaint();
		}

		private void SetViewerState(DependencyViewerState state)
		{
			m_CurrentState = state;
			m_Views = BuildViews(m_CurrentState);
			
			titleContent.text = m_CurrentState.windowTitle;
			titleContent.image = m_CurrentState.icon;
		}

		void UpdateSelection()
		{
			SetViewerState(BuildSelectionTrackingState(m_CurrentState));
			if (m_CurrentState.states.Count != 0)
			{
				m_History.Add(m_CurrentState);
				m_HistoryCursor = m_History.Count - 1;
			}
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
					EditorGUI.BeginDisabled(m_HistoryCursor <= 0);
					if (GUILayout.Button("<"))
						GotoPrevStates();
					EditorGUI.EndDisabled();
					EditorGUI.BeginDisabled(m_HistoryCursor == m_History.Count-1);
					if (GUILayout.Button(">"))
						GotoNextStates();
					EditorGUI.EndDisabled();
					GUILayout.Label(m_CurrentState.dependencyTitle);
					GUILayout.FlexibleSpace();
				}

				if (m_Views != null && m_Views.Count >= 1)
				{
					EditorGUILayout.BeginHorizontal();
					var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1)));
					m_Splitter.Draw(evt, treeViewRect);
					m_Views[0].OnGUI(treeViewRect);

					if (m_Views.Count >= 2)
					{
						treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
						m_Views[1].OnGUI(treeViewRect);

						if (evt.type == EventType.Repaint)
						{
							GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
												EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 1f, 0f);
						}
					}

					EditorGUILayout.EndHorizontal();
				}
			}
		}

		private void GotoNextStates()
		{
			SetViewerState(m_History[++m_HistoryCursor]);
			Repaint();
		}

		private void GotoPrevStates()
		{
			SetViewerState(m_History[--m_HistoryCursor]);
			Repaint();
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
