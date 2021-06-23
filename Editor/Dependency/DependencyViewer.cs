#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		void Repaint();
	}

	[AttributeUsage(AttributeTargets.Method)]
	class DependencyViewerStateAttribute : Attribute
	{
		static List<DependencyViewerStateAttribute> m_StateProviders;
		public static IEnumerable<DependencyViewerStateAttribute> s_StateProviders
		{
			get
			{
				if (m_StateProviders == null)
					FetchStateProviders();
				return m_StateProviders;
			}
		}
		static void FetchStateProviders()
		{
			m_StateProviders = new List<DependencyViewerStateAttribute>();
			var methods = TypeCache.GetMethodsWithAttribute<DependencyViewerStateAttribute>();
			foreach(var mi in methods)
			{
				try
				{
					var attr = mi.GetCustomAttributes(typeof(DependencyViewerStateAttribute), false).Cast<DependencyViewerStateAttribute>().First();
					attr.handler = Delegate.CreateDelegate(typeof(Func<DependencyViewerState>), mi) as Func<DependencyViewerState>;
					attr.name = attr.name ?? ObjectNames.NicifyVariableName(mi.Name);
					m_StateProviders.Add(attr);
				}
				catch(Exception e)
				{
					Debug.LogError($"Cannot register State provider: {mi.Name}\n{e}");
				}				
			}
		}

		public string name;
		public Func<DependencyViewerState> handler;
		public DependencyViewerStateAttribute(string name = null)
		{
			this.name = name;
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

		public DependencyState(SearchQuery query)
		{
			m_Query = query;
			m_TableConfig = query.tableConfig == null || query.tableConfig.columns.Length == 0 ? CreateDefaultTable(query.name) : query.tableConfig;
		}

		public DependencyState(SearchQueryAsset query)
		{
			m_Query = query.ToSearchQuery();
			m_TableConfig = m_Query.tableConfig == null || m_Query.tableConfig.columns.Length == 0 ? CreateDefaultTable(query.name) : m_Query.tableConfig;
		}

		public DependencyState(string name, SearchContext context, SearchTable tableConfig = null)
		{
			m_Name = name;
			m_TableConfig = tableConfig ?? CreateDefaultTable(name);
			m_Query = new SearchQuery()
			{
				name = name,
				viewState = new SearchViewState(context),
				displayName = name,
				tableConfig = m_TableConfig
			};
		}

		static public IEnumerable<SearchColumn> GetDefaultColumns(string tableName)
		{
			var defaultDepFlags = SearchColumnFlags.CanSort;
			yield return new SearchColumn(tableName, "label", "Name", null, defaultDepFlags);
			yield return new SearchColumn("Type", "type", null, defaultDepFlags | SearchColumnFlags.IgnoreSettings) { width = 60 };
			yield return new SearchColumn("Size", "size", "size", null, defaultDepFlags);
			yield return new SearchColumn("Used #", "usedByCount", null, defaultDepFlags);
		}

		static public SearchTable CreateDefaultTable(string tableName)
		{
			return new SearchTable(Guid.NewGuid().ToString("N"), tableName, GetDefaultColumns(tableName));
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

	[Serializable]
	class DependencyViewerState
	{
		public string status;
		public List<DependencyState> states;
		public List<string> globalIds;
		[SerializeField] private GUIContent title;
		[SerializeField] private GUIContent windowTitle;

		public DependencyViewerState(string status, IEnumerable<DependencyState> states = null)
				: this(status, new List<string>(), new List<DependencyState>())
		{
			title = new GUIContent(status);
			this.states = states != null ? states.ToList() : new List<DependencyState>();
		}

		public DependencyViewerState(string status, List<string> globalIds, IEnumerable<DependencyState> states = null)
		{
			this.status = status;
			this.globalIds = globalIds ?? new List<string>();
			this.states = states != null ? states.ToList() : new List<DependencyState>();
		}

		public GUIContent GetTitle()
		{
			if (title == null)
			{
				var names = EnumeratePaths().ToList();
				if (names.Count == 0)
					title = new GUIContent("No dependencies");
				else if (names.Count == 1)
					title = new GUIContent(string.Join(", ", names), AssetDatabase.GetCachedIcon(names[0]));
				else if (names.Count < 4)
					title = new GUIContent(string.Join(", ", names));
				else
					title = new GUIContent($"{names.Count} object selected", string.Join("\n", names));
			}

			return title;
		}

		public GUIContent GetWindowTitle()
		{
			if (windowTitle == null)
			{
				var names = EnumeratePaths().ToList();
				if (names.Count != 1)
					windowTitle = new GUIContent($"Dependency Viewer ({names.Count})", Icons.dependencies);
				else
					windowTitle = new GUIContent(System.IO.Path.GetFileNameWithoutExtension(names.First()), AssetDatabase.GetCachedIcon(names[0]));
			}

			return windowTitle;
		}

		IEnumerable<string> EnumeratePaths()
		{
			if (globalIds == null || globalIds.Count == 0)
				yield break;

			foreach (var sgid in globalIds)
			{
				if (!GlobalObjectId.TryParse(sgid, out var gid))
					continue;
				var instanceId = GlobalObjectId.GlobalObjectIdentifierToInstanceIDSlow(gid);
				var assetPath = AssetDatabase.GetAssetPath(instanceId);
				if (!string.IsNullOrEmpty(assetPath))
					yield return assetPath;
			}
		}
	}

	class DependencyTableView : ITableView
	{
		public PropertyTable table;
		public readonly DependencyState state;

		HashSet<SearchItem> m_Items;

		public IDependencyViewHost host { get; private set; }
		public SearchContext context => state.context;

		public DependencyTableView(DependencyState state, IDependencyViewHost host)
		{
			this.host = host;
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
			host.Repaint();
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
		public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
		{
			menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
		}

		public bool OpenContextualMenu(Event evt, SearchItem item)
		{
			return false;
		}

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
			host.Repaint();
		}

		private void OpenStateInSearch()
		{
			var searchViewState = new SearchViewState(state.context)
			{
				tableConfig = state.tableConfig
			};
			SearchService.ShowWindow(searchViewState);
		}
	}

	[EditorWindowTitle(icon = "UnityEditor.FindDependencies", title ="Dependency Viewer")]
	class DependencyViewer : EditorWindow, IDependencyViewHost
	{
		static class Styles
		{
			public static GUIStyle lockButton = "IN LockButton";
		}

		[SerializeField] bool m_LockSelection;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyViewerState m_CurrentState;

		int m_HistoryCursor = -1;
		List<DependencyViewerState> m_History;
		List<DependencyTableView> m_Views;

		internal void OnEnable()
		{
			titleContent = new GUIContent("Dependency Viewer", Icons.dependencies);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_CurrentState = m_CurrentState ?? DependencyBuiltinStates.TrackSelection();
			m_History = new List<DependencyViewerState>();
			m_Splitter.host = this;
			PushViewerState(m_CurrentState);
			UnityEditor.Selection.selectionChanged += OnSelectionChanged;
		}

		List<DependencyTableView> BuildViews(DependencyViewerState state)
		{
			return state.states.Select(s => new DependencyTableView(s, this)).ToList();
		}

		internal void OnDisable()
		{
			UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			if (UnityEditor.Selection.objects.Length == 0 || m_LockSelection)
				return;
			UpdateSelection();
			Repaint();
		}

		private void SetViewerState(DependencyViewerState state)
		{
			m_CurrentState = state;
			m_Views = BuildViews(m_CurrentState);
			titleContent = m_CurrentState.GetWindowTitle();
		}

		void UpdateSelection()
		{
			PushViewerState(DependencyBuiltinStates.TrackSelection());
			Repaint();
		}

		void PushViewerState(DependencyViewerState state)
		{
			SetViewerState(state);
			if (m_CurrentState.states.Count != 0)
			{
				m_History.Add(m_CurrentState);
				m_HistoryCursor = m_History.Count - 1;
			}
		}

		internal void OnGUI()
		{
			m_Splitter.Init(position.width / 2.0f);
			var evt = Event.current;

			using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
			{
				using (new GUILayout.HorizontalScope(Search.Styles.searchReportField))
				{
					EditorGUI.BeginDisabled(m_HistoryCursor <= 0);
					if (GUILayout.Button("<"))
						GotoPrevStates();
					EditorGUI.EndDisabled();
					EditorGUI.BeginDisabled(m_HistoryCursor == m_History.Count-1);
					if (GUILayout.Button(">"))
						GotoNextStates();
					EditorGUI.EndDisabled();
					GUILayout.Label(m_CurrentState.GetTitle(), GUILayout.Height(18f));
					GUILayout.FlexibleSpace();
					if (EditorGUILayout.DropdownButton(new GUIContent(m_CurrentState.status ?? "Source"), FocusType.Passive))
						OnSourceChange();
					EditorGUI.BeginChangeCheck();
					m_LockSelection = GUILayout.Toggle(m_LockSelection, GUIContent.none, Styles.lockButton);
					if (EditorGUI.EndChangeCheck() && !m_LockSelection)
						OnSelectionChanged();
				}

				if (m_Views != null && m_Views.Count >= 1)
				{
					EditorGUILayout.BeginHorizontal();
					var treeViewRect = m_Views.Count >= 2 ?
						EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1))) :
						EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
					m_Views[0].OnGUI(treeViewRect);
					if (m_Views.Count >= 2)
					{
						m_Splitter.Draw(evt, treeViewRect);
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

		private void OnSourceChange()
		{			
			var menu = new GenericMenu();
			foreach(var stateProvider in DependencyViewerStateAttribute.s_StateProviders)
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.handler()));

			menu.AddSeparator("");

			var depQueries = SearchQueryAsset.savedQueries.Where(sq =>
			{
				var labels = AssetDatabase.GetLabels(sq);
				return labels.Any(l => l.ToLowerInvariant() == "dependencies");
			}).ToArray();
			if (depQueries.Length > 0)
			{
				foreach (var sq in depQueries)
				{
					menu.AddItem(new GUIContent(sq.name, sq.description), false, () => PushViewerState(new DependencyViewerState(sq.name, new[] { new DependencyState(sq) })));
				}
				menu.AddSeparator("");
			}
			
			menu.AddItem(new GUIContent("Build"), false, () => Dependency.Build());
			menu.ShowAsContext();
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
	}
}
#endif
