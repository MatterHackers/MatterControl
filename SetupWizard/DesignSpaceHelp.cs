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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class GuideAssets
	{
		/// <summary>
		/// Where to find the gif or evertually movie file
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
		/// This is the imutable key assigned to this guide. It can 
		/// be used to navigate to this guide while opening the control
		/// </summary>
		public string Key;
	}

	public class DesignSpaceGuid : DialogPage
	{
		List<GuideAssets> whatsNewGuides = new List<GuideAssets>();
		List<GuideAssets> allAvailableGuides = new List<GuideAssets>();

		public DesignSpaceGuid()
			: this("", "")
		{

		}

		public DesignSpaceGuid(string preSelectTabName, string guideKey)
		: base("Close".Localize())
		{
			WindowSize = new Vector2(800, 600);
			MakeTestGuides();

			this.WindowTitle = "MatterControl " + "Help".Localize();
			this.HeaderText = "How to succed with MatterControl".Localize();
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
				("ctrl + left","Rotate".Localize()),
				("right","Rotate".Localize()),
				("ctrl + shift left","Pan".Localize()),
				("middle","Pan".Localize()),
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
				("shift + z","Zoom in".Localize()),
				("z","Zoom out".Localize()),
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
				VAnchor = VAnchor.Stretch
			};
			var guideTab = new ToolTab("Guides".Localize(), tabControl, guideSectionContainer, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "Guides Tab"
			};
			tabControl.AddTab(guideTab);

			AddGuides(guideSectionContainer, allAvailableGuides);

			var whatsNewContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			var whatsNewTab = new ToolTab("What's New".Localize(), tabControl, whatsNewContainer, theme, hasClose: false)
			{
				// this can be used to navigate to this tab on construction
				Name = "What's New Tab"
			};
			tabControl.AddTab(whatsNewTab);
			AddGuides(whatsNewContainer, whatsNewGuides);

			// if the what's new tab becomes visible mark the time
			whatsNewContainer.VisibleChanged += (s, e) =>
			{
				if (whatsNewContainer.Visible)
				{
					UserSettings.Instance.set(UserSettingsKey.LastReadWhatsNew, JsonConvert.SerializeObject(DateTime.Now));
				}
			};

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

		private void MakeTestGuides()
		{
			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/3QLZVv",
				Category = "Design Tools",
				SubCategory = "Priting",
				MenuName = "Hotend Controls",
				Title = "Hotend and Extruder Controls",
				Description = "From the hotend control, you can:\n".Localize()
					+ "    • " + "Select Material".Localize() + "\n"
					+ "    • " + "Set Temperature".Localize() + "\n"
					+ "    • " + "Move Print Head".Localize() + "\n"
					+ "    • " + "Load and Unload Filament".Localize()
			});

			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/Ifooem",
				Category = "Design Tools",
				SubCategory = "Creating",
				MenuName = "Adding Parts",
				Title = "Adding Parts to the Bed",
				Description = "You can drag parts into the 3D view from the library side bar, or directly from the desktop."
			});

			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/AW0bcR",
				Category = "Design Tools",
				SubCategory = "Printing",
				MenuName = "Starting a Print",
				Title = "Starting a Print",
				Description = "From the print control, you can:\n".Localize()
					+ "    • " + "Set Layer Height".Localize() + "\n"
					+ "    • " + "Set Fill Density".Localize() + "\n"
					+ "    • " + "Turn on and off Support".Localize() + "\n"
					+ "    • " + "Start Your Print".Localize()
			});

			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/1oH3i1",
				Category = "Design Tools",
				SubCategory = "Arangement",
				MenuName = "Rotate Controls",
				Title = "Rotating Objects in the 3D view",
				Description = "Click on any of the rotate corner contrors to rotate on the plane of that control. Moving the mouse over one of the arrow indicators locks the rotation to a 45° angle."
			});

			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/yNqiNT",
				Category = "Design Tools",
				SubCategory = "Arangement",
				MenuName = "Scale Controls",
				Title = "Scaling Objects in the 3D view",
				Description = "Click on any of the scale corner contrors to scale your part on the bed."
			});

			allAvailableGuides.Add(new GuideAssets()
			{
				AnimationUri = "https://www.matterhackers.com/r/sjMyWZ",
				Category = "Design Tools",
				SubCategory = "Printing",
				MenuName = "Supports",
				Title = "Custom Support Generation",
				Description = "Any object can be turned into support. Simply select it in the 3D view and click the 'Make Support' button. Support will automatically make interface layers and avoid interescting the printing object."
			});

			whatsNewGuides = allAvailableGuides;
		}

		private void AddGuides(FlowLayoutWidget guideContainer, List<GuideAssets> guideList)
		{
			var sequence = new ImageSequence()
			{
				FramesPerSecond = 3,
			};

			sequence.AddImage(new ImageBuffer(1, 1));

			var rightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
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

			var popupMenu = new PopupMenu(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Top | VAnchor.Fit
			};

			double maxMenuItemWidth = 0;
			PopupMenu.MenuItem firstItem = null;
			foreach(var guide in guideList)
			{
				var menuItem = popupMenu.CreateMenuItem(guide.MenuName);
				firstItem = (firstItem == null) ? menuItem : firstItem;
				maxMenuItemWidth = Math.Max(maxMenuItemWidth, menuItem.Width);
				menuItem.Click += (s, e) =>
				{
					title.Text = guide.Title;
					description.Text = guide.Description;
					imageSequenceWidget.ImageSequence = ApplicationController.Instance.GetProcessingSequence(Color.Black);
					
					ApplicationController.Instance.DownloadToImageSequenceAsync(imageSequenceWidget.ImageSequence, guide.AnimationUri);
				};
			}

			popupMenu.Load += (s, e) =>
			{
				firstItem.InvokeClick();
			};

			var splitter = new Splitter()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				SplitterBackground = theme.SplitterBackground
			};
			splitter.SplitterDistance = maxMenuItemWidth;
			splitter.Panel1.AddChild(popupMenu);
			splitter.Panel1.BackgroundColor = theme.SlightShade;
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