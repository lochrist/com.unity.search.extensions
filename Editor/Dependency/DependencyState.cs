using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[Serializable]
	class DependencyState : ISerializationCallbackReceiver
	{
		[SerializeField] private SearchQuery m_Query;
		[NonSerialized] private SearchTable m_TableConfig;

		public string guid => m_Query.guid;
		public SearchContext context => m_Query.viewState.context;
		public SearchTable tableConfig => m_TableConfig;

		public DependencyState(SearchQuery query)
		{
			m_Query = query;
			m_TableConfig = query.tableConfig == null || query.tableConfig.columns.Length == 0 ? DependencyBuiltinStates.CreateDefaultTable(query.name) : query.tableConfig;
		}

		public DependencyState(SearchQueryAsset query)
			: this(query.ToSearchQuery())
		{
		}

		public DependencyState(string name, SearchContext context, SearchTable tableConfig)
		{
			m_TableConfig = tableConfig;
			m_Query = new SearchQuery()
			{
				name = name,
				viewState = new SearchViewState(context),
				displayName = name,
				tableConfig = m_TableConfig
			};
		}

		public void Dispose()
		{
			m_Query.viewState?.context?.Dispose();
		}

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			if (m_TableConfig == null)
				m_TableConfig = m_Query.tableConfig;
			m_TableConfig?.InitFunctors();
		}
	}
}
