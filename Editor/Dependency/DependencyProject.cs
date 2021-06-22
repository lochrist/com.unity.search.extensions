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
			{
				return;
			}

			if (miniLabelAlignRight == null)
				miniLabelAlignRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

			var r = new Rect(rect.x, rect.y, 1f, 16f);
			var content = new GUIContent(count.ToString());
			r.width = 16f;
			r.x -= 10f;
			GUI.Label(r, content, EditorStyles.label);
		}
	}
}
