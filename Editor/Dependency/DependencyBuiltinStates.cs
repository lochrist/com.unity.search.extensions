#if USE_DEPENDENCY_PROVIDER
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.Search
{
	static class DependencyBuiltinStates
	{
		public static DependencyViewerState StateFromObjects(IEnumerable<UnityEngine.Object> objects)
		{
			if (!objects.Any())
				return new DependencyViewerState("No objects");

			var globalObjectIds = new List<string>();
			var selectedPaths = new List<string>();
			foreach (var obj in objects)
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
				new DependencyState("Uses", SearchService.CreateContext(providers, $"from=[{selectedPathsStr}]")),
				new DependencyState("Used By", SearchService.CreateContext(providers, $"to=[{selectedPathsStr}]"))
			});
		}

		[DependencyViewerState]
		public static DependencyViewerState TrackSelection()
		{
			if (Selection.objects.Length == 0)
				return new DependencyViewerState("No Selection");

			return StateFromObjects(Selection.objects);
		}

		[DependencyViewerState]
		internal static DependencyViewerState BrokenDependencies()
		{
			var state = new DependencyViewerState("Broken dependencies");
			state.states.Add(new DependencyState("Broken dependencies", SearchService.CreateContext("dep", "is:broken")));
			return state;
		}

		[DependencyViewerState]
		internal static DependencyViewerState MissingDependencies()
		{
			var state = new DependencyViewerState("Missing dependencies");
			state.states.Add(new DependencyState("Missing dependencies", SearchService.CreateContext("dep", "is:missing")));
			return state;
		}

		[DependencyViewerState]
		static DependencyViewerState MostUsedAssets()
		{
			var state = new DependencyViewerState("Most Used Assets");

			var defaultDepFlags = SearchColumnFlags.CanSort;
			var tableConfig = new SearchTable(System.Guid.NewGuid().ToString("N"), "Name", new[] {
				new SearchColumn("Name", "label", "name", null, defaultDepFlags),
				new SearchColumn("Count", "value", null, defaultDepFlags)
			});
			var tableState = new DependencyState("Most Used Assets", SearchService.CreateContext(new[] { "expression", "asset", "dep" }, "first{25,sort{select{p:a:assets, @path, count{dep:to=\"@path\"}}, @value, desc}}"), tableConfig);
			state.states.Add(tableState);
			return state;
		}

		[DependencyViewerState]
		static DependencyViewerState UnusedAssets()
		{
			var state = new DependencyViewerState("Unused Assets");
			var tableState = new DependencyState("Unused Assets", SearchService.CreateContext(new[] { "dep" }, "dep:in=0 is:file is:valid -is:package"));
			state.states.Add(tableState);
			return state;
		}
	}
}
#endif
