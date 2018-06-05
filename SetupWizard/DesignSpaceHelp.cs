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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class GuideAsset
	{
		/// <summary>
		/// Where to find the gif or eventually movie file
		/// </summary>
		public string AnimationUri;

		/// <summary>
		/// The first level category this guide is part of
		/// </summary>
		public string Category;

		/// <summary>
		/// Second level category
		/// </summary>
		public string SubCategory;

		/// <summary>
		/// The name that is in the navigation list with categories
		/// </summary>
		public string MenuName;

		/// <summary>
		/// The long title that appears under the animation
		/// </summary>
		public string Title;

		/// <summary>
		/// The description that is under the title
		/// </summary>
		public string Description;

		/// <summary>
		/// This is the immutable key assigned to this guide. It can
		/// be used to navigate to this guide while opening the control
		/// </summary>
		public string Key;
	}

	public class DesignSpaceGuide : DialogPage
	{
		private List<GuideAsset> whatsNewGuides = new List<GuideAsset>();
		private List<GuideAsset> allAvailableGuides = new List<GuideAsset>();


		public DesignSpaceGuide(string preSelectTabName, string guideKey)
			: base("Close".Localize())
		{
			WindowSize = new Vector2(800, 600);

			allAvailableGuides = ApplicationController.Instance.FeatureGuides;

			// TODO: Guides document should have separate properties for differing top level containers
			whatsNewGuides = allAvailableGuides;

			this.WindowTitle = "MatterControl " + "Help".Localize();
			this.HeaderText = "How to succeed with MatterControl".Localize();
			this.ChildBorderColor = theme.GetBorderColor(75);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			contentRow.AddChild(container);

			var tabControl = new SimpleTabs(theme, new GuiWidget())
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;

			container.AddChild(tabControl);

			// add the mouse commands
			var mouseControls = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				Padding = theme.DefaultContainerPadding
			};

			var mouseTab = new ToolTab("Mouse".Localize(), tabControl, mouseControls, theme, hasClose: false)
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
				("left click","Make Selection".Localize()),
				("left click + shift","Add to Selection".Localize()),
				("left click + control","Toggle Selection".Localize()),
				("left drag","Rubber Band Selection".Localize()),
				("left drag","Move Part".Localize()),
				("left drag + shift","Move Part Constrained".Localize()),
				("left drag + shift + ctrl","Pan View".Localize()),
				("left drag + ctrl","Rotate View".Localize()),
				("middle drag","Pan View".Localize()),
				("right drag","Rotate View".Localize()),
				("wheel","Zoom".Localize())
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

			var keyboardTab = new ToolTab("Keys".Localize(), tabControl, shortcutKeys, theme, hasClose: false)
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
				("ctrl + +","Zoom in".Localize()),
				("ctrl + -","Zoom out".Localize()),
				("← → ↑ ↓","Rotate".Localize()),
				("shift + ← → ↑ ↓","Pan".Localize()),
				//("f","Zoom to fit".Localize()),
				("w","Zoom to window".Localize()),
				("ctrl + z","Undo".Localize()),
				("ctrl + y","Redo".Localize()),
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
				Padding = theme.DefaultContainerPadding
			};
			var guideTab = new ToolTab("Guides".Localize(), tabControl, guideSectionContainer, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "Guides Tab"
			};
			tabControl.AddTab(guideTab);

			AddGuides(guideSectionContainer, allAvailableGuides);

			tabControl.SelectedTabIndex = 0;
			if(!string.IsNullOrWhiteSpace(preSelectTabName))
			{
				// try to find the named tab
				int index = 0;
				foreach(var tab in tabControl.AllTabs)
				{
					if (tab is GuiWidget widget)
					{
						if (widget.Name == preSelectTabName)
						{
							tabControl.SelectedTabIndex = index;
							break;
						}
					}
					index++;
				}
			}
		}

		private void AddGuides(FlowLayoutWidget guideContainer, List<GuideAsset> guideList)
		{
			var sequence = new ImageSequence()
			{
				FramesPerSecond = 3,
			};

			sequence.AddImage(new ImageBuffer(1, 1));

			var rightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = theme.DefaultContainerPadding
			};

			var imageSequenceWidget = new ImageSequenceWidget(300, 200)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				ImageSequence = sequence,
				Border = new BorderDouble(1),
				BorderColor = theme.Colors.PrimaryTextColor
			};
			rightPanel.AddChild(imageSequenceWidget);

			var title = new WrappedTextWidget("title", pointSize: 24, textColor: theme.Colors.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 4, 10, 10)
			};
			rightPanel.AddChild(title);

			var description = new WrappedTextWidget("details", pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 4, 10, 10),
			};
			rightPanel.AddChild(description);

			var treeView = new TreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
			};
			treeView.AfterSelect += (s, e) =>
			{
				if (treeView.SelectedNode.Tag is GuideAsset guide)
				{
					title.Text = guide.Title;
					description.Text = guide.Description;
					imageSequenceWidget.ImageSequence = ApplicationController.Instance.GetProcessingSequence(Color.Black);

					ApplicationController.Instance.DownloadToImageSequenceAsync(imageSequenceWidget.ImageSequence, guide.AnimationUri);
				}
			};

			var rootNode = new TreeNode()
			{
				Text = "Help Guides",
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize,
				TreeView = treeView,
			};

			treeView.Load += (s, e) =>
			{
				rootNode.Expanded = true;
				treeView.SelectedNode = rootNode.Nodes.FirstOrDefault();
			};

			TreeNode firstNode = null;
			double maxMenuItemWidth = 0;

			foreach (var guide in guideList)
			{
				var treeNode = new TreeNode()
				{
					Text = guide.MenuName,
					Tag = guide,
					TextColor = theme.Colors.PrimaryTextColor,
					PointSize = theme.DefaultFontSize,
				};

				maxMenuItemWidth = Math.Max(maxMenuItemWidth, treeNode.Width);

				if (firstNode == null)
				{
					firstNode = treeNode;
				}

				rootNode.Nodes.Add(treeNode);
			}

			treeView.AddChild(rootNode);

			var splitter = new Splitter()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				SplitterBackground = theme.SplitterBackground
			};
			splitter.SplitterDistance = maxMenuItemWidth + 30;
			splitter.Panel1.AddChild(treeView);
			splitter.Panel2.AddChild(rightPanel);
			guideContainer.AddChild(splitter);
		}

		public Color ChildBorderColor { get; private set; }

		private void AddContent(GuiWidget column, string text, bool left, bool bold)
		{
			var container = new GuiWidget()
			{
				HAnchor = HAnchor.Fit | (left ? HAnchor.Right: HAnchor.Left),
				VAnchor = VAnchor.Fit
			};
			var content = new TextWidget(text, bold: bold, textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
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