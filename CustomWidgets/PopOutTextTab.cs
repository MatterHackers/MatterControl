/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl;
using MatterHackers.VectorMath;
using System;
using System.IO;
using MatterHackers.Localizations;

namespace MatterHackers.Agg.UI
{
	public class PopOutTextTabWidget : Tab
	{
		private PopOutManager popOutManager;
		private Button popOutButton;

		public PopOutTextTabWidget(TabPage tabPageControledByTab, string internalTabName, Vector2 minSize)
			: this(tabPageControledByTab, internalTabName, minSize, 12)
		{
		}

		public PopOutTextTabWidget(TabPage tabPageControledByTab, string internalTabName, Vector2 minSize, double pointSize)
			: base(internalTabName, new GuiWidget(), new GuiWidget(), new GuiWidget(), tabPageControledByTab)
		{
			this.Padding = new BorderDouble(5, 0);
			this.Margin = new BorderDouble(0, 0, 10, 0);

			RGBA_Bytes selectedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			RGBA_Bytes selectedBackgroundColor = new RGBA_Bytes();
			RGBA_Bytes normalTextColor = ActiveTheme.Instance.TabLabelUnselected;
			RGBA_Bytes normalBackgroundColor = new RGBA_Bytes();

			AddText(tabPageControledByTab.Text, selectedWidget, selectedTextColor, selectedBackgroundColor, pointSize, true);
			AddText(tabPageControledByTab.Text, normalWidget, normalTextColor, normalBackgroundColor, pointSize, false);

			tabPageControledByTab.TextChanged += tabPageControledByTab_TextChanged;

			SetBoundsToEncloseChildren();

			popOutManager = new PopOutManager(TabPage, minSize, tabPageControledByTab.Text, internalTabName);
		}

		public void ShowInWindow()
		{
			popOutManager.ShowContentInWindow();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (!popOutButton.FirstWidgetUnderMouse)
			{
				OnSelected(mouseEvent);
			}

			base.OnMouseDown(mouseEvent);
		}

		private void tabPageControledByTab_TextChanged(object sender, EventArgs e)
		{
			normalWidget.Children[0].Text = ((GuiWidget)sender).Text;
			normalWidget.SetBoundsToEncloseChildren();
			selectedWidget.Children[0].Text = ((GuiWidget)sender).Text;
			selectedWidget.SetBoundsToEncloseChildren();
			SetBoundsToEncloseChildren();
		}

		public TextWidget tabTitle;
		private FlowLayoutWidget leftToRight;

		private void AddText(string tabText, GuiWidget widgetState, RGBA_Bytes textColor, RGBA_Bytes backgroundColor, double pointSize, bool isActive)
		{
			leftToRight = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
				Margin = 0,
				Padding = 0
			};

			tabTitle = new TextWidget(tabText, pointSize: pointSize, textColor: textColor);
			tabTitle.AutoExpandBoundsToText = true;
			leftToRight.AddChild(tabTitle);

			ImageBuffer popOutImageClick = StaticData.Instance.LoadIcon("icon_pop_out_32x32.png", 16, 16);
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				popOutImageClick.InvertLightness();
			}

			ImageBuffer popOutImage = new ImageBuffer(popOutImageClick);
			byte[] buffer = popOutImage.GetBuffer();
			for (int i = 0; i < buffer.Length; i++)
			{
				if ((i & 3) != 3)
				{
					buffer[i] = textColor.red;
				}
			}

			popOutButton = new Button(0, 0, new ButtonViewStates(new ImageWidget(popOutImage), new ImageWidget(popOutImage), new ImageWidget(popOutImageClick), new ImageWidget(popOutImageClick)));
			popOutButton.ToolTipText = "Pop This Tab out into its own Window".Localize();
			popOutButton.Click += (sender, e) =>
			{
				popOutManager.ShowContentInWindow();
			};
			popOutButton.Margin = new BorderDouble(3, 0);
			popOutButton.VAnchor = VAnchor.ParentTop;
			leftToRight.AddChild(popOutButton);

			widgetState.AddChild(leftToRight);
			widgetState.BackgroundColor = backgroundColor;

			EnforceSizingAdornActive(widgetState, isActive);
		}
	}
}