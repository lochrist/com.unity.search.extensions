#if USE_DEPENDENCY_PROVIDER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
	class DependencyManager : EditorWindow
	{
		SearchField m_SearchField;
		DependencyTreeview m_DependencyTreeViewIns;
		DependencyTreeview m_DependencyTreeViewOuts;

		[SerializeField] string m_SearchText;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] TreeViewState m_DependencyTreeViewStateIns;
		[SerializeField] TreeViewState m_DependencyTreeViewStateOuts;

		internal void OnEnable()
		{
			m_SearchField = new SearchField();
			m_SearchText = m_SearchText ?? AssetDatabase.GetAssetPath(Selection.activeObject);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_DependencyTreeViewStateIns = m_DependencyTreeViewStateIns ?? new TreeViewState();
			m_DependencyTreeViewStateOuts = m_DependencyTreeViewStateOuts ?? new TreeViewState();

			m_DependencyTreeViewIns = new DependencyTreeview(m_DependencyTreeViewStateIns, "to");
			m_DependencyTreeViewOuts = new DependencyTreeview(m_DependencyTreeViewStateOuts, "from");
			m_Splitter.host = this;

			Selection.selectionChanged += OnSelectionChanged;
		}

		internal void OnDisable()
		{
			Selection.selectionChanged -= OnSelectionChanged;
		}

		private void OnSelectionChanged()
		{
			m_DependencyTreeViewIns.Reload();
			m_DependencyTreeViewOuts.Reload();
			m_SearchText = AssetDatabase.GetAssetPath(Selection.activeObject);

			titleContent.text = System.IO.Path.GetFileNameWithoutExtension(m_SearchText);
			titleContent.image = AssetDatabase.GetCachedIcon(m_SearchText);
		}

		internal void OnGUI()
		{
			m_Splitter.Init(200f);
			var evt = Event.current;

			using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
			{
				using (new GUILayout.HorizontalScope(Styles.searchReportField))
				{
					var searchTextRect = m_SearchField.GetLayoutRect(m_SearchText, position.width, (Styles.toolbarButton.fixedWidth + Styles.toolbarButton.margin.left) + Styles.toolbarButton.margin.right);
					var searchClearButtonRect = Styles.searchFieldBtn.margin.Remove(searchTextRect);
					searchClearButtonRect.xMin = searchClearButtonRect.xMax - 20f;

					if (Event.current.type == EventType.MouseUp && searchClearButtonRect.Contains(Event.current.mousePosition))
						m_SearchText = string.Empty;

					var previousSearchText = m_SearchText;
					if (Event.current.type != EventType.KeyDown || Event.current.keyCode != KeyCode.None || Event.current.character != '\r')
					{
						m_SearchText = m_SearchField.Draw(searchTextRect, m_SearchText, Styles.searchField);
						if (!string.Equals(previousSearchText, m_SearchText, StringComparison.Ordinal))
						{
							// TODO: update search
						}
					}

					if (!string.IsNullOrEmpty(m_SearchText))
					{
						if (GUI.Button(searchClearButtonRect, Icons.clear, Styles.searchFieldBtn))
							m_SearchText = string.Empty;
						EditorGUIUtility.AddCursorRect(searchClearButtonRect, MouseCursor.Arrow);
					}

					EditorGUIUtility.AddCursorRect(searchClearButtonRect, MouseCursor.Arrow);
				}

				EditorGUILayout.BeginHorizontal();
				var treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1)));
				m_Splitter.Draw(evt, treeViewRect);
				m_DependencyTreeViewIns.OnGUI(treeViewRect);

				treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				m_DependencyTreeViewOuts.OnGUI(treeViewRect);
				EditorGUILayout.EndHorizontal();

				if (evt.type == EventType.Repaint)
				{
					GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
										EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 1f, 0f);
				}
			}
		}

		[MenuItem("Window/Search/Dependency Manager", priority = 5679)]
		public static void OpenNew()
		{
			var win = CreateWindow<DependencyManager>();
			win.position = Utils.GetMainWindowCenteredPosition(new Vector2(1000, 800));
			win.Show();
		}
	}

	class DependencyTreeview : TreeView
	{
		public const string userQuery = "User";
		public const string projectQuery = "Project";

		public static readonly string userTooltip = L10n.Tr("Your saved searches available for all Unity projects on this machine.");
		public static readonly string projectTooltip = L10n.Tr("Shared searches available for all contributors on this project.");

		static class Styles
		{
			public static readonly GUIStyle toolbarButton = new GUIStyle("IN Title")
			{
				margin = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(0, 0, 0, 0),
				imagePosition = ImagePosition.ImageOnly,
				alignment = TextAnchor.MiddleCenter
			};

			public static readonly GUIStyle categoryLabel = new GUIStyle("IN Title")
			{
				richText = true,
				wordWrap = false,
				alignment = TextAnchor.MiddleLeft,
				padding = new RectOffset(16, 0, 3, 0),
			};

			public static readonly GUIStyle itemLabel = Utils.FromUSS(new GUIStyle()
			{
				wordWrap = false,
				stretchWidth = false,
				stretchHeight = false,
				alignment = TextAnchor.MiddleLeft,
				clipping = TextClipping.Overflow,
				margin = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(0, 0, 0, 0)
			}, "quick-search-tree-view-item");
		}

		string m_Filter;

		public bool isRenaming { get; private set; }

		public DependencyTreeview(TreeViewState state, string filter)
			: base(state)
		{
			m_Filter = filter;
			showBorder = false;
			showAlternatingRowBackgrounds = false;
			rowHeight = EditorGUIUtility.singleLineHeight + 4;
			Reload();
		}

		public override void OnGUI(Rect rect)
		{
			var evt = Event.current;

			// Ignore arrow keys for this tree view, these needs to be handled by the search result view (later)
			if (!isRenaming && Utils.IsNavigationKey(evt))
				return;

			base.OnGUI(rect);

			if (evt.type == EventType.MouseDown && evt.button == 0)
			{
				// User has clicked in an area where there are no items: unselect.
				ClearSelection();
			}
		}

		public void ClearSelection()
		{
			SetSelection(new int[0]);
		}

		protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
		{
			if (item is SearchQueryCategoryTreeViewItem)
				return false;

			return base.DoesItemMatchSearch(item, search);
		}

		protected override bool CanMultiSelect(TreeViewItem item)
		{
			return false;
		}

		protected override TreeViewItem BuildRoot()
		{
			var id = 1;
			var root = new TreeViewItem { id = id++, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };

			var obj = Selection.activeObject;
			if (!obj)
				return root;
			var assetPath = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(assetPath))
				return root;
			var depProvider = SearchService.GetProvider("dep");
			using (var context = SearchService.CreateContext(depProvider, $"{m_Filter}=\"{assetPath}\""))
			using (var request = SearchService.Request(context, SearchFlags.Synchronous))
			{
				foreach (var r in request)
				{
					if (r == null)
						continue;
					var path = AssetDatabase.GUIDToAssetPath(r.id);
					root.AddChild(new TreeViewItem(id++, 0, path));
				}
			}
			return root;
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			var evt = Event.current;
			if (evt.type == EventType.Repaint)
			{
				var itemContent = Utils.GUIContentTemp(args.item.displayName, AssetDatabase.GetCachedIcon(args.item.displayName) as Texture2D);
				var oldLeftPadding = Styles.itemLabel.padding.left;
				Styles.itemLabel.padding.left = 4;
				Styles.itemLabel.Draw(args.rowRect, itemContent, args.rowRect.Contains(Event.current.mousePosition), args.selected, false, false);
				Styles.itemLabel.padding.left = oldLeftPadding;
			}
		}

		protected override void SingleClickedItem(int id)
		{
			var tvi = FindItem(id, rootItem);
			if (tvi != null)
				Utils.PingAsset(tvi.displayName);
		}

		protected override void ContextClickedItem(int id)
		{
			if (FindItem(id, rootItem) is SearchQueryTreeViewItem stvi)
				OpenContextualMenu(() => stvi.OpenContextualMenu());
		}

		protected override bool CanRename(TreeViewItem item)
		{
			return ((SearchQueryTreeViewItem)item).CanRename();
		}

		protected override void RenameEnded(RenameEndedArgs args)
		{
			isRenaming = false;
			if (!args.acceptedRename)
				return;

			if (FindItem(args.itemID, rootItem) is SearchQueryTreeViewItem item && item.AcceptRename(args.originalName, args.newName))
			{
				BuildRows(rootItem);
			}
		}

		private static void OpenContextualMenu(Action handler)
		{
			handler();
			Event.current.Use();
		}

		public void RemoveItem(TreeViewItem item)
		{
			item.parent.children.Remove(item);
			BuildRows(rootItem);
		}
	}
}
#endif
