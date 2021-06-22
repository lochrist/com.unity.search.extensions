#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	static class DependencyBuiltinStates
	{
		[DependencyViewerState]
		public static DependencyViewerState TrackSelection(DependencyViewerState previousState)
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

			var state = new DependencyViewerState(title, new[] {
				new DependencyState("Uses", SearchService.CreateContext(providers, $"from=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 1 ? previousState.states[0].tableConfig : null),
				new DependencyState("Used By", SearchService.CreateContext(providers, $"to=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 2 ? previousState.states[1].tableConfig : null)
			});
			if (singleAssetPath != "")
				state.icon = AssetDatabase.GetCachedIcon(singleAssetPath) as Texture2D;
			return state;
		}

		[DependencyViewerState]
		static DependencyViewerState GetBrokenDependencies(DependencyViewerState previousState)
		{
			var state = new DependencyViewerState("Broken dependencies");
			state.states.Add(new DependencyState("Broken dependencies", SearchService.CreateContext("dep", "is:broken")));
			return state;
		}

		[DependencyViewerState]
		static DependencyViewerState GetMissingDependencies(DependencyViewerState previousState)
		{
			var state = new DependencyViewerState("Missing dependencies");
			state.states.Add(new DependencyState("Missing dependencies", SearchService.CreateContext("dep", "is:missing")));
			return state;
		}
	}
}
#endif
