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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SetupWizard;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsOverflowMenu : OverflowMenu
	{
		public SliceSettingsOverflowMenu(PrinterConfig printer, SliceSettingsWidget sliceSettingsWidget)
		{
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.AlignToRightEdge = true;
			this.Name = "Slice Settings Overflow Menu";

			this.PopupContent = GenerateMenuContents(printer, sliceSettingsWidget);
		}

		private FlowLayoutWidget GenerateMenuContents(PrinterConfig printer, SliceSettingsWidget sliceSettingsWidget)
		{
			var popupMenu = new PopupMenu();

			var showHelpBox = new CheckBox("Show Help".Localize());
			showHelpBox.Checked = sliceSettingsWidget.ShowHelpControls;
			showHelpBox.CheckedStateChanged += (s, e) =>
			{
				sliceSettingsWidget.ShowHelpControls = showHelpBox.Checked;
				sliceSettingsWidget.RebuildSliceSettingsTabs();
			};


			popupMenu.AddChild(new MenuItem(showHelpBox, "Show Help Checkbox")
			{
				Padding = PopupMenu.MenuPadding,
			});

			popupMenu.CreateHorizontalLine();

			MenuItem menuItem;

			menuItem = popupMenu.CreateMenuItem("Export".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<ExportSettingsPage>();
			};

			menuItem = popupMenu.CreateMenuItem("Restore Settings".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<PrinterProfileHistoryPage>();
			};
			menuItem.Enabled = !string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername);

			menuItem = popupMenu.CreateMenuItem("Reset to Defaults".Localize());
			menuItem.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(
						revertSettings =>
						{
							if (revertSettings)
							{
								bool onlyReloadSliceSettings = true;
								if (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
								&& printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
								{
									onlyReloadSliceSettings = false;
								}

								printer.Settings.ClearUserOverrides();
								printer.Settings.Save();

								if (onlyReloadSliceSettings)
								{
									printer?.Bed.GCodeRenderer?.Clear3DGCode();
								}
								else
								{
									ApplicationController.Instance.ReloadAll();
								}
							}
						},
						"Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize(),
						"Revert Settings".Localize(),
						StyledMessageBox.MessageType.YES_NO);
				});
			};

			popupMenu.CreateHorizontalLine();

			popupMenu.AddChild(new TextWidget("Mode")
			{
				Margin = new BorderDouble(35, 2, 8, 8),
				TextColor = Color.Gray
			});

			var modeSelector = new SettingsModeSelector()
			{
				SelectedValue = sliceSettingsWidget.UserLevel,
				Name = "User Level Dropdown",
				Margin = new BorderDouble(35, 15, 35, 5),
				BorderColor = new Color(ActiveTheme.Instance.SecondaryTextColor, 100)
			};
			modeSelector.SelectionChanged += (s, e) =>
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsLevel, modeSelector.SelectedValue);
				sliceSettingsWidget.RebuildSliceSettingsTabs();
			};

			popupMenu.AddChild(modeSelector);
			return popupMenu;
		}
	}

	public class SettingsModeSelector : DropDownList, IIgnoredPopupChild
	{
		public SettingsModeSelector()
			: base("Basic")
		{
			this.TextColor = Color.Black;

			this.AddItem("Basic".Localize(), "Simple");
			this.AddItem("Standard".Localize(), "Intermediate");
			this.AddItem("Advanced".Localize(), "Advanced");
		}
	}
}