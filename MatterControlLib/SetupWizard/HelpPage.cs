/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class HelpArticle
	{
		public string Name;

		public string Path;

		public string ArticleKey { get; set; }

		public List<HelpArticle> Children { get; set; } = new List<HelpArticle>();
	}

	public class HelpPage : DialogPage
	{
		private TreeView treeView;
		private string guideKey = null;

		public HelpPage(string guideKey = null)
			: base("Close".Localize())
		{
			WindowSize = new Vector2(940, 700);

			this.guideKey = guideKey;
			this.WindowTitle = "MatterControl " + "Help".Localize();
			this.HeaderText = "How to succeed with MatterControl".Localize();
			this.ChildBorderColor = theme.BorderColor40;

			var tabControl = new SimpleTabs(theme, new GuiWidget())
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;

			contentRow.AddChild(tabControl);
			contentRow.Padding = 0;

			// add the mouse commands
			var mouseControls = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				Padding = theme.DefaultContainerPadding
			};

			var mouseTab = new ToolTab("Mouse", "Mouse".Localize(), tabControl, mouseControls, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "Mouse Tab"
			};
			tabControl.AddTab(mouseTab);

			var mouseKeys = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mouseControls.AddChild(mouseKeys);

			var mouseActions = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Border = new BorderDouble(1, 0, 0, 0),
				BorderColor = this.ChildBorderColor
			};
			mouseControls.AddChild(mouseActions);

			var mouseKeyActions = new List<(string key, string action)>(new(string, string)[]
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

			// now add the keyboard commands
			var shortcutKeys = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				Padding = theme.DefaultContainerPadding
			};

			var keyboardTab = new ToolTab("Keys", "Keys".Localize(), tabControl, shortcutKeys, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "Keys Tab"
			};
			tabControl.AddTab(keyboardTab);

			var keys = new FlowLayoutWidget(FlowDirection.TopToBottom);
			shortcutKeys.AddChild(keys);

			var actions = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Border = new BorderDouble(1, 0, 0, 0),
				BorderColor = this.ChildBorderColor
			};
			shortcutKeys.AddChild(actions);

			tabControl.TabBar.Padding = theme.ToolbarPadding.Clone(left: 2, bottom: 0);

			var keyActions = new List<(string key, string action)>(new(string, string)[]
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

			var guideSectionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			var guideTab = new ToolTab("Guides", "Guides".Localize(), tabControl, guideSectionContainer, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "Guides Tab"
			};
			tabControl.AddTab(guideTab);

			AddGuides(guideSectionContainer);

			// If guideKey is empty, switch to first tab
			if (string.IsNullOrEmpty(guideKey))
			{
				tabControl.SelectedTabIndex = 0;
			}
			else
			{
				// Otherwise switch to guides tab and select the target item
				tabControl.SelectedTabIndex = tabControl.GetTabIndex(guideTab);
			}
		}

		private void AddGuides(FlowLayoutWidget guideContainer)
		{
			var sequence = new ImageSequence()
			{
				FramesPerSecond = 3,
			};

			sequence.AddImage(new ImageBuffer(1, 1));

			var description = new GuiWidget();
			var markdownWidget = new MarkdownWidget(theme)
			{
				BackgroundColor = theme.ResolveColor(theme.BackgroundColor, new Color(Color.White, 20)),
				Padding = new BorderDouble(left: theme.DefaultContainerPadding / 2)
			};

			treeView = new TreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
			};
			treeView.AfterSelect += (s, e) =>
			{
				if (treeView.SelectedNode.Tag is HelpArticle article)
				{
					if (!string.IsNullOrWhiteSpace(article.Path))
					{
						markdownWidget.LoadUri(new Uri(ApplicationController.Instance.HelpArticleSource, article.Path), sourceArticle: article);
					}
					else
					{
						// Switch to empty content when path article lacks path
						markdownWidget.Markdown = "";
					}
				}
			};

			TreeNode rootNode = null;

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
			treeView.AddChild(rootNode);

			maxMenuItemWidth = Math.Max(maxMenuItemWidth, rootNode.Width);

			var splitter = new Splitter()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				SplitterBackground = theme.SplitterBackground
			};
			splitter.SplitterDistance = maxMenuItemWidth + 130;

			splitter.Panel1.Padding = new BorderDouble(theme.DefaultContainerPadding).Clone(right: 2);
			splitter.Panel1.AddChild(treeView);
			splitter.Panel2.AddChild(markdownWidget);
			guideContainer.AddChild(splitter);
		}

		private TreeNode initialSelection = null;

		private TreeNode ProcessTree(HelpArticle container)
		{
			var treeNode = new TreeNode(theme, false)
			{
				Text = container.Name,
				Tag = container
			};

			foreach (var item in container.Children.OrderBy(i => i.Children.Count == 0).ThenBy(i => i.Name))
			{
				if (item.Children.Count > 0)
				{
					treeNode.Nodes.Add(ProcessTree(item));
				}
				else
				{
					var newNode = new TreeNode(theme, false)
					{
						Text = item.Name,
						Tag = item

					};

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

		public Color ChildBorderColor { get; private set; }

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