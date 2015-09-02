/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class ThirdPanelTabView : GuiWidget
	{
		private event EventHandler unregisterEvents;

		private Button advancedControlsBackButton;
		private SliceSettingsWidget sliceSettingsWidget;
		private EventHandler AdvancedControlsButton_Click;

		private TabControl advancedControls2;

		public ThirdPanelTabView(EventHandler AdvancedControlsButton_Click = null)
		{
			this.AdvancedControlsButton_Click = AdvancedControlsButton_Click;

			advancedControls2 = CreateNewAdvancedControls(AdvancedControlsButton_Click);

			AddChild(advancedControls2);

			ApplicationController.Instance.ReloadAdvancedControlsPanelTrigger.RegisterEvent(ReloadAdvancedControlsPanelTrigger, ref unregisterEvents);

			AnchorAll();
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public void ReloadAdvancedControlsPanelTrigger(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(ReloadSliceSettings);
		}

		private static readonly string ThirdPanelTabView_AdvancedControls_CurrentTab = "ThirdPanelTabView_AdvancedControls_CurrentTab";

		private TabControl CreateNewAdvancedControls(EventHandler AdvancedControlsButton_Click)
		{
			TabControl advancedControls = new TabControl();

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			advancedControls.TabBar.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
			advancedControls.TabBar.Margin = new BorderDouble(0, 0);
			advancedControls.TabBar.Padding = new BorderDouble(0, 2);

			int textSize = 16;

			if (AdvancedControlsButton_Click != null)
			{
				// this means we are in compact view and so we will make the tabs text a bit smaller
				textSize = 14;
				TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
				advancedControlsButtonFactory.fontSize = 14;
				advancedControlsButtonFactory.invertImageLocation = false;
				advancedControlsBackButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Back"), "icon_arrow_left_32x32.png");
				advancedControlsBackButton.ToolTipText = "Switch to Queue, Library and History".Localize();
				advancedControlsBackButton.Margin = new BorderDouble(right: 3);
				advancedControlsBackButton.VAnchor = VAnchor.ParentBottom;
				advancedControlsBackButton.Cursor = Cursors.Hand;
				advancedControlsBackButton.Click += new EventHandler(AdvancedControlsButton_Click);

				advancedControls.TabBar.AddChild(advancedControlsBackButton);
			}

			GuiWidget hSpacer = new GuiWidget();
			hSpacer.HAnchor = HAnchor.ParentLeftRight;

			advancedControls.TabBar.AddChild(hSpacer);

			GuiWidget manualPrinterControls = new ManualPrinterControls();
			ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
			manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			manualPrinterControlsScrollArea.AnchorAll();
			manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

			RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

			//Add the tab contents for 'Advanced Controls'
			string sliceSettingsLabel = LocalizedString.Get("Settings").ToUpper();
			string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
			sliceSettingsWidget = new SliceSettingsWidget();

			TabPage sliceSettingsTabPage = new TabPage(sliceSettingsWidget, sliceSettingsLabel);
			PopOutTextTabWidget sliceSettingPopOut = new PopOutTextTabWidget(sliceSettingsTabPage, SliceSettingsTabName, new Vector2(590, 400), textSize);
			advancedControls.AddTab(sliceSettingPopOut);
			
			TabPage controlsTabPage = new TabPage(manualPrinterControlsScrollArea, printerControlsLabel);
			PopOutTextTabWidget controlsPopOut = new PopOutTextTabWidget(controlsTabPage, ControlsTabName, new Vector2(400, 300), textSize);
			advancedControls.AddTab(controlsPopOut);

#if !__ANDROID__
			MenuOptionSettings.sliceSettingsPopOut = sliceSettingPopOut;
			MenuOptionSettings.controlsPopOut = controlsPopOut;
#endif

			string optionsLabel = LocalizedString.Get("Options").ToUpper();
			ScrollableWidget optionsControls = new PrinterConfigurationScrollWidget();
			advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(optionsControls, optionsLabel), "Options Tab", textSize,
						ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			// Make sure we are on the right tab when we create this view
			{
				string selectedTab = UserSettings.Instance.get(ThirdPanelTabView_AdvancedControls_CurrentTab);
				advancedControls.SelectTab(selectedTab);

				advancedControls.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.set(ThirdPanelTabView_AdvancedControls_CurrentTab, advancedControls.TabBar.SelectedTabName);
				};
			}

			return advancedControls;
		}

		public static string SliceSettingsTabName
		{
			get { return "Slice Settings Tab"; }
		}

		public static string ControlsTabName
		{
			get { return "Controls Tab"; }
		}

		public void ReloadSliceSettings()
		{
			WidescreenPanel.PreChangePanels.CallEvents(null, null);

			// remove the advance control and replace it with new ones built for the selected printer
			int advancedControlsIndex = GetChildIndex(advancedControls2);
			RemoveChild(advancedControlsIndex);
			advancedControls2.Close();

			if (advancedControlsBackButton != null)
			{
				advancedControlsBackButton.Click -= new EventHandler(AdvancedControlsButton_Click);
			}

			advancedControls2 = CreateNewAdvancedControls(AdvancedControlsButton_Click);
			AddChild(advancedControls2, advancedControlsIndex);

			// This is a hack to make the panel remain on the screen.  It would be great to debug it and understand
			// why it does not work without this code in here.
			//currentParent.Parent.Width = currentParent.Parent.Width + 1;
		}
	}
}