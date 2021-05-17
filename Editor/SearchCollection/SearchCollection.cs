#if DEPENDS_ON_INTERNAL_APIS
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
            color = new Color(0, 0, 0, 0);
            #endif
            items = new HashSet<SearchItem>();
            objects = new List<UnityEngine.Object>();
        }

        public SearchCollection(SearchQueryAsset searchQuery)
            : this()
        {
            query = searchQuery != null ? searchQuery : throw new ArgumentNullException(nameof(searchQuery));
        }

        public SearchQueryAsset query;
        public List<UnityEngine.Object> objects;
        #if USE_SEARCH_TABLE
        public Color color;
        #endif

        [NonSerialized] public HashSet<SearchItem> items;
    }
}
#endif
