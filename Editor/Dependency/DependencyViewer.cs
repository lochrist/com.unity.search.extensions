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
		void PushViewerState(DependencyViewerState state);
	}

	[AttributeUsage(AttributeTargets.Method)]
	class DependencyViewerProvider : Attribute
	{
		static List<DependencyViewerProvider> m_StateProviders;
		public static IEnumerable<DependencyViewerProvider> s_StateProviders
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
			m_StateProviders = new List<DependencyViewerProvider>();
			var methods = TypeCache.GetMethodsWithAttribute<DependencyViewerProvider>();
			foreach(var mi in methods)
			{
				try
				{
					var attr = mi.GetCustomAttributes(typeof(DependencyViewerProvider), false).Cast<DependencyViewerProvider>().First();
					attr.handler = Delegate.CreateDelegate(typeof(Func<DependencyViewerState>), mi) as Func<DependencyViewerState>;
					attr.name = attr.name ?? ObjectNames.NicifyVariableName(mi.Name);
					m_StateProviders.Add(attr);
					attr.providerId = m_StateProviders.Count - 1;
				}
				catch(Exception e)
				{
					Debug.LogError($"Cannot register State provider: {mi.Name}\n{e}");
				}				
			}
		}
		public static DependencyViewerProvider GetProvider(int id)
		{
			if (id < 0 || id >= s_StateProviders.Count())
			{
				return null;
			}
			return m_StateProviders[id];
		}
		public static DependencyViewerProvider GetDefault()
		{
			var d = s_StateProviders.FirstOrDefault(p => p.flags.HasFlag(DependencyViewerFlags.TrackSelection));
			if (d != null)
				return d;
			return s_StateProviders.First();
		}

		public string name;
		public DependencyViewerFlags flags;
		private Func<DependencyViewerState> handler;
		private int providerId;
		public DependencyViewerProvider(DependencyViewerFlags flags = DependencyViewerFlags.None, string name = null)
		{
			this.flags = flags;
			this.name = name;
		}

		public DependencyViewerState CreateState()
		{
			var state = handler();
			if (state == null)
				return null;
			state.flags |= flags;
			state.viewerProviderId = providerId;
			return state;
		}
	}

	[Serializable]
	class DependencyState : ISerializationCallbackReceiver
	{
		[SerializeField] private SearchQuery m_Query;
		[NonSerialized] private SearchTable m_TableConfig;

		public string guid => m_Query.guid;
		public SearchContext context => m_Query.viewState.context;
		public SearchTable tableConfig => m_TableConfig;

		public DependencyState(SearchQuery query)
		{
			m_Query = query;
			m_TableConfig = query.tableConfig == null || query.tableConfig.columns.Length == 0 ? DependencyBuiltinStates.CreateDefaultTable(query.name) : query.tableConfig;
		}

		public DependencyState(SearchQueryAsset query)
			: this(query.ToSearchQuery())
		{
		}

		public DependencyState(string name, SearchContext context, SearchTable tableConfig)
		{
			m_TableConfig = tableConfig;
			m_Query = new SearchQuery()
			{
				name = name,
				viewState = new SearchViewState(context),
				displayName = name,
				tableConfig = m_TableConfig
			};
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
		public DependencyViewerFlags flags;
		public List<DependencyState> states;
		public List<string> globalIds;
		public string name;
		[SerializeField] internal int viewerProviderId;
		[SerializeField] private GUIContent m_Description;
		[SerializeField] private GUIContent m_WindowTitle;

		public DependencyViewerState(string name, DependencyState state)
				: this(name, null, new[] { state })
		{
		}

		public DependencyViewerState(string name, IEnumerable<DependencyState> states = null)
				: this(name, null, states)
		{
		}


		public DependencyViewerState(string name, List<string> globalIds, IEnumerable<DependencyState> states = null)
		{
			this.name = name;
			this.globalIds = globalIds;
			this.states = states != null ? states.ToList() : new List<DependencyState>();
			viewerProviderId = -1;
		}

		public DependencyViewerProvider viewerProvider => DependencyViewerProvider.GetProvider(viewerProviderId) ?? DependencyViewerProvider.GetDefault();
		public bool trackSelection => flags.HasFlag(DependencyViewerFlags.TrackSelection);

		public GUIContent description
		{
			get
			{
				if (m_Description == null)
				{
					if (globalIds != null)
					{
						var names = EnumeratePaths().ToList();
						if (names.Count == 0)
							m_Description = new GUIContent("No dependencies");
						else if (names.Count == 1)
							m_Description = new GUIContent(string.Join(", ", names), GetPreview());
						else if (names.Count < 4)
							m_Description = new GUIContent(string.Join(", ", names), Icons.dependencies);
						else
							m_Description = new GUIContent($"{names.Count} object selected", string.Join("\n", names));
					}
					else
					{
						m_Description = new GUIContent(name);
					}					
				}
				return m_Description;
			}
			set
			{
				m_Description = value;
			}
		}

		public GUIContent windowTitle		
		{
			get
			{
				if (m_WindowTitle == null)
				{
					if (globalIds != null)
					{
						var names = EnumeratePaths().ToList();
						if (names.Count != 1)
							m_WindowTitle = new GUIContent($"Dependency Viewer ({names.Count})", Icons.dependencies);
						else
							m_WindowTitle = new GUIContent(System.IO.Path.GetFileNameWithoutExtension(names.First()), GetIcon());
					}
					else
					{
						m_WindowTitle = new GUIContent(name);
					}					
				}

				return m_WindowTitle;
			}
			set
			{
				m_WindowTitle = value;
			}
		}

		Texture GetIcon()
		{
			if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
				return Icons.dependencies;
			return AssetDatabase.GetCachedIcon(AssetDatabase.GUIDToAssetPath(gid.m_AssetGUID)) ?? Icons.dependencies;
		}

		Texture GetPreview()
		{
			if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
				return Icons.dependencies;
			var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
			return AssetPreview.GetAssetPreview(obj)
				?? AssetPreview.GetAssetPreviewFromGUID(gid.assetGUID.ToString())
				?? Icons.dependencies;
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
				else if (EditorUtility.InstanceIDToObject(instanceId) is UnityEngine.Object obj)
					yield return SearchUtils.GetObjectPath(obj).Substring(1);
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

		public bool IsReadOnly()
		{
			return false;
		}

		public void AddColumnHeaderContextMenuItems(GenericMenu menu, SearchColumn sourceColumn)
		{
			menu.AddItem(new GUIContent("Open in Search"), false, OpenStateInSearch);
		}

		public bool OpenContextualMenu(Event evt, SearchItem item)
		{
			var menu = new GenericMenu();
			var currentSelection = new[] { item };
			foreach (var action in item.provider.actions.Where(a => a.enabled(currentSelection)))
			{
				var itemName = !string.IsNullOrWhiteSpace(action.content.text) ? action.content.text : action.content.tooltip;
				menu.AddItem(new GUIContent(itemName, action.content.image), false, () => ExecuteAction(action, currentSelection));
			}

			menu.ShowAsContext();
			evt.Use();
			return true;
		}

		public void ExecuteAction(SearchAction action, SearchItem[] items)
		{
			var item = items.LastOrDefault();
			if (item == null)
				return;

			if (action.handler != null && items.Length == 1)
				action.handler(item);
			else if (action.execute != null)
				action.execute(items);
			else
				action.handler?.Invoke(item);
		}

		UnityEngine.Object GetObject(in SearchItem item)
		{
			UnityEngine.Object obj = null;
			var path = SearchUtils.GetAssetPath(item);
			if (!string.IsNullOrEmpty(path))
				obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
			if (!obj)
				obj = item.ToObject();
			return obj;
		}

		public void SetSelection(IEnumerable<SearchItem> items)
		{
			var firstItem = items.FirstOrDefault();
			if (firstItem == null)
				return;
			var obj = GetObject(firstItem);
			if (!obj)
				return;
			EditorGUIUtility.PingObject(obj);
		}

		public void DoubleClick(SearchItem item)
		{
			var obj = GetObject(item);
			if (!obj)
				return;
			host.PushViewerState(DependencyBuiltinStates.ObjectDependencies(obj));
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

		[ShortcutManagement.Shortcut("dep_goto_prev_state", typeof(DependencyViewer), KeyCode.LeftArrow, ShortcutManagement.ShortcutModifiers.Alt)]
		internal static void GotoPrev(ShortcutManagement.ShortcutArguments args)
		{
			if (args.context is DependencyViewer viewer)
				viewer.GotoPrevStates();
		}

		[ShortcutManagement.Shortcut("dep_goto_next_state", typeof(DependencyViewer), KeyCode.RightArrow, ShortcutManagement.ShortcutModifiers.Alt)]
		internal static void GotoNext(ShortcutManagement.ShortcutArguments args)
		{
			if (args.context is DependencyViewer viewer)
				viewer.GotoNextStates();
		}

		internal void OnEnable()
		{
			titleContent = new GUIContent("Dependency Viewer", Icons.dependencies);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_CurrentState = m_CurrentState ?? DependencyViewerProvider.GetDefault().CreateState();
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
			if (UnityEditor.Selection.objects.Length == 0 || m_LockSelection || !m_CurrentState.trackSelection)
				return;
			PushViewerState(m_CurrentState.viewerProvider.CreateState());
			Repaint();
		}

		private void SetViewerState(DependencyViewerState state)
		{
			m_CurrentState = state;
			m_Views = BuildViews(m_CurrentState);
			titleContent = m_CurrentState.windowTitle;
		}

		public void PushViewerState(DependencyViewerState state)
		{
			if (state == null)
				return;
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
					GUILayout.Label(m_CurrentState.description, GUILayout.Height(18f));
					GUILayout.FlexibleSpace();
					if (EditorGUILayout.DropdownButton(new GUIContent(m_CurrentState.name), FocusType.Passive))
						OnSourceChange();
					EditorGUI.BeginChangeCheck();

					EditorGUI.BeginDisabled(!m_CurrentState.trackSelection);
					m_LockSelection = GUILayout.Toggle(m_LockSelection, GUIContent.none, Styles.lockButton);
					if (EditorGUI.EndChangeCheck() && !m_LockSelection)
						OnSelectionChanged();
					EditorGUI.EndDisabled();
				}

				using (SearchMonitor.GetView())
				{
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
		}

		private void OnSourceChange()
		{			
			var menu = new GenericMenu();
			foreach(var stateProvider in DependencyViewerProvider.s_StateProviders.Where(s => s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState()));
			menu.AddSeparator("");
			foreach (var stateProvider in DependencyViewerProvider.s_StateProviders.Where(s => !s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState()));

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
					menu.AddItem(new GUIContent(sq.name, sq.description), false, () => PushViewerState(DependencyBuiltinStates.CreateStateFromQuery(sq)));
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

		[SearchExpressionEvaluator("deps", SearchExpressionType.Iterable)]
		public static IEnumerable<SearchItem> SceneUses(SearchExpressionContext c)
		{
			var args = c.args[0].Execute(c);
			foreach (var e in args)
			{
				if (e == null || e.value == null)
				{
					yield return null;
					continue;
				}

				var id = e.value.ToString();
				if (Utils.TryParse(id, out int instanceId))
				{
					var assetProvider = SearchService.GetProvider(Providers.AssetProvider.type);
					var sceneProvider = SearchService.GetProvider(Providers.BuiltInSceneObjectsProvider.type);
					foreach (var item in TaskEvaluatorManager.EvaluateMainThread(() =>
						GetSceneObjectDependencies(c.search, sceneProvider, assetProvider, instanceId).ToList()))
					{
						yield return item;
					}
				}
			}
		}

		private static IEnumerable<SearchItem> GetSceneObjectDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider assetProvider, int instanceId)
		{
			var obj = EditorUtility.InstanceIDToObject(instanceId);
			if (!obj)
				yield break;

			var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
			if (!go && obj is Component goc)
			{
				foreach (var ce in GetComponentDependencies(context, sceneProvider, assetProvider, goc))
					yield return ce;
			}
			else if (go)
			{
				// Index any prefab reference
				var containerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
				if (!string.IsNullOrEmpty(containerPath))
					yield return Providers.AssetProvider.CreateItem("DEPS", context, assetProvider, null, containerPath, 0, SearchDocumentFlags.Asset);

				var gocs = go.GetComponents<Component>();
				for (int componentIndex = 1; componentIndex < gocs.Length; ++componentIndex)
				{
					var c = gocs[componentIndex];
					if (!c || (c.hideFlags & HideFlags.HideInInspector) == HideFlags.HideInInspector)
						continue;

					foreach (var ce in GetComponentDependencies(context, sceneProvider, assetProvider, c))
						yield return ce;
				}
			}
		}

		static IEnumerable<SearchItem> GetComponentDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider assetProvider, Component c)
		{
			using (var so = new SerializedObject(c))
			{
				var p = so.GetIterator();
				var next = p.NextVisible(true);
				while (next)
				{
					if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
					{
						var assetPath = AssetDatabase.GetAssetPath(p.objectReferenceValue);
						if (!string.IsNullOrEmpty(assetPath))
							yield return Providers.AssetProvider.CreateItem("DEPS", context, assetProvider, null, assetPath, 0, SearchDocumentFlags.Asset);
						else if (p.objectReferenceValue is GameObject cgo)
							yield return Providers.SceneProvider.AddResult(context, sceneProvider, cgo);
						else if (p.objectReferenceValue is Component cc && cc.gameObject)
							yield return Providers.SceneProvider.AddResult(context, sceneProvider, cc.gameObject);
					}
					next = p.NextVisible(p.hasVisibleChildren);
				}
			}
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
