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
			return new DependencyViewerState("Broken dependencies", new [] {
				new DependencyState("Broken dependencies", SearchService.CreateContext("dep", "is:broken"))
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState MissingDependencies()
		{
			return new DependencyViewerState("Missing dependencies", new [] {
				new DependencyState("Missing dependencies", SearchService.CreateContext("dep", "is:missing"))
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState MostUsedAssets()
		{
			var defaultDepFlags = SearchColumnFlags.CanSort | SearchColumnFlags.IgnoreSettings;
			var query = SearchService.CreateContext(new[] { "expression", "asset", "dep" }, "first{25,sort{select{p:a:assets, @path, count{dep:to=\"@path\"}}, @value, desc}}");
			return new DependencyViewerState("Most Used Assets", new [] {
					new DependencyState("Most Used Assets", query, new SearchTable("MostUsed", "Name", new[] {
					new SearchColumn("Name", "label", "name", null, defaultDepFlags) { width = 390 },
					new SearchColumn("Count", "value", null, defaultDepFlags) { width = 80 }
				}))
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState UnusedAssets()
		{
			var query = SearchService.CreateContext(new[] { "dep" }, "dep:in=0 is:file is:valid -is:package");
			return new DependencyViewerState("Unused Assets", new [] {
				new DependencyState("Unused Assets", query, new SearchTable("Unused", "Name", new[] {
					new SearchColumn("Unused Assets", "label", "Name") { width = 380 },
					new SearchColumn("Type", "type") { width = 90 },
					new SearchColumn("Size", "size", "size")  { width = 80 }
				}))
			});
		}
	}
}
#endif
