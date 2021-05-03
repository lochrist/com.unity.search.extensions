#if UNITY_2021_2_OR_NEWER
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using System.Collections.Generic;

static class CreateIndex
{
	[MenuItem("Search/Create Index")]
	public static void Example()
	{
		void OnSearchIndexCreated(string name, string path, IEnumerable<SearchItem> items, Action finished)
		{
			var siObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
			Debug.Log($"Search index {name} is ready to be used", siObj);

			// TODO: Run your query on the temporary index
			var searchQuery = $"p: a={name} t:Material";
			Debug.Log($"Listing materials using <b>{searchQuery}</b>");
			SearchService.Request(searchQuery, (context, materials) =>
			{
				// Process materials...
				foreach (var m in materials)
					Debug.Log(m.GetDescription(context));
			}, _ =>
			{
				// IMPORTANT: Notify the system that the search index can be disposed by calling finished()
				EditorApplication.CallDelayed(() =>
				{
					Debug.Log($"Project upgrade finished");
					finished();
				}, 3d);
			});
		}

		CreateSearchIndex(Guid.NewGuid().ToString("N"), OnSearchIndexCreated);
	}

	/// <summary>
	/// Handler called when the temporary search index is created and ready to be used.
	/// </summary>
	/// <param name="name">Name of the search index</param>
	/// <param name="path">Asset path of the temporary index</param>
	/// <param name="items">Search results of the temporary index.</param>
	/// <param name="finished">Callback to be invoked when you are done using the temporary index.</param>
	public delegate void SearchIndexCreatedHandler(string name, string path, IEnumerable<SearchItem> items, Action finished);

	/// <summary>
	/// Create a search index. When the search index is ready to be used, callback the user code.
	/// IMPORTANT: The user code must call finished() when the search index can be disposed.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="onIndexReady"></param>
	public static void CreateSearchIndex(string name, SearchIndexCreatedHandler onIndexReady)
	{
		// Create <guid>.index in the project
		var title = $"Building {name} search index";
		EditorUtility.DisplayProgressBar(title, "Creating search index...", -1f);

		// Write search index manifest
		var indexPath = AssetDatabase.GetUniquePathNameAtSelectedPath($"{name}.index");
		System.IO.File.WriteAllText(indexPath,
			@"{
				""roots"": [""Assets""],
				""includes"": [],
				""excludes"": [],
				""options"": {
					""types"": true,
					""properties"": true,
					""extended"": true,
					""dependencies"": true
				},
				""baseScore"": 9999
			}");

		// Import the search index
		AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.DontDownloadFromCacheServer);
		EditorApplication.delayCall += () =>
		{
			// Wait for the index to be finished
			var context = SearchService.CreateContext("asset", $"p: a=\"{name}\"");
			SearchService.Request(context, (_, items) =>
			{
				// Raise onIndexReady callback
				onIndexReady?.Invoke(name, indexPath, items, () =>
				{
					context?.Dispose();
					context = null;

					// Client code has finished with the created index. We can delete it.
					AssetDatabase.DeleteAsset(indexPath);
					EditorUtility.ClearProgressBar();
				});
			});
		};
	}
}
#endif
