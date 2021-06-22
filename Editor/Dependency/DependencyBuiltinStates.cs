#if USE_DEPENDENCY_PROVIDER
using System.Collections.Generic;

namespace UnityEditor.Search
{
	static class DependencyBuiltinStates
	{
		[DependencyViewerState]
		public static DependencyViewerState TrackSelection(DependencyViewerState previousState)
		{
			if (Selection.objects.Length == 0)
				return new DependencyViewerState("No Selection");

			var globalObjectIds = new List<string>();
			var selectedPaths = new List<string>();
			foreach (var obj in Selection.objects)
			{
				var instanceId = obj.GetInstanceID();
				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(assetPath))
					selectedPaths.Add("\"" + assetPath + "\"");
				else
					selectedPaths.Add(instanceId.ToString());
				globalObjectIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(instanceId).ToString());
			}

			var providers = new[] { "expression", "dep" };
			var selectedPathsStr = string.Join(",", selectedPaths);
			return new DependencyViewerState("Selection", globalObjectIds, new[] {
				new DependencyState("Uses", SearchService.CreateContext(providers, $"from=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 1 ? previousState.states[0].tableConfig : null),
				new DependencyState("Used By", SearchService.CreateContext(providers, $"to=[{selectedPathsStr}]"),
					previousState != null && previousState.states.Count >= 2 ? previousState.states[1].tableConfig : null)
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState BrokenDependencies(DependencyViewerState previousState)
		{
			var state = new DependencyViewerState("Broken dependencies");
			state.states.Add(new DependencyState("Broken dependencies", SearchService.CreateContext("dep", "is:broken")));
			return state;
		}

		[DependencyViewerState]
		internal static DependencyViewerState MissingDependencies(DependencyViewerState previousState)
		{
			var state = new DependencyViewerState("Missing dependencies");
			state.states.Add(new DependencyState("Missing dependencies", SearchService.CreateContext("dep", "is:missing")));
			return state;
		}
	}
}
#endif
