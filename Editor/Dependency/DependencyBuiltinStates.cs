#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
	[Flags]
	enum DependencyViewerFlags
	{
		None = 0,
		Uses = 1 << 1,
		UsedBy = 1 << 2,
		TrackSelection = 1 << 3,
		All = Uses | UsedBy
	}

	static class DependencyBuiltinStates
	{
		static List<string> emptySelection = new List<string>();

		private static DependencyViewerState StateFromObjects(string stateName, IEnumerable<UnityEngine.Object> objects, DependencyViewerFlags depType)
		{
			if (!objects.Any())
				return new DependencyViewerState(stateName, emptySelection);

			var globalObjectIds = new List<string>();
			var selectedPaths = new List<string>();
			var selectedInstanceIds = new List<int>();
			foreach (var obj in objects)
			{
				var instanceId = obj.GetInstanceID();
				var assetPath = AssetDatabase.GetAssetPath(instanceId);
				if (!string.IsNullOrEmpty(assetPath))
				{
					if (System.IO.Directory.Exists(assetPath))
						continue;
					selectedPaths.Add("\"" + assetPath + "\"");
				}
				else
					selectedInstanceIds.Add(instanceId);
				globalObjectIds.Add(GlobalObjectId.GetGlobalObjectIdSlow(instanceId).ToString());
			}

			if (globalObjectIds.Count == 0)
				return null;

			var providers = new[] { "expression", "dep", "scene" };
			var selectedPathsStr = string.Join(",", selectedPaths);
			var fromQuery = $"from=[{selectedPathsStr}]";
			if (selectedInstanceIds.Count > 0)
			{
				var selectedInstanceIdsStr = string.Join(",", selectedInstanceIds);
				fromQuery = $"{{{fromQuery}, deps{{[{selectedInstanceIdsStr}]}}}}";
				selectedPathsStr = string.Join(",", selectedPaths.Concat(selectedInstanceIds.Select(e => e.ToString())));
			}
			var state = new DependencyViewerState(stateName, globalObjectIds) { flags = depType | DependencyViewerFlags.TrackSelection };
			if (depType.HasFlag(DependencyViewerFlags.Uses))
				state.states.Add(new DependencyState("Uses", SearchService.CreateContext(providers, fromQuery), CreateDefaultTable("Uses")));
			if (depType.HasFlag(DependencyViewerFlags.UsedBy))
				state.states.Add(new DependencyState("Used By", SearchService.CreateContext(providers, $"ref=[{selectedPathsStr}]"), CreateDefaultTable("Used By (References)")));
			return state;
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

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection)]
		public static DependencyViewerState SelectionDependencies()
		{
			var depState = StateFromObjects(ObjectNames.NicifyVariableName(nameof(SelectionDependencies)), Selection.objects, DependencyViewerFlags.All);
			return depState;
		}

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection)]
		public static DependencyViewerState SelectionUses()
		{
			var depState = StateFromObjects(ObjectNames.NicifyVariableName(nameof(SelectionUses)), Selection.objects, DependencyViewerFlags.Uses);
			return depState;
		}

		[DependencyViewerProvider(DependencyViewerFlags.TrackSelection)]
		public static DependencyViewerState SelectionUsedBy()
		{
			var depState = StateFromObjects(ObjectNames.NicifyVariableName(nameof(SelectionUsedBy)), Selection.objects, DependencyViewerFlags.UsedBy);
			return depState;
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState BrokenDependencies()
		{
			var title = ObjectNames.NicifyVariableName(nameof(BrokenDependencies));
			return new DependencyViewerState(
				title,
				new DependencyState(title, SearchService.CreateContext("dep", "is:broken"), CreateDefaultTable(title))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState MissingDependencies()
		{
			var title = ObjectNames.NicifyVariableName(nameof(MissingDependencies));
			return new DependencyViewerState(
				title,
				new DependencyState(title, SearchService.CreateContext("dep", "is:missing"), new SearchTable("MostUsed", "Name", new[] {
					new SearchColumn("GUID", "label", "selectable") { width = 390 }
				}))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState MostUsedAssets()
		{
			var defaultDepFlags = SearchColumnFlags.CanSort | SearchColumnFlags.IgnoreSettings;
			var query = SearchService.CreateContext(new[] { "expression", "asset", "dep" }, "first{25,sort{select{p:a:assets, @path, count{dep:ref=\"@path\"}}, @value, desc}}");
			var title = ObjectNames.NicifyVariableName(nameof(MostUsedAssets));
			return new DependencyViewerState(
				title,
				new DependencyState(title, query, new SearchTable(title, "Name", new[] {
					new SearchColumn("Name", "label", "name", null, defaultDepFlags) { width = 390 },
					new SearchColumn("Count", "value", null, defaultDepFlags) { width = 80 }
				}))
			);
		}

		[DependencyViewerProvider]
		internal static DependencyViewerState UnusedAssets()
		{
			var query = SearchService.CreateContext(new[] { "dep" }, "dep:in=0 is:file is:valid -is:package");
			var title = ObjectNames.NicifyVariableName(nameof(UnusedAssets));
			return new DependencyViewerState(
				title,
				new DependencyState(title, query, new SearchTable(title, "Name", new[] {
					new SearchColumn(title, "label", "Name") { width = 380 },
					new SearchColumn("Type", "type") { width = 90 },
					new SearchColumn("Size", "size", "size")  { width = 80 }
				}))
			);
		}

		internal static DependencyViewerState ObjectDependencies(UnityEngine.Object obj)
		{
			var state = StateFromObjects(ObjectNames.NicifyVariableName(nameof(ObjectDependencies)), new[] { obj }, DependencyViewerFlags.All);
			state.name = ObjectNames.NicifyVariableName(nameof(ObjectDependencies));
			return state;
		}

		internal static DependencyViewerState CreateStateFromQuery(SearchQueryAsset sqa)
		{
			return new DependencyViewerState(sqa.name, new[] { new DependencyState(sqa) })
			{
				description = new GUIContent(sqa.searchText)
			};
		}
	}
}
#endif
