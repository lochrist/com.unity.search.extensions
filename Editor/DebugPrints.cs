using UnityEngine;
using UnityEditor;
using UnityEditor.Search;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
static class DebugPrints
{
	static long before;

	static DebugPrints()
	{
		EditorApplication.delayCall += () => before = System.GC.GetTotalMemory(false);
	}

	[MenuItem("Test/Print Stats")] // REMOVE ME
	internal static void PrintInfo()
	{
		SearchMonitor.PrintInfo();

		long after = System.GC.GetTotalMemory(false);
		long diff = after - before;
		Debug.Log("allocated bytes=" + diff.ToString());
	}

    [MenuItem("Test/Request Text Async")]
	public static void RequestTextAsync()
	{
		var batchCount = 0;
		var totalItemCount = 0;
		SearchService.Request("ref:rock t:mesh", (SearchContext context, IEnumerable<SearchItem> items) =>
		{
			var batchItemCount = items.Count();
			totalItemCount += batchItemCount;
			Debug.Log($"#{++batchCount} Incoming items ({batchItemCount}): {string.Join(",", items.Select(e => e.id))}");
		}, (SearchContext context) =>
		{
			Debug.Log($"Query <b>\"{context.searchText}\"</b> completed with a total of {totalItemCount} items");
		}, SearchFlags.Debug);
	}

	[MenuItem("Test/Request Context Async")]
	public static void RequestContextAsync()
	{
		var totalItemCount = 0;
		var searchContext = SearchService.CreateContext("scene", "ref:rock t:mesh");
		SearchService.Request(searchContext, (SearchContext context, IEnumerable<SearchItem> items) =>
		{
			totalItemCount += items.Count();
		}, (SearchContext context) =>
		{
			Debug.Log($"Query <b>\"{context.searchText}\"</b> completed with a total of {totalItemCount} items");
			searchContext?.Dispose();
			searchContext = null;
		}, SearchFlags.Debug);
	}
}
