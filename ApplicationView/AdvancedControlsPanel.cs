/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using MatterHackers.MatterControl.PrintLibrary;

namespace MatterHackers.MatterControl
{
	public class AdvancedControlsPanel : GuiWidget
	{
		private static readonly string ThirdPanelTabView_AdvancedControls_CurrentTab = "ThirdPanelTabView_AdvancedControls_CurrentTab";

		private EventHandler unregisterEvents;

		private Button backButton;
		private GuiWidget sliceSettingsWidget;

		public event EventHandler BackClicked;

		private TabControl advancedTab;

		public AdvancedControlsPanel()
		{
			advancedTab = CreateAdvancedControlsTab();
			AddChild(advancedTab);
			AnchorAll();

			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => UiThread.RunOnIdle(ReloadSliceSettings), ref unregisterEvents);
		}

		public static string SliceSettingsTabName { get; } = "Slice Settings Tab";

		public static string ControlsTabName { get; } = "Controls Tab";

		public void ReloadSliceSettings()
		{
			WidescreenPanel.PreChangePanels.CallEvents(null, null);
			if (advancedTab.HasBeenClosed)
			{
				return;
			}

			PopOutManager.SaveIfClosed = false;
			// remove the advance control and replace it with new ones built for the selected printer
			int advancedControlsIndex = GetChildIndex(advancedTab);
			RemoveChild(advancedControlsIndex);
			advancedTab.Close();

			advancedTab = CreateAdvancedControlsTab();
			AddChild(advancedTab, advancedControlsIndex);
			PopOutManager.SaveIfClosed = true;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private TabControl CreateAdvancedControlsTab()
		{
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			var advancedControls = new TabControl();
			advancedControls.TabBar.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
			advancedControls.TabBar.Margin = 0;
			advancedControls.TabBar.Padding = new BorderDouble(0, 12);

			int textSize = 14;

			RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

			var libraryTabPage = new TabPage(new PrintLibraryWidget(), "Library".Localize().ToUpper());
			advancedControls.AddTab(new SimpleTextTabWidget(libraryTabPage, "Library Tab", 15,
					ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				sliceSettingsWidget = new SliceSettingsWidget();
			}
			else
			{
				sliceSettingsWidget = new NoSettingsWidget();
			}

			var sliceSettingPopOut = new PopOutTextTabWidget(
					new TabPage(sliceSettingsWidget, "Settings".Localize().ToUpper()),
					SliceSettingsTabName,
					new Vector2(590, 400),
					textSize);

			var controlsPopOut = new PopOutTextTabWidget(
					new TabPage(new ManualPrinterControls(), "Controls".Localize().ToUpper()),
					ControlsTabName,
					new Vector2(400, 300),
					textSize);

			advancedControls.AddTab(sliceSettingPopOut);
			advancedControls.AddTab(controlsPopOut);
;

#if !__ANDROID__
			if (!UserSettings.Instance.IsTouchScreen)
			{
				MenuOptionSettings.sliceSettingsPopOut = sliceSettingPopOut;
				MenuOptionSettings.controlsPopOut = controlsPopOut;
			}
#endif

			advancedControls.AddTab(
				new SimpleTextTabWidget(
					new TabPage(new PrinterConfigurationScrollWidget(), "Options".Localize().ToUpper()), 
					"Options Tab", 
					textSize,
					ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			/*
			// Make sure we are on the right tab when we create this view
			{
				string selectedTab = UserSettings.Instance.get(ThirdPanelTabView_AdvancedControls_CurrentTab);
				advancedControls.SelectTab(selectedTab);

				advancedControls.TabBar.TabIndexChanged += (sender, e) =>
				{
					string selectedTabName = advancedControls.TabBar.SelectedTabName;
					if (!string.IsNullOrEmpty(selectedTabName))
					{
						UserSettings.Instance.set(ThirdPanelTabView_AdvancedControls_CurrentTab, selectedTabName);
					}
				};
			} */

			// MatterControl historically started with the queue selected, force to 0 to remain consistent
			advancedControls.SelectedTabIndex = 0;

			return advancedControls;
		}
	}
}