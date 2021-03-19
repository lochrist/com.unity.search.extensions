using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.Search.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

static class SearchWalker
{
	[MenuItem("Search/Search And Update")]
	public static void ExecuteSearchUpdate()
	{
		ProgressUtility.RunTask("Search Update", "Running search queries to update your project...", RunSearchUpdate, Progress.Options.Indefinite);
	}

	static IEnumerator RunSearchUpdate(int progressId, object userData)
	{
		// Create an index to index all assets
		var settings = new SearchDatabase.Settings
		{
			type = "asset",
			guid = Guid.NewGuid().ToString("N"),
			name = Guid.NewGuid().ToString("N"),
			roots = new[] { "Assets" },
			excludes = new string[0],
			includes = new string[0],
			options = new SearchDatabase.Options()
			{
				types = true,
				dependencies = true,
				properties = true,
				extended = true
			},
		};

		var indexImporterType = SearchIndexEntryImporter.GetIndexImporterType(settings.type, settings.options.GetHashCode());
		AssetDatabaseAPI.RegisterCustomDependency(indexImporterType.GUID.ToString("N"), Hash128.Parse(settings.name));

		var db = ScriptableObject.CreateInstance<SearchDatabase>();
		db.Reload(settings);
		while(!db.loaded)
			yield return null;

// 		// Find input materials
// 		var materialPaths = new HashSet<string>();
// 		yield return FindAssets(progressId, "*.mat", materialPaths);

		UnityEngine.Object.DestroyImmediate(db);
	}

	private static IEnumerator FindAssets(int progressId, string query, ICollection<string> filePaths)
	{
		using (var findAssetSearchContext = SearchService.CreateContext("find", query))
		using (var findAssetSearchRequest = SearchService.Request(findAssetSearchContext))
		{
			foreach (var r in findAssetSearchRequest)
			{
				if (r == null)
				{
					yield return null;
					continue;
				}

				var assetPath = AssetProvider.GetAssetPath(r);
				filePaths.Add(assetPath);
				Progress.Report(progressId, filePaths.Count, findAssetSearchRequest.Count, assetPath);
			}
		}
	}
}
