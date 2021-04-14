using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    [Serializable]
    class SearchCollection
    {
        public SearchCollection()
        {
            #if USE_SEARCH_TABLE
            color = Color.clear;
            #endif
            items = new HashSet<SearchItem>();
            objects = new List<UnityEngine.Object>();
        }

        public SearchCollection(SearchQuery searchQuery)
            : this()
        {
            query = searchQuery != null ? searchQuery : throw new ArgumentNullException(nameof(searchQuery));
        }

        public SearchQuery query;
        public List<UnityEngine.Object> objects;
        #if USE_SEARCH_TABLE
        public Color color;
        #endif

        [NonSerialized] public HashSet<SearchItem> items;
    }
}
