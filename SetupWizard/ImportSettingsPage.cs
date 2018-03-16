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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class SelectPartsOfPrinterToImport : DialogPage
	{
		private string importMessage = "Select what you would like to merge into your current profile.".Localize();

		private string settingsFilePath;
		private PrinterSettings settingsToImport;
		private int selectedMaterial = -1;
		private int selectedQuality = -1;

		public SelectPartsOfPrinterToImport(string settingsFilePath)
		{
			this.WindowTitle = "Import Wizard";
			this.HeaderText = "Select What to Import".Localize();

			// TODO: Need to handle load failures for import attempts
			settingsToImport = PrinterSettings.LoadFile(settingsFilePath);

			// if there are no settings to import
			if (settingsToImport.QualityLayers.Count == 0 && settingsToImport.MaterialLayers.Count == 0)
			{
				// Only main setting so don't ask what to merge just do it.
				UiThread.RunOnIdle(() =>
				{
					DisplayFailedToImportMessage(settingsFilePath);
					this.Parents<SystemWindow>().First().Close();
				});
			}

			this.settingsFilePath = settingsFilePath;

			var scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(scrollWindow);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			scrollWindow.AddChild(container);

			if (settingsToImport.QualityLayers.Count > 0)
			{
				container.AddChild(new TextWidget("Quality Presets:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 3, 0, 15),
				});

				int buttonIndex = 0;
				foreach (var qualitySetting in settingsToImport.QualityLayers)
				{
					RadioButton qualityButton = new RadioButton(string.IsNullOrEmpty(qualitySetting.Name) ? "no name" : qualitySetting.Name)
					{
						TextColor = ActiveTheme.Instance.PrimaryTextColor,
						Margin = new BorderDouble(5, 0, 0, 0),
						HAnchor = HAnchor.Left,
					};
					container.AddChild(qualityButton);

					int localButtonIndex = buttonIndex;
					qualityButton.CheckedStateChanged += (s, e) =>
					{
						if (qualityButton.Checked)
						{
							selectedQuality = localButtonIndex;
						}
						else
						{
							selectedQuality = -1;
						}
					};

					buttonIndex++;
				}
			}

			if (settingsToImport.MaterialLayers.Count > 0)
			{
				container.AddChild(new TextWidget("Material Presets:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 3, 0, 15),
				});

				int buttonIndex = 0;
				foreach (var materialSetting in settingsToImport.MaterialLayers)
				{
					RadioButton materialButton = new RadioButton(string.IsNullOrEmpty(materialSetting.Name) ? "no name" : materialSetting.Name)
					{
						TextColor = ActiveTheme.Instance.PrimaryTextColor,
						Margin = new BorderDouble(5, 0),
						HAnchor = HAnchor.Left,
					};

					container.AddChild(materialButton);

					int localButtonIndex = buttonIndex;
					materialButton.CheckedStateChanged += (s, e) =>
					{
						if (materialButton.Checked)
						{
							selectedMaterial = localButtonIndex;
						}
						else
						{
							selectedMaterial = -1;
						}
					};

					buttonIndex++;
				}
			}

			var importButtonTitle = "Import".Localize();
			var mergeButton = textImageButtonFactory.Generate(importButtonTitle);
			mergeButton.Name = "Merge Profile";
			mergeButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				bool copyName = false;
				PrinterSettingsLayer sourceLayer = null;
				bool destIsMaterial = true;
				if (selectedMaterial > -1)
				{
					sourceLayer = settingsToImport.MaterialLayers[selectedMaterial];
					copyName = true;
				}
				else if (selectedQuality > -1)
				{
					destIsMaterial = false;
					sourceLayer = settingsToImport.QualityLayers[selectedQuality];
					copyName = true;
				}

				List<PrinterSettingsLayer> sourceFilter;

				if (selectedQuality == -1 && selectedMaterial == -1)
				{
					sourceFilter = new List<PrinterSettingsLayer>()
					{
						settingsToImport.OemLayer,
						settingsToImport.UserLayer
					};
				}
				else
				{
					sourceFilter = new List<PrinterSettingsLayer>()
					{
						sourceLayer
					};
				}

				PrinterSettingsLayer printerSettingsLayer = ImportSettings(destIsMaterial, settingsFilePath);
				if (printerSettingsLayer != null)
				{
					ActiveSliceSettings.Instance.Merge(printerSettingsLayer, settingsToImport, sourceFilter, copyName);

					var layerName = (printerSettingsLayer.ContainsKey(SettingsKey.layer_name)) ? printerSettingsLayer[SettingsKey.layer_name] : "none";
					Success(settingsFilePath, layerName, destIsMaterial ? "Material".Localize() : "Quality".Localize());
				}
				else
				{
					UiThread.RunOnIdle(() =>
					{
						DisplayFailedToImportMessage(settingsFilePath);
						this.Parents<SystemWindow>().First().Close();
					});
				}
			});

			this.AddPageAction(mergeButton);
		}

		private void Success(string settingsFilePath, string sourceName, string sectionName)
		{
			string importSettingSuccessMessage = $"You have successfully imported a new {sectionName} setting. You can find '{sourceName}' in your list of {sectionName} settings.".Localize();

			WizardWindow.ChangeToPage(new ImportSucceeded(importSettingSuccessMessage)
			{
				WizardWindow = this.WizardWindow,
			});
		}

		private static void DisplayFailedToImportMessage(string settingsFilePath)
		{
			StyledMessageBox.ShowMessageBox("Oops! Settings file '{0}' did not contain any settings we could import.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
		}

		public PrinterSettingsLayer ImportSettings(bool destIsMaterial, string settingsFilePath)
		{
			PrinterSettingsLayer printerSettingsLayer = null;
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ProfileManager.ProfileExtension:
						printerSettingsLayer = new PrinterSettingsLayer();
						printerSettingsLayer[SettingsKey.layer_name] = Path.GetFileNameWithoutExtension(settingsFilePath);

						if (destIsMaterial)
						{
							ActiveSliceSettings.Instance.MaterialLayers.Add(printerSettingsLayer);
						}
						else
						{
							ActiveSliceSettings.Instance.QualityLayers.Add(printerSettingsLayer);
						}
						break;

					default:
						// Did not figure out what this file is, let the user know we don't understand it
						StyledMessageBox.ShowMessageBox("Oops! Unable to recognize settings file '{0}'.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
						break;
				}
			}

			return printerSettingsLayer;
		}
	}

	public class ImportSucceeded : DialogPage
	{
		public ImportSucceeded(string successMessage) :
			base("Done".Localize())
		{
			this.WindowTitle = "Import Wizard".Localize();
			this.HeaderText = "Import Successful".Localize();

			contentRow.AddChild(new WrappedTextWidget(successMessage, textColor: ActiveTheme.Instance.PrimaryTextColor));
		}
	}
}