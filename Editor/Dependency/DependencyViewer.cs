using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
	// Controller

	internal class DependencyViewer : EditorWindow
    {
        private const float kInitialPosOffset = 10500.0f;
        private const float kEditorWindowTabHeight = 21.0f;
        private const float kNodeSize = 100.0f;
        private const float kHalfNodeSize = kNodeSize / 2.0f;
        private const float kPreviewSize = 64.0f;

        private readonly Color kWeakInColor = new Color(240f / 255f, 240f / 255f, 240f / 255f);
        private readonly Color kWeakOutColor = new Color(120 / 255f, 134f / 255f, 150f / 255f);
        private readonly Color kDirectInColor = new Color(146 / 255f, 196 / 255f, 109 / 255f);
        private readonly Color kDirectOutColor = new Color(83 / 255f, 150 / 255f, 153 / 255f);

        private readonly System.Timers.Timer timer = new System.Timers.Timer(30);
        private readonly Rect screenRect = new Rect(0.0f, kEditorWindowTabHeight, Screen.width, Screen.height);
        private readonly Matrix4x4 translation = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, Vector3.one);

        private Node nodeOfInterest;
        private Graph graph;
        private DependencyDatabase db;
        private IGraphLayout graphLayout;
		[SerializeField] private UnityEngine.Object m_Selection;

        private Node selecteNode = null;
        private string status = "";
        private float zoom = 1.0f;
        private Rect pan = new Rect(-kInitialPosOffset, -kInitialPosOffset, 1000000, 1000000);

        [MenuItem("Assets/Dependencies #&D")]
        internal static void ShowDependencyViewer()
        {
            if (Selection.activeObject == null)
                return;

            var view = CreateWindow<DependencyViewer>();
			view.Show();
        }

        internal void OnEnable()
        {
			m_Selection = m_Selection ?? Selection.activeObject;

            if (db == null)
                db = DependencyManager.Scan();

            var selectedAssetPath = AssetDatabase.GetAssetPath(m_Selection);
            int selectedResourceID = db.FindResourceByName(selectedAssetPath);
            if (selectedResourceID < 0)
                return;

            titleContent.image = db.GetResourcePreview(selectedResourceID);
            titleContent.text = Path.GetFileNameWithoutExtension(selectedAssetPath);
            titleContent.tooltip = selectedAssetPath;

            graph = new Graph(db)
            {
                nodeInitialPositionCallback = GetNodeInitialPosition
            };
            var cx = -pan.x + position.width / 2;
            var cy = -pan.y + position.height / 2;
            nodeOfInterest = graph.BuildGraph(selectedResourceID, new Vector2(cx, cy));
            pan.x = Math.Min(0, -nodeOfInterest.rect.x + position.width / 2);
            pan.y = Math.Min(0, -nodeOfInterest.rect.y + position.height / 2);

            graphLayout = new ForceDirectedLayout(graph);

            timer.Interval = 30;
            timer.Elapsed += (sender, args) => Repaint();
            timer.Start();
        }

        private Rect GetNodeInitialPosition(Graph graphModel, Vector2 offset)
        {
            return new Rect(
                offset.x + Random.Range(-position.width / 2, position.width / 2),
                offset.y + Random.Range(-position.height / 2, position.height / 2) + kEditorWindowTabHeight,
                kNodeSize, kNodeSize);
        }

        private void HandleEvent(Event e)
        {
            if (e.type == EventType.MouseDrag)
            {
                pan.x = Math.Min(0, pan.x + e.delta.x / zoom);
                pan.y = Math.Min(0, pan.y + e.delta.y / zoom);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                var zoomDelta = 0.1f;
                float delta = e.delta.x + e.delta.y;
                zoomDelta = delta < 0 ? zoomDelta : -zoomDelta;
                zoom = Mathf.Clamp(zoom + zoomDelta, 0.5f, 2.25f);
                e.Use();
            }
        }

        private Color GetLinkColor(LinkType linkType)
        {
            switch (linkType)
            {
                case LinkType.Self: return Color.red;
                case LinkType.WeakIn: return kWeakInColor;
                case LinkType.WeakOut: return kWeakOutColor;
                case LinkType.DirectIn: return kDirectInColor;
                case LinkType.DirectOut: return kDirectOutColor;
            }

            return Color.red;
        }

        private void DrawEdge(Edge edge, Vector2 from, Vector2 to)
        {
            if (edge.hidden)
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
                new Vector2(edge.Source.rect.xMax + kHalfNodeSize, edge.Source.rect.center.y),
                new Vector2(edge.Target.rect.xMin - kHalfNodeSize, edge.Target.rect.center.y),
                edgeColor, null, 5f);
        }

        protected void DrawNode(Node node, Vector2 iPosition)
        {
            node.rect = GUI.Window(node.index, node.rect, nodeId =>
                {
                    bool handled = false;
                    var windowRect = node.rect;
                    const float kPreviewOffsetY = 10.0f;
                    var previewOffset = (kNodeSize - kPreviewSize) / 2.0f;
                    GUI.DrawTexture(new Rect(
                            previewOffset, previewOffset + kPreviewOffsetY,
                            kPreviewSize, kPreviewSize), node.preview ?? EditorGUIUtility.FindTexture(typeof(DefaultAsset)));

                    const float kHeightDiff = 4.0f;
                    const float kButtonWidth = 16.0f, kButtonHeight = kButtonWidth - kHeightDiff;
                    const float kRightPadding = 20.0f, kBottomPadding = kRightPadding - kHeightDiff;
                    var buttonRect = new Rect(windowRect.width - kRightPadding, windowRect.height - kBottomPadding, kButtonWidth, kButtonHeight);
                    if (!node.expanded)
                    {
                        if (GUI.Button(buttonRect, "+"))
                        {
                            graph.ExpandNode(node);
                            handled = true;
                        }
                    }

                    buttonRect = new Rect(windowRect.width - kRightPadding, kBottomPadding, 20, 20);
                    bool hasChanged = node.pinned;
                    node.pinned = EditorGUI.Toggle(buttonRect, node.pinned);
                    handled |= hasChanged != node.pinned;

                    if (!handled && Event.current.type == EventType.MouseDown)
                    {
                        if (Event.current.button == 0)
                        {
                            var selectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.name);
                            if (Event.current.clickCount == 1)
                            {
                                selecteNode = node;
                                EditorGUIUtility.PingObject(selectedObject.GetInstanceID());
                            }
                            else if (Event.current.clickCount == 2)
                            {
                                Selection.activeObject = selectedObject;
                                ShowDependencyViewer();
                            }
                        }
                    }
                    GUI.DragWindow();
                }, node.title);

            node.rect.x = Math.Max(0, node.rect.x);
            node.rect.y = Math.Max(0, node.rect.y);

            if (node.rect.Contains(Event.current.mousePosition))
            {
                if (String.IsNullOrEmpty(status))
                    Repaint();
                status = node.tooltip;
            }
        }

        private void DrawGraph()
        {
            if (graphLayout.Animated)
            {
                const float kTimeStep = 0.05f;
                graphLayout.Calculate(graph, kTimeStep);
            }

            // Reset status message, it will be set again when hovering a node.
            status = "";

            Handles.BeginGUI();
            graph.edges.ForEach(edge => DrawEdge(edge, edge.Source.rect.center, edge.Target.rect.center));
            Handles.EndGUI();

            BeginWindows();
            graph.nodes.ForEach(node => DrawNode(node, node.rect.center));
            EndWindows();
        }

        private void DrawView()
        {
            // Setup view
            Rect clippedArea = pan.ScaleSizeBy(1.0f / zoom, new Vector2(pan.x, pan.y + kEditorWindowTabHeight));
            GUI.BeginGroup(clippedArea);
            var originalGUIMatrix = GUI.matrix;
            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoom, zoom, zoom));
            GUI.matrix = translation * scale * translation.inverse;

            // Render view
            DrawGraph();

            // Restore view
            GUI.matrix = originalGUIMatrix;
            GUI.EndGroup();
        }

        private void DrawHUD()
        {
            if (!String.IsNullOrEmpty(status))
                GUI.Label(new Rect(10, position.height - 10, position.width, 20), status);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Layout/Relayout"), false, () =>
                    {
                        graphLayout.Calculate(graph, 0.05f);
                    });
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Layout/Column"), false, () =>
                    {
                        graphLayout = CreateColumnLayout();
                    });
                menu.AddItem(new GUIContent("Layout/Springs"), false, () =>
                    {
                        graphLayout = new ForceDirectedLayout(graph);
                        timer.Start();
                    });
                timer.Stop();
                menu.AddItem(new GUIContent("Layout/Organic"), false, () =>
                    {
                        SetLayout(new OrganicLayout());
                    });

                menu.ShowAsContext();
            }
        }

        internal void OnGUI()
        {
            if (db == null || graph == null || graph.nodes == null || graph.nodes.Count == 0)
                return;

            // Clear internal group, we will set our own.
            GUI.EndGroup();

            DrawView();
            DrawHUD();
            HandleEvent(Event.current);

            // Restore internal group
            GUI.BeginGroup(screenRect);
        }

        void SetLayout(IGraphLayout layout)
        {
            graphLayout = layout;
            graphLayout.Calculate(graph, 0.05f);
            if (layout.Animated)
                timer.Start();
            else
                timer.Stop();
        }

        IGraphLayout CreateColumnLayout()
        {
            var layout = new DependencyColumnLayout(nodeOfInterest,
                    graph.GetDependencies(nodeOfInterest.id),
                    graph.GetReferences(nodeOfInterest.id)
                    );
            layout.Calculate(graph, 0.05f);
            return layout;
        }
    }
}
