/*
Copyright (c) 2017, Lars Brubaker, John Lewin

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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SetupWizard;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsDetailControl : FlowLayoutWidget
	{
		private const string SliceSettingsLevelEntry = "SliceSettingsLevel";
		private const string SliceSettingsShowHelpEntry = "SliceSettingsShowHelp";
		private DropDownList settingsDetailSelector;
		private CheckBox showHelpBox;
		private List<NamedAction> slicerOptionsMenuItems;

		string resetToDefaultsMessage = "Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize();
		string resetToDefaultsWindowTitle = "Revert Settings".Localize();
		bool primarySettingsView;

		private SliceSettingsWidget sliceSettingsWidget;

		public SliceSettingsDetailControl(List<PrinterSettingsLayer> layerCascade, SliceSettingsWidget sliceSettingsWidget)
		{
			this.sliceSettingsWidget = sliceSettingsWidget;

			primarySettingsView = layerCascade == null;
			settingsDetailSelector = new DropDownList("Basic", maxHeight: 200);
			settingsDetailSelector.Name = "User Level Dropdown";
			settingsDetailSelector.AddItem("Basic".Localize(), "Simple");
			settingsDetailSelector.AddItem("Standard".Localize(), "Intermediate");
			settingsDetailSelector.AddItem("Advanced".Localize(), "Advanced");

			if (primarySettingsView)
			{
				// set to the user requested value when in default view
				if (UserSettings.Instance.get(SliceSettingsLevelEntry) != null
					&& SliceSettingsOrganizer.Instance.UserLevels.ContainsKey(UserSettings.Instance.get(SliceSettingsLevelEntry)))
				{
					settingsDetailSelector.SelectedValue = UserSettings.Instance.get(SliceSettingsLevelEntry);
				}
			}
			else // in settings editor view
			{
				// set to advanced
				settingsDetailSelector.SelectedValue = "Advanced";
			}

			settingsDetailSelector.SelectionChanged += (s, e) => RebuildSlicerSettings(null, null);
			settingsDetailSelector.VAnchor = VAnchor.ParentCenter;
			settingsDetailSelector.Margin = new BorderDouble(5, 3);
			settingsDetailSelector.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100);

			if (primarySettingsView)
			{
				// only add these in the default view
				this.AddChild(settingsDetailSelector);
				this.AddChild(GetSliceOptionsMenuDropList());
			}

			VAnchor = VAnchor.ParentCenter;
		}

		public event EventHandler ShowHelpChanged;

		public string SelectedValue => settingsDetailSelector.SelectedValue; 

		public bool ShowingHelp => primarySettingsView ? showHelpBox.Checked : false;

		private DropDownMenu GetSliceOptionsMenuDropList()
		{
			DropDownMenu sliceOptionsMenuDropList;
			sliceOptionsMenuDropList = new DropDownMenu("Options".Localize() + "... ")
			{
				HoverColor = new RGBA_Bytes(0, 0, 0, 50),
				NormalColor = new RGBA_Bytes(0, 0, 0, 0),
				BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100),
				BackgroundColor = new RGBA_Bytes(0, 0, 0, 0),
				BorderWidth = 1,
				MenuAsWideAsItems = false,
				AlignToRightEdge = true,
			};
			sliceOptionsMenuDropList.Name = "Slice Settings Options Menu";
			sliceOptionsMenuDropList.VAnchor |= VAnchor.ParentCenter;


			showHelpBox = new CheckBox("Show Help".Localize());

			if (primarySettingsView)
			{
				// only turn on the help if in the main view and it is set to on
				showHelpBox.Checked = UserSettings.Instance.get(SliceSettingsShowHelpEntry) == "true";
			}

			showHelpBox.CheckedStateChanged += (s, e) =>
			{
				if (primarySettingsView)
				{
					// only save the help settings if in the main view
					UserSettings.Instance.set(SliceSettingsShowHelpEntry, showHelpBox.Checked.ToString().ToLower());
				}
				ShowHelpChanged?.Invoke(this, null);
			};

			MenuItem showHelp = new MenuItem(showHelpBox, "Show Help Checkbox")
			{
				Padding = sliceOptionsMenuDropList.MenuItemsPadding,
			};
			sliceOptionsMenuDropList.MenuItems.Add(showHelp);
			sliceOptionsMenuDropList.AddHorizontalLine();

			sliceOptionsMenuDropList.AddItem("Import".Localize()).Selected += (s, e) => { ImportSettingsMenu_Click(); };
			sliceOptionsMenuDropList.AddItem("Export".Localize()).Selected += (s, e) => { WizardWindow.Show<ExportSettingsPage>("ExportSettingsPage", "Export Settings"); };

			MenuItem settingsHistory = sliceOptionsMenuDropList.AddItem("Restore Settings".Localize());
			settingsHistory.Selected += (s, e) => { WizardWindow.Show<PrinterProfileHistoryPage>("PrinterProfileHistory", "Restore Settings"); };

			settingsHistory.Enabled = !string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername);

			sliceOptionsMenuDropList.AddItem("Reset to Defaults".Localize()).Selected += (s, e) => { UiThread.RunOnIdle(ResetToDefaults); };

			return sliceOptionsMenuDropList;
		}

		private bool ImportSettingsMenu_Click()
		{
			UiThread.RunOnIdle(() => WizardWindow.Show<ImportSettingsPage>("ImportSettingsPage", "Import Settings Page"));
			return true;
		}

		private void RebuildSlicerSettings(object sender, EventArgs e)
		{
			UserSettings.Instance.set(SliceSettingsLevelEntry, settingsDetailSelector.SelectedValue);
			sliceSettingsWidget.RebuildSliceSettingsTabs();
		}

		private void ResetToDefaults()
		{
			StyledMessageBox.ShowMessageBox(
				revertSettings =>
				{
					if (revertSettings)
					{
						bool onlyReloadSliceSettings = true;
						if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
						&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
						{
							onlyReloadSliceSettings = false;
						}
					
						ActiveSliceSettings.Instance.ClearUserOverrides();
						ActiveSliceSettings.Instance.Save();

						if (onlyReloadSliceSettings)
						{
							ApplicationController.Instance.ReloadAdvancedControlsPanel();
						}
						else
						{
							ApplicationController.Instance.ReloadAll();
						}
					}
				},
				resetToDefaultsMessage,
				resetToDefaultsWindowTitle,
				StyledMessageBox.MessageType.YES_NO);
		}
	}
}