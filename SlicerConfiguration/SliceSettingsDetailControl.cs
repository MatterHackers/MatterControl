using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using MatterHackers.MatterControl.DataStorage;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsDetailControl : FlowLayoutWidget
	{
		private const string SliceSettingsShowHelpEntry = "SliceSettingsShowHelp";
		private const string SliceSettingsLevelEntry = "SliceSettingsLevel";

		private CheckBox showHelpBox;
		private StyledDropDownList settingsDetailSelector;

		public DropDownMenu sliceOptionsMenuDropList;
		private TupleList<string, Func<bool>> slicerOptionsMenuItems;

		public SliceSettingsDetailControl()
		{
			showHelpBox = new CheckBox(0, 0, LocalizedString.Get("Show Help"), textSize: 10);
			showHelpBox.Checked = UserSettings.Instance.get(SliceSettingsShowHelpEntry) == "true";
			// add in the ability to turn on and off help text
			{
				showHelpBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				showHelpBox.Margin = new BorderDouble(right: 3);
				showHelpBox.VAnchor = VAnchor.ParentCenter;
				showHelpBox.Cursor = Cursors.Hand;
				showHelpBox.CheckedStateChanged += RebuildSlicerSettings;

				this.AddChild(showHelpBox);
			}

			settingsDetailSelector = new StyledDropDownList("Basic", maxHeight: 200);
			settingsDetailSelector.Name = "User Level Dropdown";
			settingsDetailSelector.AddItem(LocalizedString.Get("Basic"), "Simple");
			settingsDetailSelector.AddItem(LocalizedString.Get("Standard"), "Intermediate");
			settingsDetailSelector.AddItem(LocalizedString.Get("Advanced"), "Advanced");
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

		private void SetMenuItems()
		{
			string importTxt = LocalizedString.Get("Import");
			string importTxtFull = string.Format("{0}", importTxt);
			string exportTxt = LocalizedString.Get("Export");
			string exportTxtFull = string.Format("{0}", exportTxt);
			//Set the name and callback function of the menu items
			slicerOptionsMenuItems = new TupleList<string, Func<bool>>
			{
				{importTxtFull, ImportQueueMenu_Click},
				{exportTxtFull, ExportQueueMenu_Click},
			};

			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
			{
				sliceOptionsMenuDropList.AddItem(item.Item1);
			}
		}

		private bool ImportQueueMenu_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				ActiveSliceSettings.Instance.LoadSettingsFromIni();
			});
			return true;
		}

		private bool ExportQueueMenu_Click()
		{
			UiThread.RunOnIdle(ActiveSliceSettings.Instance.SaveAs);
			return true;
		}

		private void SettingsDetail_SelectionChanged(object sender, EventArgs e)
		{
			RebuildSlicerSettings(null, null);
		}

		private void RebuildSlicerSettings(object sender, EventArgs e)
		{
			UserSettings.Instance.set(SliceSettingsShowHelpEntry, showHelpBox.Checked.ToString().ToLower());
			UserSettings.Instance.set(SliceSettingsLevelEntry, settingsDetailSelector.SelectedValue);

			ApplicationController.Instance.ReloadAdvancedControlsPanel();
		}

		public string SelectedValue
		{
			get { return settingsDetailSelector.SelectedValue; }
		}

		public bool ShowingHelp
		{
			get { return showHelpBox.Checked; }
		}
	}

}
