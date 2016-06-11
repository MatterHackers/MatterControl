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
using System.IO;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl
{
	public class ImportSettingsPage : WizardPage
	{
		private string importMode;

		public ImportSettingsPage() :
			base("Cancel")
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			contentRow.AddChild(container);

			var newPrinterButton = new RadioButton("Import as new printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			newPrinterButton.CheckedStateChanged += (s, e) => importMode = "new";
			newPrinterButton.Checked = true;
			container.AddChild(newPrinterButton);
			this.importMode = "new";

			var mergeButton = new RadioButton("Merge into current printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			mergeButton.CheckedStateChanged += (s, e) => importMode = "merge";
			container.AddChild(mergeButton);

			var replaceButton = new RadioButton("Replace current printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
			replaceButton.CheckedStateChanged += (s, e) => importMode = "replace";
			container.AddChild(replaceButton);

			var importButton = textImageButtonFactory.Generate("Import Settings".Localize());
			importButton.Click += (s, e) => UiThread.RunOnIdle(() => 
			{
				FileDialog.OpenFileDialog(
						new OpenFileDialogParams("settings files|*.ini;*.printer,"),
						(dialogParams) => ImportSettingsFile(dialogParams.FileName));
			});

			importButton.Visible = true;
			cancelButton.Visible = true;

			//Add buttons to buttonContainer
			footerRow.AddChild(importButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private void ImportSettingsFile(string settingsFilePath)
		{
			WizardWindow.Close();

			switch (importMode)
			{
				case "new":
					ActiveSliceSettings.ImportFromExisting(settingsFilePath);
					break;

				case "merge":
				case "replace":
					ReplaceOrMergeSettings(settingsFilePath);
					break;
			}
		}

		private void ReplaceOrMergeSettings(string settingsFilePath)
		{
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ".printer":
						throw new NotImplementedException("need to import from 'MatterControl.printer' files");
						break;

					case ".ini":
						var settingsToImport = SettingsLayer.LoadFromIni(settingsFilePath);
						string layerHeight;

						bool isSlic3r = settingsToImport.TryGetValue("layer_height", out layerHeight);
						if (isSlic3r)
						{
							var activeSettings = ActiveSliceSettings.Instance;

							if (importMode == "replace")
							{
								activeSettings.ClearUserOverrides();
							}

							foreach (var item in settingsToImport)
							{
								// Compare the value to import to the layer cascade value and only set if different
								string currentValue = activeSettings.GetActiveValue(item.Key, null).Trim();
								if (currentValue != item.Value)
								{
									activeSettings.UserLayer[item.Key] = item.Value;
								}
							}

							activeSettings.SaveChanges();

							UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
						}
						else
						{
							// looks like a cura file
							throw new NotImplementedException("need to import from 'cure.ini' files");
						}
						break;

					default:
						// Did not figure out what this file is, let the user know we don't understand it
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to recognize settings file '{0}'.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
						break;
				}

			}
			Invalidate();
		}
	}
}
