using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System;

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

			settingsDetailSelector.SelectionChanged += new EventHandler(SettingsDetail_SelectionChanged);
			settingsDetailSelector.VAnchor = VAnchor.ParentCenter;
			settingsDetailSelector.Margin = new BorderDouble(5, 3);
			settingsDetailSelector.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100);

			this.AddChild(settingsDetailSelector);
			this.AddChild(GetSliceOptionsMenuDropList());
		}

		public event EventHandler ShowHelpChanged;

		public string SelectedValue
		{
			get { return settingsDetailSelector.SelectedValue; }
		}

		public bool ShowingHelp
		{
			get { return showHelpBox.Checked; }
		}

		private bool ExportSettingsMenu_Click()
		{
			UiThread.RunOnIdle(ActiveSliceSettings.Instance.SaveAs);
			return true;
		}

		private DropDownMenu GetSliceOptionsMenuDropList()
		{
			if (sliceOptionsMenuDropList == null)
			{
				sliceOptionsMenuDropList = new DropDownMenu("Options".Localize() + "... ");
				sliceOptionsMenuDropList.HoverColor = new RGBA_Bytes(0, 0, 0, 50);
				sliceOptionsMenuDropList.NormalColor = new RGBA_Bytes(0, 0, 0, 0);
				sliceOptionsMenuDropList.BorderColor = new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 100);
				sliceOptionsMenuDropList.BackgroundColor = new RGBA_Bytes(0, 0, 0, 0);
				sliceOptionsMenuDropList.BorderWidth = 1;
				sliceOptionsMenuDropList.VAnchor |= VAnchor.ParentCenter;
				sliceOptionsMenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);

				SetMenuItems();
			}

			return sliceOptionsMenuDropList;
		}

		private bool ImportSettingsMenu_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				// TODO: jlewin
				throw new NotImplementedException();
				// ActiveSliceSettings.Instance.LoadSettingsFromIni();
			});
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

		private bool RestoreAllSettingsMenu_Click()
		{
			string warningMessage = "Resetting to default values will remove your current overrides and restore your original printer settings.\r\nAre you sure you want to continue?";
			StyledMessageBox.ShowMessageBox(
				revertSettings =>
				{
					if (revertSettings)
					{
						// TODO: We should offer to export the settings before the purge
						ActiveSliceSettings.Instance.UserLayer.Clear();
						ActiveSliceSettings.Instance.SaveChanges();
						ApplicationController.Instance.ReloadAdvancedControlsPanel();
					}
				}, 
				warningMessage.Localize(), 
				"Revert Settings".Localize(), 
				StyledMessageBox.MessageType.YES_NO);

			return true;
		}

		private void SetMenuItems()
		{
			//Set the name and callback function of the menu items
			slicerOptionsMenuItems = new TupleList<string, Func<bool>>
			{
				{ "Import".Localize(), ImportSettingsMenu_Click },
				{ "Export".Localize(), ExportSettingsMenu_Click },
				{ "Reset to defaults".Localize(), RestoreAllSettingsMenu_Click },
#if DEBUG
				{ "Bake Overrides".Localize(), BakeOverrides_Click },
#endif
			};

			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
			{
				sliceOptionsMenuDropList.AddItem(item.Item1);
			}
		}

		private bool BakeOverrides_Click()
		{
			var activeSettings = ActiveSliceSettings.Instance;
			foreach(var keyValue in activeSettings.UserLayer)
			{
				activeSettings.OemLayer[keyValue.Key] = keyValue.Value;
			}

			activeSettings.UserLayer.Clear();
			activeSettings.SaveChanges();

			ApplicationController.Instance.ReloadAdvancedControlsPanel();

			return true;
		}

		private void SettingsDetail_SelectionChanged(object sender, EventArgs e)
		{
			RebuildSlicerSettings(null, null);
		}
	}
}