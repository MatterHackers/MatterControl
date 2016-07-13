using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SetupWizard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsDetailControl : FlowLayoutWidget
	{
		public DropDownMenu sliceOptionsMenuDropList;
		private const string SliceSettingsLevelEntry = "SliceSettingsLevel";
		private const string SliceSettingsShowHelpEntry = "SliceSettingsShowHelp";
		private DropDownList settingsDetailSelector;
		private CheckBox showHelpBox;
		private TupleList<string, Func<bool>> slicerOptionsMenuItems;

		private static string resetToDefaultsMessage = "Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize();
		private static string resetToDefaultsWindowTitle = "Revert Settings".Localize();

		public SliceSettingsDetailControl()
		{
			showHelpBox = new CheckBox(0, 0, "Show Help".Localize(), textSize: 10);
			showHelpBox.Checked = UserSettings.Instance.get(SliceSettingsShowHelpEntry) == "true";
			// add in the ability to turn on and off help text
			showHelpBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			showHelpBox.Margin = new BorderDouble(right: 3);
			showHelpBox.VAnchor = VAnchor.ParentCenter;
			showHelpBox.Cursor = Cursors.Hand;
			showHelpBox.CheckedStateChanged += (s, e) =>
			{
				UserSettings.Instance.set(SliceSettingsShowHelpEntry, showHelpBox.Checked.ToString().ToLower());
				ShowHelpChanged?.Invoke(this, null);
			};

			this.AddChild(showHelpBox);

			settingsDetailSelector = new DropDownList("Basic", maxHeight: 200);
			settingsDetailSelector.Name = "User Level Dropdown";
			settingsDetailSelector.AddItem("Basic".Localize(), "Simple");
			settingsDetailSelector.AddItem("Standard".Localize(), "Intermediate");
			settingsDetailSelector.AddItem("Advanced".Localize(), "Advanced");
			if (UserSettings.Instance.get(SliceSettingsLevelEntry) != null
				&& SliceSettingsOrganizer.Instance.UserLevels.ContainsKey(UserSettings.Instance.get(SliceSettingsLevelEntry)))
			{
				settingsDetailSelector.SelectedValue = UserSettings.Instance.get(SliceSettingsLevelEntry);
			}

			settingsDetailSelector.SelectionChanged += (s, e) => RebuildSlicerSettings(null, null); ;
			settingsDetailSelector.VAnchor = VAnchor.ParentCenter;
			settingsDetailSelector.Margin = new BorderDouble(5, 3);
			settingsDetailSelector.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100);

			this.AddChild(settingsDetailSelector);
			this.AddChild(GetSliceOptionsMenuDropList());
		}

		public event EventHandler ShowHelpChanged;

		public string SelectedValue => settingsDetailSelector.SelectedValue; 

		public bool ShowingHelp => showHelpBox.Checked; 

		private DropDownMenu GetSliceOptionsMenuDropList()
		{
			if (sliceOptionsMenuDropList == null)
			{
				sliceOptionsMenuDropList = new DropDownMenu("Options".Localize() + "... ")
				{
					HoverColor = new RGBA_Bytes(0, 0, 0, 50),
					NormalColor = new RGBA_Bytes(0, 0, 0, 0),
					BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100),
					BackgroundColor = new RGBA_Bytes(0, 0, 0, 0),
					BorderWidth = 1,
				};
				sliceOptionsMenuDropList.VAnchor |= VAnchor.ParentCenter;
				sliceOptionsMenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);

				//Set the name and callback function of the menu items
				slicerOptionsMenuItems = new TupleList<string, Func<bool>>
				{
					{ "Import".Localize(), ImportSettingsMenu_Click },
					{ "Export".Localize(), () => {  WizardWindow.Show<ExportSettingsPage>("ExportSettingsPage", "Export Settings"); return true; } },
					{ "Printer History".Localize(), () => { WizardWindow.Show<PrinterProfileHistoryPage>("somecontext", "Hello world"); return true; } },
					{ "Reset to defaults".Localize(),() => { UiThread.RunOnIdle(ResetToDefaults); return true; } },
				};

				//Add the menu items to the menu itself
				foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
				{
					sliceOptionsMenuDropList.AddItem(item.Item1);
				}
			}

			return sliceOptionsMenuDropList;
		}

		private bool ImportSettingsMenu_Click()
		{
			UiThread.RunOnIdle(() => WizardWindow.Show<ImportSettingsPage>("ImportSettingsPage", "Import Settings Page"));
			return true;
		}

		private void MenuDropList_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
			{
				// if the menu we select is this one
				if (item.Item1 == menuSelection)
				{
					// call its function
					item.Item2();
				}
			}
		}

		private void RebuildSlicerSettings(object sender, EventArgs e)
		{
			UserSettings.Instance.set(SliceSettingsLevelEntry, settingsDetailSelector.SelectedValue);
			ApplicationController.Instance.ReloadAdvancedControlsPanel();
		}

		private void ResetToDefaults()
		{
			StyledMessageBox.ShowMessageBox(
				revertSettings =>
				{
					if (revertSettings)
					{
						ActiveSliceSettings.Instance.ClearUserOverrides();
						ActiveSliceSettings.Instance.SaveChanges();
						ApplicationController.Instance.ReloadAdvancedControlsPanel();
					}
				},
				resetToDefaultsMessage,
				resetToDefaultsWindowTitle,
				StyledMessageBox.MessageType.YES_NO);
		}

	}
}