using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Search
{
	[ExcludeFromPreset]
	class DependencyInfo : ScriptableObject, IDisposable
	{
		bool disposed;
		public string guid;
		public readonly List<string> broken = new List<string>();
		public readonly List<Object> @using = new List<Object>();
		public readonly List<Object> usedBy = new List<Object>();
		public readonly List<string> untracked = new List<string>();

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					broken.Clear();
					@using.Clear();
					usedBy.Clear();
					untracked.Clear();
				}
				DestroyImmediate(this);
			}
		}

		~DependencyInfo()
		{
			Dispose(disposing: false);
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
