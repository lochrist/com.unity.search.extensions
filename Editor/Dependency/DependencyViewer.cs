using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[EditorWindowTitle(title ="Dependency Viewer")]
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
			m_CurrentState = m_CurrentState ?? DependencyViewerProviderAttribute.GetDefault().CreateState();
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
			foreach(var stateProvider in DependencyViewerProviderAttribute.s_StateProviders.Where(s => s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState()));
			menu.AddSeparator("");
			foreach (var stateProvider in DependencyViewerProviderAttribute.s_StateProviders.Where(s => !s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
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
