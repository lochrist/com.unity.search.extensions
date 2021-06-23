using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor.Search;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Threading;

// Syntax:
// all           => Yield all results
// <guid>        => Yield asset with <guid>
// id:<guid>     => Yield asset with <guid>
// path:<path>   => Yield asset at <path>
// t:<extension> => Yield assets with <extension>
//
// is:file       => Yield file assets
// is:folder     => Yield folder assets
// is:package    => Yield package assets
// is:valid      => Yield assets which have no missing references
// is:broken     => Yield assets that have at least one broken reference.
// is:missing    => Yield GUIDs which are missing an valid asset (a GUID was found but no valid asset use that GUID)
//
// from:         => Yield assets which are used by asset with <guid>
// in=<count>    => Yield assets which are used <count> times
//
// to:<guid>     => Yield assets which are referencing the asset with <guid>
// out=<count>   => Yield assets which have <count> references to other assets
static class Dependency
{
	const string providerId = "dep";
	const string dependencyIndexLibraryPath = "Library/dependencies.index";
	static SearchIndexer index;

	readonly static Regex guidRx = new Regex(@"guid:\s+([a-z0-9]{32})");

	readonly static ConcurrentDictionary<string, string> guidToPathMap = new ConcurrentDictionary<string, string>();
	readonly static ConcurrentDictionary<string, string> pathToGuidMap = new ConcurrentDictionary<string, string>();
	readonly static ConcurrentDictionary<string, string> aliasesToPathMap = new ConcurrentDictionary<string, string>();
	readonly static ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> guidToRefsMap = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
	readonly static ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> guidFromRefsMap = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
	readonly static Dictionary<string, int> guidToDocMap = new Dictionary<string, int>();
	readonly static HashSet<string> ignoredGuids = new HashSet<string>();

	readonly static string[] builtinGuids = new string[]
	{
		"0000000000000000d000000000000000",
		"0000000000000000e000000000000000",
		"0000000000000000f000000000000000"
	};

	[SearchSelector("usedByCount", provider: providerId)]
	internal static object SelectUsedByCount(SearchItem item)
	{
		var count = GetUseByCount(item.id);
		if (count < 0)
			return null;
		return count;
	}

	[MenuItem("Window/Search/Rebuild dependency index", priority = 5677)]
	public static void Build()
	{
		pathToGuidMap.Clear();
		aliasesToPathMap.Clear();
		guidToPathMap.Clear();
		guidToRefsMap.Clear();
		guidFromRefsMap.Clear();
		guidToDocMap.Clear();
		ignoredGuids.Clear();

		var allGuids = AssetDatabase.FindAssets("a:all");
		ignoredGuids.UnionWith(AssetDatabase.FindAssets("l:Ignore"));
		foreach (var guid in allGuids.Concat(builtinGuids))
		{
			TrackGuid(guid);
			var assetPath = AssetDatabase.GUIDToAssetPath(guid);
			pathToGuidMap.TryAdd(assetPath, guid);
			guidToPathMap.TryAdd(guid, assetPath);
		}

		Task.Run(RunThreadIndexing);
	}

	[MenuItem("Window/Search/Dependencies", priority = 5678)]
	internal static void OpenDependencySearch()
	{
		SearchService.ShowContextual(providerId);
	}

	[MenuItem("Assets/(depends) Copy GUID")]
	internal static void CopyGUID()
	{
		var obj = Selection.activeObject;
		if (!obj)
			return;
		EditorGUIUtility.systemCopyBuffer = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
	}

	[MenuItem("Assets/(depends) Find Usings (from)")]
	internal static void FindUsings()
	{
		var obj = Selection.activeObject;
		if (!obj)
			return;
		var path = AssetDatabase.GetAssetPath(obj);
		var searchContext = SearchService.CreateContext(providerId, $"from=\"{path}\"");
		SearchService.ShowWindow(searchContext, "Dependencies (from)", saveFilters: false);
	}

	[MenuItem("Assets/(depends) Find Usages (to)")]
	internal static void FindUsages()
	{
		var obj = Selection.activeObject;
		if (!obj)
			return;
		var path = AssetDatabase.GetAssetPath(obj);
		var searchContext = SearchService.CreateContext(providerId, $"to=\"{path}\"");
		SearchService.ShowWindow(searchContext, "Dependencies (to)", saveFilters: false);
	}

	internal static int GetUseByCount(string id)
	{
		var recordKey = PropertyDatabase.CreateRecordKey(id, "assetUseByCount");
		using (var view = SearchMonitor.GetView())
		{
			if (view.TryLoadProperty(recordKey, out object data))
				return (int)data;
		}

		var path = AssetDatabase.GUIDToAssetPath(id);
		if (path == null || Directory.Exists(path))
			return -1;

		var searchContext = SearchService.CreateContext(providerId, $"to=\"{path}\"");
		SearchService.Request(searchContext, (context, items) =>
		{
			using (var view = SearchMonitor.GetView())
			{
				view.Invalidate(recordKey);
				view.StoreProperty(recordKey, items.Count);
			}
			context.Dispose();
		});
		return -1;
	}

	[MenuItem("Assets/(depends) Find References (ref)")]
	internal static void FindReferences()
	{
		var obj = Selection.activeObject;
		if (!obj)
			return;
		var path = AssetDatabase.GetAssetPath(obj);
		var searchContext = SearchService.CreateContext(new[] { "asset", "adb" }, $"ref=\"{path}\"");
		SearchService.ShowWindow(searchContext, "References", saveFilters: false);
	}

	static void Load(string indexPath)
	{
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		var indexBytes = File.ReadAllBytes(indexPath);
		index = new SearchIndexer()
		{
			resolveDocumentHandler = ResolveAssetPath
		};
		index.LoadBytes(indexBytes, (success) =>
		{
			if (!success)
				Debug.LogError($"Failed to load dependency index at {indexPath}");
			else
				Debug.Log($"Loading dependency index took {sw.Elapsed.TotalMilliseconds,3:0.##} ms ({EditorUtility.FormatBytes(indexBytes.Length)} bytes)");
		});
	}

	static void RunThreadIndexing()
	{
		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		index = new SearchIndexer();
		index.Start();
		int completed = 0;
		var metaFiles = Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories);
		var totalCount = metaFiles.Length;
		var progressId = Progress.Start($"Scanning dependencies ({metaFiles.Length} assets)");
		Parallel.ForEach(metaFiles, (mf) => ProcessAsset(mf, progressId, ref completed, totalCount));
		Progress.Finish(progressId, Progress.Status.Succeeded);

		var scriptPaths = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
		completed = 0;
		totalCount = scriptPaths.Length;
		progressId = Progress.Start($"Indexing weak dependencies");
		Parallel.ForEach(scriptPaths, (path, state, index) => ProcessScript(path.Replace("\\", "/"), progressId, ref completed, totalCount));
		Progress.Finish(progressId, Progress.Status.Succeeded);

		completed = 0;
		var total = pathToGuidMap.Count + guidToRefsMap.Count + guidFromRefsMap.Count;
		progressId = Progress.Start($"Indexing {total} dependencies");
		foreach (var kvp in pathToGuidMap)
		{
			var guid = kvp.Value;
			var path = kvp.Key;

			var ext = Path.GetExtension(path);
			if (ext.Length > 0 && ext[0] == '.')
				ext = ext.Substring(1);

			Progress.Report(progressId, completed++, total, path);

			var di = AddGuid(guid);

			index.AddExactWord("all", 0, di);
			AddStaticProperty("id", guid, di, exact: true);
			AddStaticProperty("path", path, di, exact: true);
			AddStaticProperty("t", GetExtension(path), di);
			if (Directory.Exists(path))
				AddStaticProperty("is", "folder", di);
			else
				AddStaticProperty("is", "file", di);
			if (path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
				AddStaticProperty("is", "package", di);
			index.AddWord(guid, guid.Length, 0, di);
			IndexWordComponents(di, path);
		}

		foreach (var kvp in guidToRefsMap)
		{
			var guid = kvp.Key;
			var refs = kvp.Value.Keys;
			var di = AddGuid(guid);
			index.AddWord(guid, guid.Length, 0, di);

			Progress.Report(progressId, completed++, total, guid);

			index.AddNumber("out", refs.Count, 0, di);
			foreach (var r in refs)
			{
				AddStaticProperty("to", r, di);
				if (guidToPathMap.TryGetValue(r, out var toPath))
					AddStaticProperty("to", toPath, di, exact: true);
			}
		}

		foreach (var kvp in guidFromRefsMap)
		{
			var guid = kvp.Key;
			var refs = kvp.Value.Keys;
			var di = AddGuid(guid);

			Progress.Report(progressId, completed++, total, guid);

			index.AddNumber("in", refs.Count, 0, di);
			foreach (var r in refs)
			{
				AddStaticProperty("from", r, di);
				if (guidToPathMap.TryGetValue(r, out var fromPath))
					AddStaticProperty("from", fromPath, di, exact: true);
			}

			if (guidToPathMap.TryGetValue(guid, out var path))
				AddStaticProperty("is", "valid", di);
			else
			{
				AddStaticProperty("is", "missing", di);

				foreach (var r in refs)
				{
					var refDocumentIndex = AddGuid(r);
					AddStaticProperty("is", "broken", refDocumentIndex);
					var refDoc = index.GetDocument(refDocumentIndex);
					var refMetaData = index.GetMetaInfo(refDoc.id);
					if (refMetaData == null)
						index.SetMetaInfo(refDoc.id, $"Broken links {guid}");
					else
						index.SetMetaInfo(refDoc.id, $"{refMetaData}, {guid}");
				}

				var refString = string.Join(", ", refs.Select(r =>
				{
					if (guidToPathMap.TryGetValue(r, out var rp))
						return rp;
					return r;
				}));
				index.SetMetaInfo(guid, $"Refered by {refString}");
			}
		}

		Progress.Report(progressId, -1f);
		Progress.SetDescription(progressId, $"Saving dependency index at {dependencyIndexLibraryPath}");

		index.Finish((bytes) =>
		{
			File.WriteAllBytes(dependencyIndexLibraryPath, bytes);
			Progress.Finish(progressId, Progress.Status.Succeeded);

			Debug.Log($"Dependency indexing took {sw.Elapsed.TotalMilliseconds,3:0.##} ms " +
				$"and was saved at {dependencyIndexLibraryPath} ({EditorUtility.FormatBytes(bytes.Length)} bytes)");
		}, removedDocuments: null);
	}

	private static void ProcessAsset(string mf, int progressId, ref int completed, int totalCount)
	{
		Interlocked.Increment(ref completed);
		var assetPath = mf.Replace("\\", "/").Substring(0, mf.Length - 5).ToLowerInvariant();
		if (!File.Exists(assetPath))
			return;

		var guid = ToGuid(assetPath);
		if (ignoredGuids.Contains(guid))
			return;

		Progress.Report(progressId, completed, totalCount, assetPath);

		TrackGuid(guid);
		pathToGuidMap.TryAdd(assetPath, guid);
		guidToPathMap.TryAdd(guid, assetPath);

		var dir = Path.GetDirectoryName(assetPath).ToLowerInvariant();
		var name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
		var ext = Path.GetExtension(assetPath).ToLowerInvariant();
		aliasesToPathMap.TryAdd(assetPath.ToLowerInvariant(), guid);
		aliasesToPathMap.TryAdd(name, guid);
		aliasesToPathMap.TryAdd(name+ext, guid);
		aliasesToPathMap.TryAdd(dir+"/"+name, guid);

		var mfc = File.ReadAllText(mf);
		ScanDependencies(guid, mfc);

		using (var file = new StreamReader(assetPath))
		{
			var header = new char[5];
			if (file.ReadBlock(header, 0, header.Length) == header.Length &&
				header[0] == '%' && header[1] == 'Y' && header[2] == 'A' && header[3] == 'M' && header[4] == 'L')
			{
				var ac = file.ReadToEnd();
				ScanDependencies(guid, ac);
			}
		}
	}

	private static void ProcessScript(string path, int progressId, ref int completed, int totalCount)
	{
		Interlocked.Increment(ref completed);
		Progress.Report(progressId, completed, totalCount, path);

		var scriptGuid = ToGuid(path);
		if (string.IsNullOrEmpty(scriptGuid))
			return;
		int lineIndex = 1;
		var re = new Regex(@"""[\w\/\-\s\.]+""");
		foreach (var line in File.ReadLines(path))
		{
			var matches = re.Matches(line);
			foreach (Match m in matches)
			{
				var parsedValue = m.Value.ToLowerInvariant().Trim('"');
				if (aliasesToPathMap.TryGetValue(parsedValue, out var guid) && !string.Equals(guid, scriptGuid))
				{
					guidToRefsMap[scriptGuid].TryAdd(guid, 1);
					guidFromRefsMap[guid].TryAdd(scriptGuid, 1);
				}

				if (guidToPathMap.TryGetValue(parsedValue.Replace("-", ""), out _))
				{
					guidToRefsMap[scriptGuid].TryAdd(parsedValue, 1);
					guidFromRefsMap[parsedValue].TryAdd(scriptGuid, 1);
				}
			}
			lineIndex++;
		}
	}

	static void IndexWordComponents(int documentIndex, string word)
	{
		foreach (var c in UnityEditor.Search.SearchUtils.SplitFileEntryComponents(word, UnityEditor.Search.SearchUtils.entrySeparators))
			index.AddWord(c.ToLowerInvariant(), 0, documentIndex);
	}

	static string GetExtension(string path)
	{
		var ext = Path.GetExtension(path);
		if (ext.Length > 0 && ext[0] == '.')
			ext = ext.Substring(1);
		return ext;
	}

	static void AddStaticProperty(string key, string value, int di, bool exact = false)
	{
		value = value.ToLowerInvariant();
		index.AddProperty(key, value, value.Length, value.Length, 0, di, false, exact);
	}

	static void ScanDependencies(string guid, string content)
	{
		foreach (Match match in guidRx.Matches(content))
		{
			if (match.Groups.Count < 2)
				continue;
			var rg = match.Groups[1].Value;
			if (rg == guid || ignoredGuids.Contains(rg))
				continue;

			TrackGuid(rg);

			guidToRefsMap[guid].TryAdd(rg, 0);
			guidFromRefsMap[rg].TryAdd(guid, 0);
		}
	}

	static void TrackGuid(string guid)
	{
		if (!guidToRefsMap.ContainsKey(guid))
			guidToRefsMap.TryAdd(guid, new ConcurrentDictionary<string, byte>());

		if (!guidFromRefsMap.ContainsKey(guid))
			guidFromRefsMap.TryAdd(guid, new ConcurrentDictionary<string, byte>());
	}

	static int AddGuid(string guid)
	{
		if (guidToDocMap.TryGetValue(guid, out var di))
			return di;

		di = index.AddDocument(guid);
		guidToDocMap.Add(guid, di);
		return di;
	}

	static string ToGuid(string assetPath)
	{
		if (pathToGuidMap.TryGetValue(assetPath, out var guid))
			return guid;

		string metaFile = $"{assetPath}.meta";
		if (!File.Exists(metaFile))
			return null;

		string line;
		using (var file = new StreamReader(metaFile))
		{
			while ((line = file.ReadLine()) != null)
			{
				if (!line.StartsWith("guid:", StringComparison.Ordinal))
					continue;
				return line.Substring(6);
			}
		}

		return null;
	}

	#region CreateSearchItem_FetchAllMembers_Provider
	[SearchItemProvider]
	internal static SearchProvider CreateProvider()
	{
		return new SearchProvider(providerId, "Dependencies")
		{
			active = false,
			isExplicitProvider = false,
			showDetails = true,
			showDetailsOptions = ShowDetailsOptions.Inspector | ShowDetailsOptions.Actions,
			onEnable = OnEnable,
			fetchItems = (context, items, provider) => FetchItems(context, provider),
			fetchLabel = FetchLabel,
			fetchDescription = FetchDescription,
			fetchThumbnail = FetchThumbnail,
			trackSelection = TrackSelection,
			toObject = ToObject
		};
	}

	#endregion

	[ExcludeFromPreset]
	class DependencyInfo : ScriptableObject
	{
		public string guid;
		public List<string> broken = new List<string>();
		public List<UnityEngine.Object> @using = new List<UnityEngine.Object>();
		public List<UnityEngine.Object> usedBy = new List<UnityEngine.Object>();
		public List<string> untracked = new List<string>();
	}

	static DependencyInfo s_CurrentDependencyInfo = null;
	static UnityEngine.Object ToObject(SearchItem item, Type type)
	{
		if (s_CurrentDependencyInfo)
			ScriptableObject.DestroyImmediate(s_CurrentDependencyInfo);
		s_CurrentDependencyInfo = ScriptableObject.CreateInstance<DependencyInfo>();
		s_CurrentDependencyInfo.guid = item.id;
		using (var context = SearchService.CreateContext(new string[] { providerId }, $"from:{item.id}"))
		{
			foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(r.id);
				if (string.IsNullOrEmpty(assetPath))
					s_CurrentDependencyInfo.broken.Add(r.id);
				else
				{
					var ur = AssetDatabase.LoadMainAssetAtPath(assetPath);
					if (ur != null)
						s_CurrentDependencyInfo.@using.Add(ur);
					else
						s_CurrentDependencyInfo.untracked.Add($"{assetPath} ({r.id})");
				}
			}
		}

		using (var context = SearchService.CreateContext(new string[] { providerId }, $"to:{item.id}"))
		{
			foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(r.id);
				if (string.IsNullOrEmpty(assetPath))
					s_CurrentDependencyInfo.broken.Add(r.id);
				else
				{
					{
						var ur = AssetDatabase.LoadMainAssetAtPath(assetPath);
						if (ur != null)
							s_CurrentDependencyInfo.usedBy.Add(ur);
						else
							s_CurrentDependencyInfo.untracked.Add($"{assetPath} ({r.id})");
					}
				}
			}
		}

		return s_CurrentDependencyInfo;
	}

	static Texture2D FetchThumbnail(SearchItem item, SearchContext context)
	{
		if (ResolveAssetPath(item, out var path))
			return AssetDatabase.GetCachedIcon(path) as Texture2D ?? InternalEditorUtility.GetIconForFile(path);
		return null;
	}

	static void OnEnable()
	{
		if (index == null)
		{
			if (File.Exists(dependencyIndexLibraryPath))
				Load(dependencyIndexLibraryPath);
			else
				Build();
		}
	}

	[SearchActionsProvider]
	internal static IEnumerable<SearchAction> ActionHandlers()
	{
		yield return SelectAsset();
		yield return Goto("to", "Show outgoing references", "to");
		yield return Goto("from", "Show incoming references", "from");
		yield return Goto("missing", "Show broken links", "is:missing from");
		yield return LogRefs();

		if (SearchService.GetProvider("asset")?.active ?? false)
		{
			yield return new SearchAction("asset", "usings", new GUIContent("Find Usings"), FindUsings);
			yield return new SearchAction("asset", "usage", new GUIContent("Find Usage"), FindUsage);
		}
	}

	static void FindUsage(SearchItem item)
	{
		if (!GlobalObjectId.TryParse(item.id, out var gid))
			return;
		var searchContext = SearchService.CreateContext(providerId, $"to:{gid.assetGUID}");
		SearchService.ShowWindow(searchContext, "Dependencies (to)", saveFilters: false);
	}

	static void FindUsings(SearchItem item)
	{
		if (!GlobalObjectId.TryParse(item.id, out var gid))
			return;
		var searchContext = SearchService.CreateContext(providerId, $"from:{gid.assetGUID}");
		SearchService.ShowWindow(searchContext, "Dependencies (from)", saveFilters: false);
	}

	static SearchAction SelectAsset()
	{
		return new SearchAction(providerId, "select", null, "Select", (SearchItem item) =>
		{
			if (ResolveAssetPath(item, out var path))
				Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
			else
				item.context?.searchView?.SetSearchText($"dep: to:{item.id}");
		})
		{
			closeWindowAfterExecution = false
		};
	}

	static SearchAction LogRefs()
	{
		return new SearchAction(providerId, "log", null, "Log references and usages", (SearchItem[] items) =>
		{
			foreach (var item in items)
			{
				var sb = new StringBuilder();
				if (ResolveAssetPath(item, out var assetPath))
					sb.AppendLine($"Dependency info: {LogAssetHref(assetPath)}");
				using (var context = SearchService.CreateContext(new string[] { providerId }, $"from:{item.id}"))
				{
					sb.AppendLine("outgoing:");
					foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
						LogRefItem(sb, r);
				}

				using (var context = SearchService.CreateContext(new string[] { providerId }, $"to:{item.id}"))
				{
					sb.AppendLine("incoming:");
					foreach (var r in SearchService.GetItems(context, SearchFlags.Synchronous))
						LogRefItem(sb, r);
				}

				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, sb.ToString());
			}
		})
		{
			closeWindowAfterExecution = false
		};
	}

	static string LogAssetHref(string assetPath)
	{
		return $"<a href=\"{assetPath}\" line=\"0\">{assetPath}</a>";
	}

	static void LogRefItem(StringBuilder sb, SearchItem item)
	{
		if (ResolveAssetPath(item, out var assetPath))
			sb.AppendLine($"\t{LogAssetHref(assetPath)} ({item.id})");
		else
			sb.AppendLine($"\t<color=#EE9898>BROKEN</color> ({item.id})");
	}

	static SearchAction Goto(string action, string title, string filter)
	{
		return new SearchAction(providerId, action, null, title, (SearchItem item) => item.context?.searchView?.SetSearchText($"dep: {filter}:{item.id}"))
		{
			closeWindowAfterExecution = false
		};
	}

	static bool ResolveAssetPath(string guid, out string path)
	{
		if (guidToPathMap.TryGetValue(guid, out path))
			return true;

		path = AssetDatabase.GUIDToAssetPath(guid);
		if (!string.IsNullOrEmpty(path))
			return true;

		return false;
	}

	static string ResolveAssetPath(string id)
	{
		if (ResolveAssetPath(id, out var path))
			return path;
		return null;
	}

	static bool ResolveAssetPath(SearchItem item, out string path)
	{
		return ResolveAssetPath(item.id, out path);
	}

	static string FetchLabel(SearchItem item, SearchContext context)
	{
		var metaString = index.GetMetaInfo(item.id);
		var hasMetaString = !string.IsNullOrEmpty(metaString);
		if (ResolveAssetPath(item, out var path))
			return !hasMetaString ? path : $"<color=#EE9898>{path}</color>";

		return $"<color=#EE6666>{item.id}</color>";
	}

	static string GetDescription(SearchItem item)
	{
		var metaString = index.GetMetaInfo(item.id);
		if (!string.IsNullOrEmpty(metaString))
			return metaString;

		if (ResolveAssetPath(item, out _))
			return item.id;

		return "<invalid>";
	}

	static string FetchDescription(SearchItem item, SearchContext context)
	{
		var description = GetDescription(item);
		return $"{FetchLabel(item, context)} ({description})";
	}

	static void TrackSelection(SearchItem item, SearchContext context)
	{
		EditorGUIUtility.systemCopyBuffer = item.id;
		Utils.PingAsset(AssetDatabase.GUIDToAssetPath(item.id));
	}

	static IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider)
	{
		if (index == null)
			OnEnable();
		while (!index.IsReady())
			yield return null;
		foreach (var e in index.Search(context.searchQuery.ToLowerInvariant(), context, provider))
		{
			var item = provider.CreateItem(context, e.id, e.score, null, null, null, e.index);
			item.options &= ~SearchItemOptions.Ellipsis;
			yield return item;
		}
	}
}
