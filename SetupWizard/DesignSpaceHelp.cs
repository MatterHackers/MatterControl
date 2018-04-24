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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl
{
	public class DesignSpaceHelp : DialogPage
	{
		public DesignSpaceHelp()
		: base("Close".Localize())
		{
			this.WindowTitle = "Design Space Help".Localize();
			this.HeaderText = "Navigation Controls and Shortcut Keys".Localize();
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
			tabControl.TabBar.BackgroundColor = theme.ActiveTabBarBackground;

			container.AddChild(tabControl);

			// add the mouse commands
			var mouseControls = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				Padding = theme.DefaultContainerPadding
			};

			var mouseTab = new ToolTab("Mouse".Localize(), tabControl, mouseControls, theme, hasClose: false);
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
				("ctrl left, right","Rotate".Localize()),
				("ctrl shift left, middle","Pan".Localize()),
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

			var keyboardTab = new ToolTab("Keys".Localize(), tabControl, shortcutKeys, theme, hasClose: false);
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
				("shift z","Zoom in".Localize()),
				("z","Zoom out".Localize()),
				("← → ↑ ↓","Rotate".Localize()),
				("shift ← → ↑ ↓","Pan".Localize()),
				//("f","Zoom to fit".Localize()),
				("w","Zoom to window".Localize()),
				("ctrl / ⌘ z","Undo".Localize()),
				("ctrl / ⌘ y","Redo".Localize()),
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

			var tipsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			tabControl.AddTab(new ToolTab("Tips".Localize(), tabControl, tipsContainer, theme, hasClose: false));

			AddTips(tipsContainer);

			var whatsNewContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			tabControl.AddTab(new ToolTab("What's New".Localize(), tabControl, whatsNewContainer, theme, hasClose: false));

			tabControl.SelectedTabIndex = 0;
		}

		private void AddTips(FlowLayoutWidget tipsContainer)
		{
			var sequence = AggContext.StaticData.LoadSequence(@"C:\Users\jlewin\Desktop\editwidget-never-closes.gif");
			sequence.FramePerSecond = 3;
			tipsContainer.AddChild(new ImageSequenceWidget(sequence));
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