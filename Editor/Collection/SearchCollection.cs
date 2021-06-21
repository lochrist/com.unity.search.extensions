using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search.Collections
{
    [Serializable]
    class SearchCollection
    {
        public SearchCollection()
        {
            m_Name = null;
            m_Icon = null;
            query = null;
            color = new Color(0, 0, 0, 0);
            items = new HashSet<SearchItem>();
            m_gids = new List<string>();
        }

        public SearchCollection(string name)
            : this()
        {
            m_Name = name;
        }

        public SearchCollection(SearchQueryAsset searchQuery)
            : this()
        {
            query = searchQuery != null ? searchQuery : throw new ArgumentNullException(nameof(searchQuery));
            m_Name = query.displayName;
        }

        public string name
        {
            get
            {
                if (string.IsNullOrEmpty(m_Name) && query)
                    return query.displayName;
                return m_Name ?? string.Empty;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                    m_Name = null;
                else
                    m_Name = value;
            }
        }

        public Texture2D icon
        {
            get
            {
                if (query && query.icon)
                    return query.icon;
                if (m_Icon)
                    return m_Icon;
                return Icons.quicksearch;
            }

            set
            {
                if (query)
                {
                    m_Icon = null;
                    query.icon = value;
                }
                else
                    m_Icon = value;
            }
        }

        public void AddObject(UnityEngine.Object obj)
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            m_gids.Add(gid);
            objects.Add(obj);
        }

        internal void AddObjects(UnityEngine.Object[] objs)
        {
            var gids = new GlobalObjectId[objs.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(objs, gids);
            m_gids.AddRange(gids.Select(g => g.ToString()));
            objects.UnionWith(objs);
        }

        void LoadObjects()
        {
            var gids = m_gids.Select(id =>
            {
                if (GlobalObjectId.TryParse(id, out var gid))
                    return gid;
                return default;
            }).Where(g => g.identifierType != 0).ToArray();

            var objects = new UnityEngine.Object[gids.Length];
            GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(gids, objects);
            m_Objects = new HashSet<UnityEngine.Object>(objects);
        }

        public SearchQueryAsset query;
        public Color color;
        [SerializeField] private string m_Name;
        [SerializeField] private Texture2D m_Icon;
        [SerializeField] private List<string> m_gids;
        
        [NonSerialized] public HashSet<SearchItem> items;
        [NonSerialized] public HashSet<UnityEngine.Object> m_Objects;

        public ISet<UnityEngine.Object> objects
        {
            get
            {
                if (m_Objects == null)
                    LoadObjects();
                return m_Objects;
            }
        }

        public void RemoveObject(UnityEngine.Object obj)
        {
            if (m_Objects.Remove(obj))
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
                m_gids.Remove(gid.ToString());
            }
        }
    }

    [Serializable]
    class SearchCollections
    {
        public SearchCollections()
        {
            this.collections = new List<SearchCollection>();
        }

        public SearchCollections(List<SearchCollection> collections)
        {
            this.collections = collections;
        }

        public List<SearchCollection> collections;
    }
}
