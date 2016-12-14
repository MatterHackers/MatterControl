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

using System;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class EditPrinterSettingsPage : WizardPage
	{
		private EventHandler unregisterEvents = null;

		public EditPrinterSettingsPage()
			: base("Done")
		{
			headerLabel.Text = "Current Settings".Localize();

			textImageButtonFactory.borderWidth = 1;
			textImageButtonFactory.normalBorderColor = RGBA_Bytes.White;

			int tabIndex = 0;
			AddNameSetting(SettingsKey.printer_name, contentRow, ref tabIndex);
			AddNameSetting(SettingsKey.auto_connect, contentRow, ref tabIndex);
			//Check if networked printing is on before displaying comport or ip/port
			if(ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.enable_network_printing))
			{
				AddNameSetting(SettingsKey.ip_address, contentRow, ref tabIndex);
				AddNameSetting(SettingsKey.ip_port, contentRow, ref tabIndex);
			}
			else
			{
				AddNameSetting(SettingsKey.baud_rate, contentRow, ref tabIndex);
				AddNameSetting(SettingsKey.com_port, contentRow, ref tabIndex);
			}

			contentRow.AddChild(new VerticalSpacer());

			contentRow.AddChild(SliceSettingsWidget.CreatePrinterExtraControls());

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			cancelButton.Text = "Back".Localize();

			// Close this form if the active printer changes
			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) => 
			{
				if (!WizardWindow.HasBeenClosed)
				{
					UiThread.RunOnIdle(WizardWindow.Close);
				}
			}, ref unregisterEvents);
		}

		private void AddNameSetting(string sliceSettingsKey, FlowLayoutWidget contentRow, ref int tabIndex)
		{
			GuiWidget control = SliceSettingsWidget.CreateSettingControl(sliceSettingsKey, ref tabIndex);
			if (control != null)
			{
				contentRow.AddChild(control);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);	
		}
	}
}
