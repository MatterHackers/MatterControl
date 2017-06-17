/*
Copyright (c) 2017, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class PrinterSelector : DropDownList
	{
		public event EventHandler AddPrinter;

		private EventHandler unregisterEvents;
		int lastSelectedIndex = -1;

		public PrinterSelector() : base("Printers".Localize() + "... ")
		{
			Rebuild();

			this.Name = "Printers... Menu";

			this.SelectionChanged += (s, e) =>
			{
				string printerID = this.SelectedValue;
				if (printerID == "new" 
					|| string.IsNullOrEmpty(printerID) 
					|| printerID == ActiveSliceSettings.Instance.ID)
				{
					// do nothing
				}
				else
				{
					if (PrinterConnection.Instance.PrinterIsPrinting
						|| PrinterConnection.Instance.PrinterIsPaused)
					{
						if (this.SelectedIndex != lastSelectedIndex)
						{
							UiThread.RunOnIdle(() =>
							StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't switch printers while printing".Localize())
							);
							this.SelectedIndex = lastSelectedIndex;
						}
					}
					else
					{
						lastSelectedIndex = this.SelectedIndex;
						UiThread.RunOnIdle(() => ActiveSliceSettings.SwitchToProfile(printerID));
					}
				}
			};

			ActiveSliceSettings.SettingChanged.RegisterEvent(SettingChanged, ref unregisterEvents);

			// Rebuild the droplist any time the Profiles list changes
			ProfileManager.ProfilesListChanged.RegisterEvent((s, e) => Rebuild(), ref unregisterEvents);
		}

		public void Rebuild()
		{
			this.MenuItems.Clear();

			//Add the menu items to the menu itself
			foreach (var printer in ProfileManager.Instance.ActiveProfiles.OrderBy(p => p.Name))
			{
				this.AddItem(printer.Name, printer.ID.ToString());
			}

			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				this.SelectedValue = ActiveSliceSettings.Instance.ID;
				lastSelectedIndex = this.SelectedIndex;
				this.mainControlText.Text = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
			}

			var menuItem = this.AddItem(StaticData.Instance.LoadIcon("icon_plus.png", 32, 32), "Add New Printer".Localize() + "...", "new");
			menuItem.CanHeldSelection = false;
			menuItem.Click += (s, e) =>
			{
				if (AddPrinter != null)
				{
					if (PrinterConnection.Instance.PrinterIsPrinting
						|| PrinterConnection.Instance.PrinterIsPaused)
					{
						UiThread.RunOnIdle(() =>
							StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize())
						);
					}
					else
					{
						UiThread.RunOnIdle(() => AddPrinter(this, null));
					}
				}
			};
		}

		private void SettingChanged(object sender, EventArgs e)
		{
			string settingsName = (e as StringEventArgs)?.Data;
			if (settingsName != null && settingsName == SettingsKey.printer_name)
			{
				if (ProfileManager.Instance.ActiveProfile != null)
				{
					ProfileManager.Instance.ActiveProfile.Name = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
					Rebuild();
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}