/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Agg;
using MatterControlLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class HelpTreePanel : SearchableTreePanel
	{
		private string guideKey = null;

		public HelpTreePanel(ThemeConfig theme, string guideKey = null)
			: base(theme)
		{
			horizontalSplitter.Panel1.BackgroundColor = Color.Black.WithAlpha(12);

			var toolbar = new Toolbar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = theme.ToolbarPadding
			};

			theme.ApplyBottomBorder(toolbar);

			toolbar.AddChild(new TextButton("MatterControl Help".Localize(), theme)
			{
				Padding = new BorderDouble(6, 0),
				Selectable = false
			});

			this.AddChild(toolbar, 0);

			this.ChildBorderColor = theme.BorderColor40;
			AddGuides();
			CreateMousePage();
			CreateKeyBindingsPage();
		}

		protected override void PerformSearch(string filter)
		{
			searchHits = new HashSet<string>(HelpIndex.Search(filter).Select(d => d.Path));

			base.PerformSearch(filter);
		}

		protected override bool FilterTree(TreeNode context, string filter, bool parentVisible, List<TreeNode> matches)
		{
			// Filter against make/model for printers or make for top level nodes
			string path = (context as HelpArticleTreeNode)?.HelpArticle.Path;

			bool isSearchMatch = searchHits.Contains(path);

			context.Visible = isSearchMatch || parentVisible;

			if (context.Visible
				&& context.NodeParent != null)
			{
				context.NodeParent.Visible = true;
				context.NodeParent.Expanded = true;
				context.Expanded = true;
			}

			if (context.NodeParent != null
				&& isSearchMatch)
			{
				matches.Add(context);
			}

			bool childMatched = false;

			foreach (var child in context.Nodes)
			{
				childMatched |= FilterTree(child, filter, isSearchMatch || parentVisible, matches);
			}

			bool hasMatch = childMatched || isSearchMatch;

			if (hasMatch)
			{
				context.Visible = context.Expanded = true;
			}

			return hasMatch;
		}

		protected override void OnClearSearch()
		{
			rootNode.Expanded = true;
			base.OnClearSearch();
		}

		private void CreateKeyBindingsPage()
		{
			double left, right;

			// now add the keyboard commands
			var shortcutKeys = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = theme.DefaultContainerPadding,
				Visible = false
			};

			var keys = new FlowLayoutWidget(FlowDirection.TopToBottom);
			shortcutKeys.AddChild(keys);

			var actions = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Border = new BorderDouble(1, 0, 0, 0),
				BorderColor = this.ChildBorderColor
			};
			shortcutKeys.AddChild(actions);

			var keyActions = new List<(string key, string action)>(new (string, string)[]
			{
				("F1","Show Help".Localize()),
				("ctrl + +","Zoom in".Localize()),
				("ctrl + -","Zoom out".Localize()),
				("← → ↑ ↓","Rotate".Localize()),
				("shift + ← → ↑ ↓","Pan".Localize()),
				//("f","Zoom to fit".Localize()),
				("w","Zoom to window".Localize()),
				("ctrl + s","Save".Localize()),
				("ctrl + z","Undo".Localize()),
				("ctrl + y","Redo".Localize()),
				("ctrl + p","Print".Localize()),
				("delete","Delete selection".Localize()),
				("space bar","Clear selection".Localize()),
				("esc","Cancel command".Localize()),
				//("enter","Accept command".Localize())
			});

			AddContent(keys, "Keys".Localize(), true, true);
			AddContent(actions, "Action".Localize(), false, true);

			foreach (var keyAction in keyActions)
			{
				AddContent(keys, keyAction.key, true, false);
				AddContent(actions, keyAction.action, false, false);
			}

			// center the vertical bar in the view by adding margin to the small side
			left = Math.Max(0, actions.Width - keys.Width);
			right = Math.Max(0, keys.Width - actions.Width);
			shortcutKeys.Margin = new BorderDouble(left, 0, right, 0);

			rootNode.Nodes.Add(new TreeNode(theme, false)
			{
				Text = "Keys".Localize(),
				Tag = shortcutKeys
			});

			horizontalSplitter.Panel2.AddChild(shortcutKeys);
		}

		private void CreateMousePage()
		{
			// add the mouse commands
			var mouseControls = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = theme.DefaultContainerPadding,
				Visible = false
			};

			var mouseKeys = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mouseControls.AddChild(mouseKeys);

			var mouseActions = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Border = new BorderDouble(1, 0, 0, 0),
				BorderColor = this.ChildBorderColor
			};
			mouseControls.AddChild(mouseActions);

			var mouseKeyActions = new List<(string key, string action)>(new (string, string)[]
			{
				("left click".Localize(), "Make Selection".Localize()),
				("left click".Localize() + " + shift","Add to Selection".Localize()),
				("left click".Localize() + " + ctrl","Toggle Selection".Localize()),
				("left drag".Localize(), "Rubber Band Selection".Localize()),
				("left drag".Localize(), "Move Part".Localize()),
				("left drag".Localize() + " + shift", "Move Part Constrained".Localize()),
				("left drag".Localize() + " + shift + ctrl", "Pan View".Localize()),
				("left drag".Localize() + " + ctrl","Rotate View".Localize()),
				("middle drag".Localize(), "Pan View".Localize()),
				("right drag".Localize(), "Rotate View".Localize()),
				("wheel".Localize(), "Zoom".Localize())
			});

			AddContent(mouseKeys, "Mouse".Localize(), true, true);
			AddContent(mouseActions, "Action".Localize(), false, true);

			foreach (var keyAction in mouseKeyActions)
			{
				AddContent(mouseKeys, keyAction.key, true, false);
				AddContent(mouseActions, keyAction.action, false, false);
			}

			// center the vertical bar in the view by adding margin to the small side
			var left = Math.Max(0, mouseActions.Width - mouseKeys.Width);
			var right = Math.Max(0, mouseKeys.Width - mouseActions.Width);
			mouseControls.Margin = new BorderDouble(left, 0, right, 0);

			rootNode.Nodes.Add(new TreeNode(theme, false)
			{
				Text = "Mouse".Localize(),
				Tag = mouseControls
			});

			horizontalSplitter.Panel2.AddChild(mouseControls);
		}

		private void AddGuides()
		{
			var sequence = new ImageSequence()
			{
				FramesPerSecond = 3,
			};

			sequence.AddImage(new ImageBuffer(1, 1));

			var description = new GuiWidget();
			var markdownWidget = new MarkdownWidget(theme)
			{
				Padding = new BorderDouble(left: theme.DefaultContainerPadding / 2)
			};

			treeView.AfterSelect += (s, e) =>
			{
				// Hide all sibling content controls
				foreach (var child in horizontalSplitter.Panel2.Children)
				{
					child.Visible = false;
				}

				if (treeView.SelectedNode?.Tag is HelpArticle article)
				{
					markdownWidget.MatchingText = this.MatchingText;

					// reset matching text after applying
					this.MatchingText = null;

					if (!string.IsNullOrWhiteSpace(article.Path))
					{
						markdownWidget.LoadUri(new Uri(ApplicationController.Instance.HelpArticleSource, article.Path), sourceArticle: article);
					}
					else
					{
						// Switch to empty content when path article lacks path
						markdownWidget.Markdown = "";
					}

					// Show Markdown help page
					markdownWidget.Visible = true;
				}
				else if (treeView.SelectedNode?.Tag is GuiWidget widget)
				{
					// Show non-markdown page
					widget.Visible = true;
				}
			};

			treeView.Load += (s, e) =>
			{
				rootNode.Expanded = true;

				if (treeView.SelectedNode == null)
				{
					if (string.IsNullOrEmpty(guideKey))
					{
						treeView.SelectedNode = rootNode.Nodes.FirstOrDefault();
					}
					else
					{
						if (initialSelection != null)
						{
							treeView.SelectedNode = initialSelection;
						}

						// TODO: Implement or revise .Expanded
						if (treeView.SelectedNode != null)
						{
							foreach (var ancestor in treeView.SelectedNode.Parents<TreeNode>())
							{
								ancestor.Expanded = true;
							}
						}
					}
				}

				if (treeView.SelectedNode == null)
				{
					treeView.SelectedNode = rootNode;
				}
			};

			double maxMenuItemWidth = 0;

			rootNode = ProcessTree(ApplicationController.Instance.HelpArticles);
			rootNode.Text = "Help";
			rootNode.TreeView = treeView;

			contentPanel.AddChild(rootNode);

			maxMenuItemWidth = Math.Max(maxMenuItemWidth, rootNode.Width);

			horizontalSplitter.Panel2.AddChild(markdownWidget);
		}

		private TreeNode initialSelection = null;
		private TreeNode rootNode;

		private Dictionary<string, HelpArticleTreeNode> nodesByPath = new Dictionary<string, HelpArticleTreeNode>();
		private IEnumerable<HelpSearchResult> searchResults;
		private HashSet<string> searchHits;

		private TreeNode ProcessTree(HelpArticle container)
		{
			var treeNode = new HelpArticleTreeNode(container, theme);

			nodesByPath[container.Path] = treeNode;

			foreach (var item in container.Children.OrderBy(i => i.Children.Count == 0).ThenBy(i => i.Name))
			{
				if (item.Children.Count > 0)
				{
					treeNode.Nodes.Add(ProcessTree(item));
				}
				else
				{
					var newNode = new HelpArticleTreeNode(item, theme);

					nodesByPath[item.Path] = newNode;

					if (item.Name == guideKey
						|| (guideKey != null
							&& item.ArticleKey == guideKey
							&& ApplicationController.Instance.HelpArticlesByID.ContainsKey(guideKey)))
					{
						initialSelection = newNode;
					}

					treeNode.Nodes.Add(newNode);
				}
			}

			return treeNode;
		}

		public string ActiveNodePath
		{
			get => treeView.SelectedNode?.Tag as string;
			set
			{
				if (nodesByPath.TryGetValue(value, out HelpArticleTreeNode treeNode))
				{
					treeView.SelectedNode = treeNode;
				}
			}
		}

		public Color ChildBorderColor { get; private set; }

		public string MatchingText { get; internal set; }

		private void AddContent(GuiWidget column, string text, bool left, bool bold)
		{
			var container = new GuiWidget()
			{
				HAnchor = HAnchor.Fit | (left ? HAnchor.Right: HAnchor.Left),
				VAnchor = VAnchor.Fit
			};
			var content = new TextWidget(text, bold: bold, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = (left ? new BorderDouble(5, 3, 10, 3) : new BorderDouble(10, 3, 5, 3))
			};
			container.AddChild(content);

			column.AddChild(container);
			column.AddChild(new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				Border = new BorderDouble(0, 1, 0, 0),
				BorderColor = this.ChildBorderColor,
			});
		}
	}
}