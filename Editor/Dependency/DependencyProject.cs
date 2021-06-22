using UnityEditor;
using UnityEngine;

namespace UnityEditor.Search
{
	[InitializeOnLoad]
	static class DependencyProject
	{
		static DependencyProject()
		{
			EditorApplication.projectWindowItemOnGUI -= DrawDependencies;
			EditorApplication.projectWindowItemOnGUI += DrawDependencies;
		}

		static GUIStyle miniLabelAlignRight = null;

		private static void DrawDependencies(string guid, Rect rect)
		{
			var count = Dependency.GetUseByCount(guid);
			if (count == -1)
				return;

			if (miniLabelAlignRight == null)
				miniLabelAlignRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 0, 0, 0) };

			var r = new Rect(rect.x - 14f, rect.y, 16f, rect.height);
			GUI.Label(r, Utils.FormatCount((ulong)count), miniLabelAlignRight);
		}
	}
}
