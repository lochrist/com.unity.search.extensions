using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[AttributeUsage(AttributeTargets.Method)]
	class DependencyViewerProviderAttribute : Attribute
	{
		static List<DependencyViewerProviderAttribute> m_StateProviders;
		public static IEnumerable<DependencyViewerProviderAttribute> s_StateProviders
		{
			get
			{
				if (m_StateProviders == null)
					FetchStateProviders();
				return m_StateProviders;
			}
		}
		static void FetchStateProviders()
		{
			m_StateProviders = new List<DependencyViewerProviderAttribute>();
			var methods = TypeCache.GetMethodsWithAttribute<DependencyViewerProviderAttribute>();
			foreach(var mi in methods)
			{
				try
				{
					var attr = mi.GetCustomAttributes(typeof(DependencyViewerProviderAttribute), false).Cast<DependencyViewerProviderAttribute>().First();
					attr.handler = Delegate.CreateDelegate(typeof(Func<DependencyViewerState>), mi) as Func<DependencyViewerState>;
					attr.name = attr.name ?? ObjectNames.NicifyVariableName(mi.Name);
					m_StateProviders.Add(attr);
					attr.providerId = m_StateProviders.Count - 1;
				}
				catch(Exception e)
				{
					Debug.LogError($"Cannot register State provider: {mi.Name}\n{e}");
				}				
			}
		}
		public static DependencyViewerProviderAttribute GetProvider(int id)
		{
			if (id < 0 || id >= s_StateProviders.Count())
			{
				return null;
			}
			return m_StateProviders[id];
		}
		public static DependencyViewerProviderAttribute GetDefault()
		{
			var d = s_StateProviders.FirstOrDefault(p => p.flags.HasFlag(DependencyViewerFlags.TrackSelection));
			if (d != null)
				return d;
			return s_StateProviders.First();
		}

		public string name;
		public DependencyViewerFlags flags;
		private Func<DependencyViewerState> handler;
		private int providerId;
		public DependencyViewerProviderAttribute(DependencyViewerFlags flags = DependencyViewerFlags.None, string name = null)
		{
			this.flags = flags;
			this.name = name;
		}

		public DependencyViewerState CreateState()
		{
			var state = handler();
			if (state == null)
				return null;
			state.flags |= flags;
			state.viewerProviderId = providerId;
			return state;
		}
	}
}
