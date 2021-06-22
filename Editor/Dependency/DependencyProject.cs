using UnityEditor;
using UnityEngine;

namespace UnityEditor.Search
{
	[InitializeOnLoad]
	static class DependencyProject
	{
		static DependencyProject()
		{
			EditorApplication.projectWindowItemOnGUI += DrawDependencies;
		}


		private static void DrawDependencies(string guid, Rect rect)
		{
			// DependencyColumnLayout.


			/*
			var r = new Rect(rect.x, rect.y, 1f, 16f);
			if (scenes.Contains(guid))
			{
				EditorGUI.DrawRect(r, GUI2.Theme(new Color32(72, 150, 191, 255), Color.blue));
			}
			else if (guidsIgnore.Contains(guid))
			{
				var ignoreRect = new Rect(rect.x + 3f, rect.y + 6f, 2f, 2f);
				EditorGUI.DrawRect(ignoreRect, GUI2.darkRed);
			}

			if (!FR2_Cache.isReady)
			{
				return; // not ready
			}

			if (!FR2_Setting.ShowReferenceCount)
			{
				return;
			}

			FR2_Cache api = FR2_Cache.Api;
			if (FR2_Cache.Api.AssetMap == null)
			{
				FR2_Cache.Api.Check4Changes(false);
			}

			FR2_Asset item;

			if (!api.AssetMap.TryGetValue(guid, out item))
			{
				return;
			}

			if (item == null || item.UsedByMap == null)
			{
				return;
			}

			if (item.UsedByMap.Count > 0)
			{
				var content = new GUIContent(item.UsedByMap.Count.ToString());
				r.width = 0f;
				r.xMin -= 100f;
				GUI.Label(r, content, GUI2.miniLabelAlignRight);
			}
			*/
		}
	}
}
