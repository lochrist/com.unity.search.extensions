using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[Serializable]
	class DependencyViewerState
	{
		public DependencyViewerFlags flags;
		public List<DependencyState> states;
		public List<string> globalIds;
		public string name;
		[SerializeField] internal int viewerProviderId;
		[SerializeField] private GUIContent m_Description;
		[SerializeField] private GUIContent m_WindowTitle;

		public DependencyViewerState(string name, DependencyState state)
				: this(name, null, new[] { state })
		{
		}

		public DependencyViewerState(string name, IEnumerable<DependencyState> states = null)
				: this(name, null, states)
		{
		}


		public DependencyViewerState(string name, List<string> globalIds, IEnumerable<DependencyState> states = null)
		{
			this.name = name;
			this.globalIds = globalIds;
			this.states = states != null ? states.ToList() : new List<DependencyState>();
			viewerProviderId = -1;
		}

		public DependencyViewerProviderAttribute viewerProvider => DependencyViewerProviderAttribute.GetProvider(viewerProviderId) ?? DependencyViewerProviderAttribute.GetDefault();
		public bool trackSelection => flags.HasFlag(DependencyViewerFlags.TrackSelection);

		public GUIContent description
		{
			get
			{
				if (m_Description == null)
				{
					if (globalIds != null)
					{
						var names = EnumeratePaths().ToList();
						if (names.Count == 0)
							m_Description = new GUIContent("No dependencies");
						else if (names.Count == 1)
							m_Description = new GUIContent(string.Join(", ", names), GetPreview());
						else if (names.Count < 4)
							m_Description = new GUIContent(string.Join(", ", names), Icons.dependencies);
						else
							m_Description = new GUIContent($"{names.Count} object selected", string.Join("\n", names));
					}
					else
					{
						m_Description = new GUIContent(name);
					}					
				}
				return m_Description;
			}
			set
			{
				m_Description = value;
			}
		}

		public GUIContent windowTitle		
		{
			get
			{
				if (m_WindowTitle == null)
				{
					if (globalIds != null)
					{
						var names = EnumeratePaths().ToList();
						if (names.Count != 1)
							m_WindowTitle = new GUIContent($"Dependency Viewer ({names.Count})", Icons.dependencies);
						else
							m_WindowTitle = new GUIContent(System.IO.Path.GetFileNameWithoutExtension(names.First()), GetIcon());
					}
					else
					{
						m_WindowTitle = new GUIContent(name);
					}					
				}

				return m_WindowTitle;
			}
			set
			{
				m_WindowTitle = value;
			}
		}

		Texture GetIcon()
		{
			if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
				return Icons.dependencies;
			return AssetDatabase.GetCachedIcon(AssetDatabase.GUIDToAssetPath(gid.m_AssetGUID)) ?? Icons.dependencies;
		}

		Texture GetPreview()
		{
			if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
				return Icons.dependencies;
			var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
			return AssetPreview.GetAssetPreview(obj)
				?? AssetPreview.GetAssetPreviewFromGUID(gid.assetGUID.ToString())
				?? Icons.dependencies;
		}

		IEnumerable<string> EnumeratePaths()
		{
			if (globalIds == null || globalIds.Count == 0)
				yield break;

			foreach (var sgid in globalIds)
			{
				if (!GlobalObjectId.TryParse(sgid, out var gid))
					continue;
				var instanceId = GlobalObjectId.GlobalObjectIdentifierToInstanceIDSlow(gid);
				var assetPath = AssetDatabase.GetAssetPath(instanceId);
				if (!string.IsNullOrEmpty(assetPath))
					yield return assetPath;
				else if (EditorUtility.InstanceIDToObject(instanceId) is UnityEngine.Object obj)
					yield return SearchUtils.GetObjectPath(obj).Substring(1);
			}
		}
	}
}
