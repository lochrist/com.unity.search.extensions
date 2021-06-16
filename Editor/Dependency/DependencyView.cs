#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
	[SearchResultView, Icon("UnityEditor.FindDependencies")]
	class DependencyView : ResultView
	{
		private const float kInitialPosOffset = -0;
		private const float kNodeSize = 100.0f;
		private const float kHalfNodeSize = kNodeSize / 2.0f;
		private const float kPreviewSize = 64.0f;

		private readonly Color kWeakInColor = new Color(240f / 255f, 240f / 255f, 240f / 255f);
		private readonly Color kWeakOutColor = new Color(120 / 255f, 134f / 255f, 150f / 255f);
		private readonly Color kDirectInColor = new Color(146 / 255f, 196 / 255f, 109 / 255f);
		private readonly Color kDirectOutColor = new Color(83 / 255f, 150 / 255f, 153 / 255f);

		private Node nodeOfInterest;
		private Graph graph;
		private DependencyDatabase db;
		private IGraphLayout graphLayout;
		[SerializeField] private UnityEngine.Object m_Selection;

		private Node selecteNode = null;
		private string status = "";
		private float zoom = 1.0f;
		private Vector2 pan = new Vector2(kInitialPosOffset, kInitialPosOffset);

		public DependencyView(ISearchView hostView)
			: base(hostView)
		{
			m_Selection = m_Selection ?? Selection.activeObject;

			if (db == null)
				db = DependencyManager.Scan();

			graph = new Graph(db) { nodeInitialPositionCallback = GetNodeInitialPosition };
		}

		public override void Draw(Rect rect, ICollection<int> selection)
		{
			if (db == null || graph == null || graph.nodes == null)
				return;

			if (nodeOfInterest == null)
			{
				var selectedAssetPath = AssetDatabase.GetAssetPath(m_Selection);
				int selectedResourceID = db.FindResourceByName(selectedAssetPath);
				if (selectedResourceID < 0)
					return;

				nodeOfInterest = graph.BuildGraph(selectedResourceID, rect.center);
				SetLayout(new OrganicLayout());
				pan = nodeOfInterest.rect.center + this.rect.size + new Vector2(200, 200);
			}

			var evt = Event.current;
			DrawView(evt);
			DrawHUD(evt);
			HandleEvent(evt);
		}

		private Rect GetNodeInitialPosition(Graph graphModel, Vector2 offset)
		{
			return new Rect(
				offset.x + Random.Range(-rect.width / 2, rect.width / 2),
				offset.y + Random.Range(-rect.height / 2, rect.height / 2),
				kNodeSize, kNodeSize);
		}

		private void HandleEvent(Event e)
		{
			if (e.type == EventType.MouseDrag)
			{
				pan.x += e.delta.x / zoom;
				pan.y += e.delta.y / zoom;
				e.Use();
			}
			else if (e.type == EventType.ScrollWheel)
			{
				var zoomDelta = 0.1f;
				float delta = e.delta.x + e.delta.y;
				zoomDelta = delta < 0 ? zoomDelta : -zoomDelta;

				float oldZoom = zoom;
				zoom = Mathf.Clamp(zoom + zoomDelta, 0.2f, 6.25f);

				var areaMousePos = (e.mousePosition - rect.position) - (rect.center - rect.position);
				var contentOldMousePos = (areaMousePos / oldZoom) - (pan / oldZoom);
				var contentMousePos = (areaMousePos / zoom) - (pan / zoom);
				var mouseDelta = contentMousePos - contentOldMousePos;

				pan += mouseDelta * -zoom;

				e.Use();
			}
		}

		private Color GetLinkColor(in LinkType linkType)
		{
			switch (linkType)
			{
				case LinkType.Self:
					return Color.red;
				case LinkType.WeakIn:
					return kWeakInColor;
				case LinkType.WeakOut:
					return kWeakOutColor;
				case LinkType.DirectIn:
					return kDirectInColor;
				case LinkType.DirectOut:
					return kDirectOutColor;
			}

			return Color.red;
		}

		private void DrawEdge(in Rect viewportRect, in Edge edge, in Vector2 from, in Vector2 to)
		{
			if (edge.hidden)
				return;
			var edgeScale = to - from;
			var edgeBounds = new Rect(
				Mathf.Min(from.x, to.x) - pan.x, Mathf.Min(from.y, to.y) - pan.y,
				Mathf.Abs(edgeScale.x), Mathf.Abs(edgeScale.y));
			if (!edgeBounds.Overlaps(viewportRect))
				return;
			var edgeColor = GetLinkColor(edge.linkType);
			bool selected = selecteNode == edge.Source || selecteNode == edge.Target;
			if (selected)
			{
				const float kHightlightFactor = 1.65f;
				edgeColor.r = Math.Min(edgeColor.r * kHightlightFactor, 1.0f);
				edgeColor.g = Math.Min(edgeColor.g * kHightlightFactor, 1.0f);
				edgeColor.b = Math.Min(edgeColor.b * kHightlightFactor, 1.0f);
			}
			Handles.DrawBezier(from, to,
				new Vector2(edge.Source.rect.xMax + kHalfNodeSize, edge.Source.rect.center.y) + pan,
				new Vector2(edge.Target.rect.xMin - kHalfNodeSize, edge.Target.rect.center.y) + pan,
				edgeColor, null, 5f);
		}

		protected void DrawNode(Event evt, in Rect viewportRect, Node node)
		{
			var windowRect = new Rect(node.rect.position + pan, node.rect.size);
			if (!node.rect.Overlaps(viewportRect))
				return;

			node.rect = GUI.Window(node.index, windowRect, _ => DrawNodeWindow(windowRect, evt, node), node.title);

			if (node.rect.Contains(evt.mousePosition))
			{
				if (string.IsNullOrEmpty(status))
					searchView.Repaint();
				status = node.tooltip;
			}

			node.rect.x -= pan.x;
			node.rect.y -= pan.y;
		}

		private void DrawNodeWindow(in Rect windowRect, Event evt, in Node node)
		{
			bool handled = false;

			const float kPreviewOffsetY = 10.0f;
			if (evt.type == EventType.Repaint)
			{
				var previewOffset = (kNodeSize - kPreviewSize) / 2.0f;
				GUI.DrawTexture(new Rect(
						previewOffset, previewOffset + kPreviewOffsetY,
						kPreviewSize, kPreviewSize), node.preview ?? EditorGUIUtility.FindTexture(typeof(DefaultAsset)));
			}

			const float kHeightDiff = 2.0f;
			const float kButtonWidth = 16.0f, kButtonHeight = 18f;
			const float kRightPadding = 17.0f, kBottomPadding = kRightPadding - kHeightDiff;
			var buttonRect = new Rect(windowRect.width - kRightPadding, windowRect.height - kBottomPadding - 4f, kButtonWidth, kButtonHeight);
			if (!node.expanded && GUI.Button(buttonRect, "+"))
			{
				var expandedNodes = graph.ExpandNode(node);
				graphLayout.Calculate(graph, expandedNodes, 0.05f);
				handled = true;
			}

			buttonRect = new Rect(windowRect.width - kRightPadding, kBottomPadding, 23, 26);
			bool hasChanged = node.pinned;
			node.pinned = EditorGUI.Toggle(buttonRect, node.pinned);
			handled |= hasChanged != node.pinned;

			if (!handled && evt.type == EventType.MouseDown)
			{
				if (evt.button == 0)
				{
					var selectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.name);
					if (evt.clickCount == 1 && selectedObject)
					{
						selecteNode = node;
						EditorGUIUtility.PingObject(selectedObject.GetInstanceID());
					}
					else if (evt.clickCount == 2)
					{
						Selection.activeObject = selectedObject;
						//ShowDependencyViewer();
					}
				}
			}
			GUI.DragWindow();
		}

		private void DrawGraph(Event evt)
		{
			if (graphLayout.Animated)
			{
				if (graphLayout.Calculate(graph, null, 0.05f))
					searchView.Repaint();
			}

			var viewportRect = new Rect(-pan.x, -pan.y, rect.width, rect.height).ScaleSizeBy(1f / zoom, -pan);
			if (evt.type == EventType.Layout)
			{
				// Reset status message, it will be set again when hovering a node.
				status = "";
			}
			else if (evt.type == EventType.Repaint)
			{
				Handles.BeginGUI();
				foreach (var edge in graph.edges)
					DrawEdge(viewportRect, edge, edge.Source.rect.center + pan, edge.Target.rect.center + pan);
				Handles.EndGUI();
			}

			var searchWindow = (EditorWindow)searchView;
			searchWindow.BeginWindows();
			foreach (var node in graph.nodes)
				DrawNode(evt, viewportRect, node);
			searchWindow.EndWindows();
		}

		private void DrawView(Event evt)
		{
			EditorZoomArea.Begin(zoom, rect);
			DrawGraph(evt);
			EditorZoomArea.End();
		}

		private void DrawHUD(Event evt)
		{
			if (!string.IsNullOrEmpty(status))
				GUI.Label(new Rect(4, rect.yMax - 20, rect.width, 20), status);

			if (evt.type == EventType.MouseDown && evt.button == 1)
			{
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent("Relayout"), false, () => Relayout());
				menu.AddSeparator("");

				menu.AddItem(new GUIContent("Layout/Column"), false, () => SetLayout(CreateColumnLayout()));
				menu.AddItem(new GUIContent("Layout/Springs"), false, () => SetLayout(new ForceDirectedLayout(graph)));
				menu.AddItem(new GUIContent("Layout/Organic"), false, () => SetLayout(new OrganicLayout()));
				menu.ShowAsContext();
				evt.Use();
			}
		}

		private void Relayout()
		{
			foreach (var v in graph.nodes)
				v.pinned = false;
			nodeOfInterest.pinned = true;
			graphLayout.Calculate(graph, null, 0.05f);
		}

		void SetLayout(IGraphLayout layout)
		{
			graphLayout = layout;
			graphLayout.Calculate(graph, null, 0.05f);
			searchView.Repaint();
		}

		IGraphLayout CreateColumnLayout()
		{
			return new DependencyColumnLayout(nodeOfInterest,
					graph.GetDependencies(nodeOfInterest.id),
					graph.GetReferences(nodeOfInterest.id)
					);
		}
	}
}
#endif
