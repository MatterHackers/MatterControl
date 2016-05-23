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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrintQueue;
using System;

namespace MatterHackers.MatterControl
{
	public class CompactSlidePanel : SlidePanel
	{
		private event EventHandler unregisterEvents;

		private TabControl mainControlsTabControl;
		public TabPage QueueTabPage;
		public TabPage AboutTabPage;

		private QueueDataView queueDataView;

		const int StandardControlsPanelIndex = 0;
		const int AdvancedControlsPanelIndex = 1;

		private GuiWidget LeftPanel =>  GetPanel(0);

		private GuiWidget RightPanel => GetPanel(1);

		public double TabBarWidth => mainControlsTabControl.Width;

		private static int lastPanelIndexBeforeReload = 0;

		public CompactSlidePanel(QueueDataView queueDataView) : base(2)
		{
			this.queueDataView = queueDataView;

			// do the front panel stuff
			{
				// first add the print progress bar
				this.LeftPanel.AddChild(new PrintProgressBar());

				// construct the main controls tab control
				mainControlsTabControl = new FirstPanelTabView(queueDataView);

				var advancedControlsButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
					hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
					pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
					fontSize = 10,

					disabledTextColor = RGBA_Bytes.LightGray,
					disabledFillColor = ActiveTheme.Instance.PrimaryBackgroundColor,
					disabledBorderColor = ActiveTheme.Instance.PrimaryBackgroundColor,

					invertImageLocation = true
				};

				Button advancedControlsLinkButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Settings\n& Controls"), 
					StaticData.Instance.LoadIcon("icon_arrow_right_32x32.png", 32,32));
				advancedControlsLinkButton.Name = "SettingsAndControls";
				advancedControlsLinkButton.ToolTipText = "Switch to Settings, Controls and Options".Localize();
				advancedControlsLinkButton.Margin = new BorderDouble(right: 3);
				advancedControlsLinkButton.VAnchor = VAnchor.ParentBottom;
				advancedControlsLinkButton.Cursor = Cursors.Hand;
				advancedControlsLinkButton.Click += ToggleActivePanel_Click;

				mainControlsTabControl.TabBar.AddChild(new HorizontalSpacer());
				mainControlsTabControl.TabBar.AddChild(advancedControlsLinkButton);
				mainControlsTabControl.TabBar.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
				mainControlsTabControl.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;

				this.LeftPanel.AddChild(mainControlsTabControl);
			}

			// Right panel
			this.RightPanel.AddChild(new PrintProgressBar());

			var advancedControlsPanel = new AdvancedControlsPanel()
			{
				Name = "For - CompactSlidePanel"
			};
			advancedControlsPanel.BackClicked += ToggleActivePanel_Click;

			this.RightPanel.AddChild(advancedControlsPanel);

			WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentPanelIndex, ref unregisterEvents);

			SetPanelIndexImmediate(lastPanelIndexBeforeReload);
		}

		private void SaveCurrentPanelIndex(object sender, EventArgs e)
		{
			lastPanelIndexBeforeReload = PanelIndex;
		}

		private void ToggleActivePanel_Click(object sender, EventArgs mouseEvent)
		{
			if (this.PanelIndex == StandardControlsPanelIndex)
			{
				this.PanelIndex = AdvancedControlsPanelIndex;
			}
			else
			{
				this.PanelIndex = StandardControlsPanelIndex;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}