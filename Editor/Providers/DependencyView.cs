#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
	[SearchResultView, Icon("UnityEditor.FindDependencies")]
	class DependencyView : ResultView
	{
		public DependencyView(ISearchView hostView)
			: base(hostView)
		{
		}

		public override void Draw(Rect rect, ICollection<int> selection)
		{
			GUI.Button(rect, "Dependency");
		}

		public override int GetDisplayItemCount()
		{
			return 0;
		}

		protected override int GetFirstVisibleItemIndex()
		{
			return -1;
		}

		protected override int GetLastVisibleItemIndex()
		{
			return -1;
		}
	}
}
#endif
