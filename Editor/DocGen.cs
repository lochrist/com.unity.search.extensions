#if DEPENDS_ON_INTERNAL_APIS
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;

public static class DocGen
{
	struct QueryDescriptor
	{
		public string category;
		public string filename;
		public string path;
		public SearchQueryAsset query;
	}

	[MenuItem("Window/Search/Tools/Print Query Doc Markdown", false, 100000)]
	static void PrintSearchQueryDoc()
	{
		var queries = SearchQueryAsset.savedQueries;
		var queryCategories = ExtractCategories(queries);
		var report = GenerateMarkdown(queryCategories);
		Debug.Log(report);
	}

	[MenuItem("Window/Search/Tools/Write Query Doc Markdown", false, 100000)]
	static void GenerateSearchQueryDoc()
	{
		var queries = SearchQueryAsset.savedQueries;
		var queryCategories = ExtractCategories(queries);
		var report = GenerateMarkdown(queryCategories);
		File.WriteAllText("Packages/com.unity.search.extensions/Documentation~/queries.md", report);
	}

	static Dictionary<string, List<QueryDescriptor>> ExtractCategories(IEnumerable<SearchQueryAsset> queries)
	{
		var queryCategories = new Dictionary<string, List<QueryDescriptor>>();
		foreach (var query in queries)
		{
			var path = AssetDatabase.GetAssetPath(query);
			if (!path.StartsWith("Packages/com.unity.search.extensions/"))
				continue;

			var folder = Path.GetDirectoryName(path);
			var category = Path.GetFileName(folder);
			var desc = new QueryDescriptor()
			{
				path = path,
				filename = Path.GetFileNameWithoutExtension(path),
				category = category,
				query = query
			};
			if (!queryCategories.TryGetValue(category, out var qs))
			{
				qs = new List<QueryDescriptor>();
				queryCategories.Add(category, qs);
			}
			qs.Add(desc);
		}
		return queryCategories;
	}

	static string GenerateMarkdown(Dictionary<string, List<QueryDescriptor>> queryCategories)
	{
		var str = new StringBuilder();
		foreach (var kvp in queryCategories)
		{
			var category = kvp.Key;
			str.AppendLine($"## {category}");
			foreach (var desc in kvp.Value)
			{
				str.AppendLine($"### {desc.filename}");
				str.AppendLine($"`{desc.query.text}`");
				if (!string.IsNullOrEmpty(desc.query.description))
				{
					str.AppendLine("");
					str.AppendLine($"\n{desc.query.description}");
				}

				if (desc.query.viewState != null &&
					desc.query.viewState.tableConfig != null &&
					desc.query.viewState.tableConfig.columns != null &&
					desc.query.viewState.tableConfig.columns.Length > 0
				)
				{
					str.AppendLine("");
					str.Append("**Search Table:** ");
					foreach (var column in desc.query.viewState.tableConfig.columns)
					{
						str.Append($"**{column.name}**");
						if (!string.IsNullOrEmpty(column.selector))
						{
							str.Append($" (`{column.selector}`)");
						}
						if (column != desc.query.viewState.tableConfig.columns.Last())
							str.Append($", ");
					}
				}
				str.AppendLine("");
				str.AppendLine("");
			}
		}

		return str.ToString();
	}
}
#endif
