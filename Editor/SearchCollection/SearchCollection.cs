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
            color = Color.clear;
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
        public Color color;

        [NonSerialized] public HashSet<SearchItem> items;
    }
}
