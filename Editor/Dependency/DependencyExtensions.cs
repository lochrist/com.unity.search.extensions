using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	static class DependencyExtensions
	{
		[MenuItem("Window/Search/Dependency Viewer", priority = 5679)]
		internal static void OpenNew()
		{
			var win = EditorWindow.CreateWindow<DependencyViewer>();
			win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 400));
			win.Show();
		}

		[SearchExpressionEvaluator]
		internal static IEnumerable<SearchItem> Selection(SearchExpressionContext c)
		{
			return TaskEvaluatorManager.EvaluateMainThread<SearchItem>(CreateItemsFromSelection);
		}

		[SearchExpressionEvaluator("deps", SearchExpressionType.Iterable)]
		internal static IEnumerable<SearchItem> SceneUses(SearchExpressionContext c)
		{
			var args = c.args[0].Execute(c);
			foreach (var e in args)
			{
				if (e == null || e.value == null)
				{
					yield return null;
					continue;
				}

				var id = e.value.ToString();
				if (Utils.TryParse(id, out int instanceId))
				{
					var assetProvider = SearchService.GetProvider(Providers.AssetProvider.type);
					var sceneProvider = SearchService.GetProvider(Providers.BuiltInSceneObjectsProvider.type);
					foreach (var item in TaskEvaluatorManager.EvaluateMainThread(() =>
						GetSceneObjectDependencies(c.search, sceneProvider, assetProvider, instanceId).ToList()))
					{
						yield return item;
					}
				}
			}
		}

		static IEnumerable<SearchItem> GetSceneObjectDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider assetProvider, int instanceId)
		{
			var obj = EditorUtility.InstanceIDToObject(instanceId);
			if (!obj)
				yield break;

			var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
			if (!go && obj is Component goc)
			{
				foreach (var ce in GetComponentDependencies(context, sceneProvider, assetProvider, goc))
					yield return ce;
			}
			else if (go)
			{
				// Index any prefab reference
				var containerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
				if (!string.IsNullOrEmpty(containerPath))
					yield return Providers.AssetProvider.CreateItem("DEPS", context, assetProvider, null, containerPath, 0, SearchDocumentFlags.Asset);

				var gocs = go.GetComponents<Component>();
				for (int componentIndex = 1; componentIndex < gocs.Length; ++componentIndex)
				{
					var c = gocs[componentIndex];
					if (!c || (c.hideFlags & HideFlags.HideInInspector) == HideFlags.HideInInspector)
						continue;

					foreach (var ce in GetComponentDependencies(context, sceneProvider, assetProvider, c))
						yield return ce;
				}
			}
		}

		static IEnumerable<SearchItem> GetComponentDependencies(SearchContext context, SearchProvider sceneProvider, SearchProvider assetProvider, Component c)
		{
			using (var so = new SerializedObject(c))
			{
				var p = so.GetIterator();
				var next = p.NextVisible(true);
				while (next)
				{
					if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
					{
						var assetPath = AssetDatabase.GetAssetPath(p.objectReferenceValue);
						if (!string.IsNullOrEmpty(assetPath))
							yield return Providers.AssetProvider.CreateItem("DEPS", context, assetProvider, null, assetPath, 0, SearchDocumentFlags.Asset);
						else if (p.objectReferenceValue is GameObject cgo)
							yield return Providers.SceneProvider.AddResult(context, sceneProvider, cgo);
						else if (p.objectReferenceValue is Component cc && cc.gameObject)
							yield return Providers.SceneProvider.AddResult(context, sceneProvider, cc.gameObject);
					}
					next = p.NextVisible(p.hasVisibleChildren);
				}
			}
		}

		static void CreateItemsFromSelection(Action<SearchItem> yielder)
		{
			foreach (var obj in UnityEditor.Selection.objects)
			{
				var assetPath = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(assetPath))
					yielder(EvaluatorUtils.CreateItem(assetPath));
				else
					yielder(EvaluatorUtils.CreateItem(obj.GetInstanceID()));
			}
		}
	}
}
