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
			var selectedInstanceIds = new List<int>();
			foreach (var obj in objects)
			{
				var instanceId = obj.GetInstanceID();
				var assetPath = AssetDatabase.GetAssetPath(instanceId);
				if (!string.IsNullOrEmpty(assetPath))
					selectedPaths.Add("\"" + assetPath + "\"");
				else
					selectedInstanceIds.Add(instanceId);
				globalObjectIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(instanceId).ToString());
			}

			var providers = new[] { "expression", "dep", "scene" };
			var selectedPathsStr = string.Join(",", selectedPaths);
			var fromQuery = $"from=[{selectedPathsStr}]";
			if (selectedInstanceIds.Count > 0)
			{
				var selectedInstanceIdsStr = string.Join(",", selectedInstanceIds);
				fromQuery = $"{{{fromQuery}, deps{{[{selectedInstanceIdsStr}]}}}}";
				selectedPathsStr = string.Join(",", selectedPaths.Concat(selectedInstanceIds.Select(e => e.ToString())));
			}
			return new DependencyViewerState("Selection", globalObjectIds, new[] {
				new DependencyState("Uses", SearchService.CreateContext(providers, fromQuery), CreateDefaultTable("Uses")),
				new DependencyState("Used By", SearchService.CreateContext(providers, $"ref=[{selectedPathsStr}]"), CreateDefaultTable("Used By (References)"))
			});
		}

		static IEnumerable<SearchColumn> GetDefaultColumns(string tableName)
		{
			var defaultDepFlags = SearchColumnFlags.CanSort;
			yield return new SearchColumn("Ref #", "refCount", null, defaultDepFlags | SearchColumnFlags.TextAlignmentRight) { width = 40 };
			yield return new SearchColumn(tableName, "label", "Name", null, defaultDepFlags);
			yield return new SearchColumn("Type", "type", null, defaultDepFlags | SearchColumnFlags.IgnoreSettings | SearchColumnFlags.Hidden) { width = 60 };
			yield return new SearchColumn("Size", "size", "size", null, defaultDepFlags);
		}

		public static SearchTable CreateDefaultTable(string tableName)
		{
			return new SearchTable(System.Guid.NewGuid().ToString("N"), tableName, GetDefaultColumns(tableName));
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
			var title = ObjectNames.NicifyVariableName(nameof(BrokenDependencies));
			return new DependencyViewerState(title, new [] {
				new DependencyState(title, SearchService.CreateContext("dep", "is:broken"), CreateDefaultTable(title))
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState MissingDependencies()
		{
			var title = ObjectNames.NicifyVariableName(nameof(MissingDependencies));
			return new DependencyViewerState(title, new [] {
				new DependencyState(title, SearchService.CreateContext("dep", "is:missing"), new SearchTable("MostUsed", "Name", new[] {
					new SearchColumn("GUID", "label", "selectable") { width = 390 }
				}))
			});
		}

		[DependencyViewerState]
		internal static DependencyViewerState MostUsedAssets()
		{
			var defaultDepFlags = SearchColumnFlags.CanSort | SearchColumnFlags.IgnoreSettings;
			var query = SearchService.CreateContext(new[] { "expression", "asset", "dep" }, "first{25,sort{select{p:a:assets, @path, count{dep:ref=\"@path\"}}, @value, desc}}");
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
