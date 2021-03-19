using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.Search.Providers;
using System.Collections.Generic;
using System.Collections;

static class SearchWalker
{
	[MenuItem("Search/Search And Update")]
	public static void ExecuteSearchUpdate()
	{
		// IMPORTANT: Make sure to have a proper index setup.

		ProgressUtility.RunTask("Search Update", "Running search queries to update your project...", RunSearchUpdate);
	}

	static IEnumerator RunSearchUpdate(int progressId, object userData)
	{
		Progress.Report(progressId, -1);

		// Find input materials
		var materialPaths = new HashSet<string>();
		yield return FindAssets(progressId, "*.mat", materialPaths);

		// For each material, find object references
		int processCount = 0;
		foreach (var matPath in materialPaths)
		{
			Debug.Log($"<color=#23E55A>Processing</color> {matPath}");
			Progress.Report(progressId, processCount++, materialPaths.Count, matPath);
			foreach (var obj in EnumerateObjects($"ref=\"{matPath}\""))
			{
				if (!obj)
				{
					yield return null;
					continue;
				}

				// TODO: Patch object
				Debug.Log($"Patching {obj}...");
				yield return null;
				yield return null;
				yield return null;
			}
		}
	}

	private static IEnumerator FindAssets(int progressId, string query, ICollection<string> filePaths)
	{
		using (var context = SearchService.CreateContext("find", query))
		using (var request = SearchService.Request(context))
		{
			foreach (var r in request)
			{
				if (r == null)
				{
					yield return null;
					continue;
				}

				var assetPath = AssetProvider.GetAssetPath(r);
				filePaths.Add(assetPath);
				Progress.Report(progressId, filePaths.Count, request.Count, assetPath);
			}
		}
	}

	private static IEnumerable<Object> EnumerateObjects(string query)
	{
		using (var context = SearchService.CreateContext("asset", query))
		using (var request = SearchService.Request(context))
		{
			foreach (var r in request)
			{
				if (r == null)
				{
					yield return null;
					continue;
				}

				if (!GlobalObjectId.TryParse(r.id, out var gid))
					continue;

				var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
				if (!obj)
				{
					// TODO: Open container scene

					// Reload object
					obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
					if (!obj)
					{
						Debug.Log($"<color=#E5455A>Failed to patch</color> {r.id}");
						continue;
					}
				}

				yield return obj;
			}
		}
	}
}
