/*
Copyright (c) 2016, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.Localizations;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl
{
	public class ExportSettingsPage : WizardPage
	{
		RadioButton matterControlButton;
		RadioButton slic3rButton;
		RadioButton curaButton;

		public ExportSettingsPage() :
			base("Cancel", "Export As")
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			contentRow.AddChild(container);

			if (true)
			{
				// export as matter control
				matterControlButton = new RadioButton("MatterControl".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				matterControlButton.Checked = true;
				container.AddChild(matterControlButton);

				// export as slic3r
				slic3rButton = new RadioButton("Slic3r".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(slic3rButton);

				// export as cura
#if DEBUG
				curaButton = new RadioButton("Cura".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(curaButton);
#endif
			}
			else
			{
				// export as matter control
				matterControlButton = new RadioButton("Export MatterControl settings (*.printer)".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				matterControlButton.Checked = true;
				container.AddChild(matterControlButton);

				// export as slic3r
				slic3rButton = new RadioButton("Export Slic3r settings (*.ini)".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(slic3rButton);

				container.AddChild(
					CreateDetailInfo("Export as a config.ini file that can be read by older versions of Matter Control and Slic3r.")
					);

				// export as cura
#if DEBUG
				curaButton = new RadioButton("Export Cura settings (*.ini)".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(curaButton);

				container.AddChild(
					CreateDetailInfo("Export as a file that can be read by Cura.\nNote: Not all settings are exportable in this format.")
					);
#endif
			}

			var exportButton = textImageButtonFactory.Generate("Export Settings".Localize());
			exportButton.Click += (s, e) => UiThread.RunOnIdle(exportButton_Click);

			exportButton.Visible = true;
			cancelButton.Visible = true;

			//Add buttons to buttonContainer
			footerRow.AddChild(exportButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private GuiWidget CreateDetailInfo(string detailText)
		{
			var wrappedText = new WrappedTextWidget(detailText)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
			};

			var container = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(25, 15, 5, 5),
			};

			container.AddChild(wrappedText);

			return container;
		}

		private void exportButton_Click()
		{
			WizardWindow.Close();

			if (matterControlButton.Checked)
			{
				ActiveSliceSettings.Instance.Helpers.ExportAsMatterControlConfig();
			}
			else if(slic3rButton.Checked)
			{
				ActiveSliceSettings.Instance.Helpers.ExportAsSlic3rConfig();
			}
			else if(curaButton.Checked)
			{
				ActiveSliceSettings.Instance.Helpers.ExportAsCuraConfig();
			}
		}
	}
}
