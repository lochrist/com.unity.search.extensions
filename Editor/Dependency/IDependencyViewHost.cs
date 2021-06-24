using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	interface IDependencyViewHost
	{
		void Repaint();
		void PushViewerState(DependencyViewerState state);
	}
}
