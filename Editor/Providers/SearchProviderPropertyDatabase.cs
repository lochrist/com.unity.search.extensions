using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Search;

static class SearchProviderPropertyDatabase
{
	[SearchItemProvider]
	public static SearchProvider CreateProvider()
	{
		var qe = BuildQueryEngine();
		return new SearchProvider("pdb", "Property DB", (context, provider) => FetchItem(qe, context, provider))
		{
			isExplicitProvider = true,
			fetchDescription = FetchDescription
		};
	}

	private static string FetchDescription(SearchItem item, SearchContext context)
	{
		var record = (PropertyRecordItem)item.data;
		var valueString = GetValueString(record);
		if (item.options.HasAny(SearchItemOptions.Compacted))
			return $"{item.GetLabel(context)} / {valueString}";
		return $"{valueString} ({record.source})";
	}

	static string GetValueString(PropertyRecordItem record)
	{
		if (record.type != PropertyDatabaseType.Volatile)
			return GetValueString((PropertyDatabaseRecordValue)record.entry.value, record.db);

		var volatileRecord = (PropertyDatabaseVolatileRecord)record.entry;
		if (volatileRecord.recordValue.value == null)
			return "<nil>";
		return volatileRecord.recordValue.value.ToString();
	}

	static string GetValueString(PropertyDatabaseRecordValue recordValue, PropertyDatabase db)
	{
		if (!recordValue.valid)
			return "<nil>";
		var value = db.GetObjectFromRecordValue(recordValue);
		if (value != null)
			return value.ToString();
		return $"{recordValue.uint32_0}, {recordValue.uint32_1}, {recordValue.uint32_2}, {recordValue.uint32_3}, " +
			$"{recordValue.uint32_4}, {recordValue.uint32_5}, {recordValue.uint32_6}, {recordValue.uint32_7}";
	}

	readonly struct PropertyRecordItem
	{
		public readonly PropertyDatabase db;
		public readonly IPropertyDatabaseRecord entry;

		public ulong documentKey => entry.key.documentKey;
		public Hash128 propertyKey => entry.key.propertyKey;
		public PropertyDatabaseType type => entry.value.type;
		public string source => System.IO.Path.GetFileNameWithoutExtension(db.filePath);

		public PropertyRecordItem(PropertyDatabase db, IPropertyDatabaseRecord entry)
		{
			this.db = db;
			this.entry = entry;
		}
	}

	static QueryEngine<PropertyRecordItem> BuildQueryEngine()
	{
		var queryEngineOptions = new QueryValidationOptions { validateFilters = false, skipNestedQueries = true };
		var qe = new QueryEngine<PropertyRecordItem>(queryEngineOptions);
		qe.SetSearchDataCallback(e => null, s => s.Length < 2 ? null : s, StringComparison.Ordinal);
		return qe;
	}

	class PropertyDBEvaluator : IQueryHandler<PropertyRecordItem, object>
	{
		private QueryGraph graph;
		private SearchMonitorView view;

		public PropertyDBEvaluator(QueryGraph graph, SearchMonitorView view)
		{
			this.graph = graph;
			this.view = view;
		}

		public IEnumerable<PropertyRecordItem> Eval(object payload)
		{
			if (graph.empty)
				return Enumerable.Empty<PropertyRecordItem>();

			return EvalNode(graph.root.children[0]);
		}

		private IEnumerable<PropertyRecordItem> EvalNode(IQueryNode node)
		{
			return EvalNode(node, null);
		}

		private IEnumerable<PropertyRecordItem> EvalNode(IQueryNode node, IEnumerable<PropertyRecordItem> inSet)
		{
			switch (node.type)
			{
				case QueryNodeType.And:
					Debug.Assert(inSet == null);
					return IntersectRecords(node.children[0], node.children[1]);

				case QueryNodeType.Or:
					return GetRecords(node.children[0]).Concat(GetRecords(node.children[1]));

				case QueryNodeType.Search:
					return GetRecords(node/*, inSet*/);

				case QueryNodeType.Filter:
					return FilterRecords(node, inSet);

				default:
					throw new NotSupportedException($"Node {node.type} not supported for evaluation");
			}
		}

		private IEnumerable<PropertyRecordItem> FilterRecords(IQueryNode node, IEnumerable<PropertyRecordItem> inSet)
		{
			if (!(node is FilterNode fn))
				throw new Exception($"Invalid filter node {node.token.text}");

			if (string.Equals("path", fn.filterId, StringComparison.Ordinal))
			{
				var propertyHash = PropertyDatabase.CreatePropertyHash(fn.filterValue);

				if (inSet != null)
					return inSet.Where(r => r.entry.key.propertyKey == propertyHash);
				else
					return view.propertyDatabaseView.EnumerateAll()
						.Where(r => r.key.propertyKey == propertyHash)
						.Select(r => new PropertyRecordItem(view.propertyDatabaseView.database, r))
						.Concat(view.propertyAliasesView.EnumerateAll()
							.Where(r => r.key.propertyKey == propertyHash)
							.Select(r => new PropertyRecordItem(view.propertyAliasesView.database, r)));
			}

			return Enumerable.Empty<PropertyRecordItem>();
		}

		private IEnumerable<PropertyRecordItem> IntersectRecords(IQueryNode lhs, IQueryNode rhs)
		{
			if (YieldRecords(lhs))
				return EvalNode(rhs, EvalNode(lhs));

			if (YieldRecords(rhs))
				return EvalNode(lhs, EvalNode(rhs));

			return EvalNode(lhs).Intersect(EvalNode(rhs));
		}

		private bool YieldRecords(IQueryNode node)
		{
			if (node.type == QueryNodeType.Search ||
				node.type == QueryNodeType.Or ||
				node.type == QueryNodeType.And)
				return true;
			return false;
		}

		private IEnumerable<PropertyRecordItem> GetRecords(IQueryNode node/*, IEnumerable<PropertyRecordItem> inSet*/)
		{
			if (!Utils.TryParse(node.identifier, out ulong documentKey))
				documentKey = PropertyDatabase.CreateDocumentKey(node.identifier);
			return GetRecords(documentKey, view.propertyDatabaseView)
				.Concat(GetRecords(documentKey, view.propertyAliasesView));
		}

		private IEnumerable<PropertyRecordItem> GetRecords(ulong documentKey, PropertyDatabaseView dbView)
		{
			if (!dbView.TryLoad(documentKey, out IEnumerable<IPropertyDatabaseRecord> records))
				return Enumerable.Empty<PropertyRecordItem>();
			return records.Select(r => new PropertyRecordItem(dbView.database, r));
		}

		public bool Eval(PropertyRecordItem element) => throw new NotSupportedException();
	}

	class PropertyDBQueryHandler : IQueryHandlerFactory<PropertyRecordItem, PropertyDBEvaluator, object>
	{
		public PropertyDBQueryHandler(SearchMonitorView view)
		{
			this.view = view;
		}

		public SearchMonitorView view { get; }

		public PropertyDBEvaluator Create(QueryGraph graph, ICollection<QueryError> errors) => new PropertyDBEvaluator(graph, view);
	}

	static IEnumerable<SearchItem> FetchItem(QueryEngine<PropertyRecordItem> qe, SearchContext context, SearchProvider provider)
	{
		using (var view = SearchMonitor.GetView())
		{
			var query = qe.Parse(context.searchQuery, new PropertyDBQueryHandler(view));
			if (!query.valid)
				yield break;

			foreach (var r in query.Apply())
			{
				var id = $"{r.documentKey}/{r.propertyKey}";
				yield return provider.CreateItem(context, id, $"{id} ({r.type})", null, null, r);
			}
		}
	}
}
