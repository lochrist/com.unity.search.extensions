using UnityEngine;

namespace UnityEditor.Search
{
	[InitializeOnLoad]
	static class DependencyProject
	{
		static GUIStyle miniLabelAlignRight = null;

		static DependencyProject()
		{
			EditorApplication.projectWindowItemOnGUI -= DrawDependencies;
			EditorApplication.projectWindowItemOnGUI += DrawDependencies;
		}

		static void DrawDependencies(string guid, Rect rect)
		{
			var count = Dependency.GetReferenceCount(guid);
			if (count == -1)
				return;

			if (miniLabelAlignRight == null)
				miniLabelAlignRight = CreateLabelStyle();

			var r = new Rect(rect.x - 14f, rect.y, 16f, rect.height);
			GUI.Label(r, Utils.FormatCount((ulong)count), miniLabelAlignRight);
		}

		static GUIStyle CreateLabelStyle()
		{
			return new GUIStyle(EditorStyles.miniLabel)
			{
				fontSize = EditorStyles.miniLabel.fontSize - 1,
				alignment = TextAnchor.MiddleRight,
				padding = new RectOffset(0, 0, 0, 0)
			};
		}
	}
}
