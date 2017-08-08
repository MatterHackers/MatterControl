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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class SelectPartsOfPrinterToImport : WizardPage
	{
		private string importMessage = "Select what you would like to merge into your current profile.".Localize();

		private string settingsFilePath;
		private PrinterSettings settingsToImport;
		private int selectedMaterial = -1;
		private int selectedQuality = -1;

		private PrinterSettingsLayer destinationLayer;
		private string sectionName;

		private bool isMergeIntoUserLayer = false;

		public SelectPartsOfPrinterToImport(string settingsFilePath, PrinterSettingsLayer destinationLayer, string sectionName = null) :
			base(unlocalizedTextForTitle: "Import Wizard")
		{
			this.isMergeIntoUserLayer = destinationLayer == ActiveSliceSettings.Instance.UserLayer;
			this.destinationLayer = destinationLayer;
			this.sectionName = sectionName;

			// TODO: Need to handle load failures for import attempts
			settingsToImport = PrinterSettings.LoadFile(settingsFilePath);

			this.headerLabel.Text = "Select What to Import".Localize();

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

			if (isMergeIntoUserLayer)
			{
				container.AddChild(new WrappedTextWidget(importMessage, textColor: ActiveTheme.Instance.PrimaryTextColor));
			}

			// add in the check boxes to select what to import
			container.AddChild(new TextWidget("Main Settings:")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(0, 3, 0, isMergeIntoUserLayer ? 10 : 0),
			});

			var mainProfileRadioButton = new RadioButton("Printer Profile")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0),
				HAnchor = HAnchor.Left,
				Checked = true,
			};
			container.AddChild(mainProfileRadioButton);

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
					RadioButton qualityButton = new RadioButton(qualitySetting.Name)
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
					RadioButton materialButton = new RadioButton(materialSetting.Name)
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

			var mergeButtonTitle = this.isMergeIntoUserLayer ? "Merge".Localize() : "Import".Localize();
			var mergeButton = textImageButtonFactory.Generate(mergeButtonTitle);
			mergeButton.Name = "Merge Profile";
			mergeButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				bool copyName = false;
				PrinterSettingsLayer sourceLayer = null;
				if (selectedMaterial > -1)
				{
					sourceLayer = settingsToImport.MaterialLayers[selectedMaterial];
					copyName = true;
				}
				else if (selectedQuality > -1)
				{
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

				ActiveSliceSettings.Instance.Merge(destinationLayer, settingsToImport, sourceFilter, copyName);

				this.Parents<SystemWindow>().FirstOrDefault()?.CloseOnIdle();
			});

			footerRow.AddChild(mergeButton);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			if (settingsToImport.QualityLayers.Count == 0 && settingsToImport.MaterialLayers.Count == 0)
			{
				// Only main setting so don't ask what to merge just do it.
				UiThread.RunOnIdle(() =>
				{
					var sourceFilter = new List<PrinterSettingsLayer>()
					{
						settingsToImport.OemLayer ?? new PrinterSettingsLayer(),
						settingsToImport.UserLayer ?? new PrinterSettingsLayer()
					};

					ActiveSliceSettings.Instance.Merge(destinationLayer, settingsToImport, sourceFilter, false);
					UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);

					string successMessage = importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath));
					if (!isMergeIntoUserLayer)
					{
						string sourceName = isMergeIntoUserLayer ? Path.GetFileNameWithoutExtension(settingsFilePath) : destinationLayer[SettingsKey.layer_name];
						string importSettingSuccessMessage = "You have successfully imported a new {1} setting. You can find '{0}' in your list of {1} settings.".Localize();
						successMessage = importSettingSuccessMessage.FormatWith(sourceName, sectionName);
					}

					WizardWindow.ChangeToPage(new ImportSucceeded(successMessage)
					{
						WizardWindow = this.WizardWindow,
					});
				});
			}
		}

		private string importPrinterSuccessMessage = "Settings have been merged into your current printer.".Localize();
	}

	public class ImportSucceeded : WizardPage
	{
		public ImportSucceeded(string successMessage) :
			base("Done", "Import Wizard")
		{
			this.headerLabel.Text = "Import Successful".Localize();

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			contentRow.AddChild(container);

			var successMessageWidget = new WrappedTextWidget(successMessage, textColor: ActiveTheme.Instance.PrimaryTextColor);
			container.AddChild(successMessageWidget);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}
	}

	public class ImportSettingsPage : WizardPage
	{
		private RadioButton newPrinterButton;
		private RadioButton mergeButton;
		private RadioButton newQualityPresetButton;
		private RadioButton newMaterialPresetButton;

		public ImportSettingsPage() :
			base("Cancel", "Import Wizard")
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			contentRow.AddChild(container);

			if (true)
			{
				container.AddChild(new TextWidget("Merge Into:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 0, 0, 5),
				});

				// merge into current settings
				mergeButton = new RadioButton("Current".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				mergeButton.Checked = true;
				container.AddChild(mergeButton);

				container.AddChild(new TextWidget("Create New:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 0, 0, 15),
				});

				// add new profile
				newPrinterButton = new RadioButton("Printer".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newPrinterButton);

				// add as quality preset
				newQualityPresetButton = new RadioButton("Quality preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newQualityPresetButton);

				// add as material preset
				newMaterialPresetButton = new RadioButton("Material preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newMaterialPresetButton);
			}
			else
			{
				// add new profile
				newPrinterButton = new RadioButton("Import as new printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				newPrinterButton.Checked = true;
				container.AddChild(newPrinterButton);

				container.AddChild(
					CreateDetailInfo("Add a new printer profile to your list of available printers.\nThis will not change your current settings.")
					);

				// merge into current settings
				mergeButton = new RadioButton("Merge into current printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(mergeButton);

				container.AddChild(
					CreateDetailInfo("Merge settings and presets (if any) into your current profile. \nYou will still be able to revert to the factory settings at any time.")
					);

				// add as quality preset
				newQualityPresetButton = new RadioButton("Import settings as new QUALITY preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newQualityPresetButton);

				container.AddChild(
					CreateDetailInfo("Add new quality preset with the settings from this import.")
					);

				// add as material preset
				newMaterialPresetButton = new RadioButton("Import settings as new MATERIAL preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newMaterialPresetButton);

				container.AddChild(
					CreateDetailInfo("Add new material preset with the settings from this import.")
					);
			}

			var importButton = textImageButtonFactory.Generate("Choose File".Localize());
			importButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				FileDialog.OpenFileDialog(
						new OpenFileDialogParams("settings files|*.ini;*.printer;*.slice"),
						(dialogParams) =>
						{
							if (!string.IsNullOrEmpty(dialogParams.FileName))
							{
								ImportSettingsFile(dialogParams.FileName);
							}
						});
			});

			importButton.Visible = true;
			cancelButton.Visible = true;

			//Add buttons to buttonContainer
			footerRow.AddChild(importButton);
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

		protected string importPrinterSuccessMessage = "You have successfully imported a new printer profile. You can find '{0}' in your list of available printers.".Localize();
		protected string importSettingSuccessMessage = "You have successfully imported a new {1} setting. You can find '{0}' in your list of {1} settings.".Localize();

		private void ImportSettingsFile(string settingsFilePath)
		{
			if (newPrinterButton.Checked)
			{
				if (ProfileManager.ImportFromExisting(settingsFilePath))
				{
					WizardWindow.ChangeToPage(new ImportSucceeded(importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath)))
					{
						WizardWindow = this.WizardWindow,
					});
				}
				else
				{
					displayFailedToImportMessage(settingsFilePath);
				}
			}
			else if (mergeButton.Checked)
			{
				MergeSettings(settingsFilePath);
			}
			else if (newQualityPresetButton.Checked)
			{
				ImportToPreset(settingsFilePath);
			}
			else if (newMaterialPresetButton.Checked)
			{
				ImportToPreset(settingsFilePath);
			}
		}

		private void ImportToPreset(string settingsFilePath)
		{
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				PrinterSettingsLayer newLayer;

				string sectionName = (newMaterialPresetButton.Checked) ? "Material".Localize() : "Quality".Localize();

				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ProfileManager.ProfileExtension:
						newLayer = new PrinterSettingsLayer();
						newLayer[SettingsKey.layer_name] = Path.GetFileNameWithoutExtension(settingsFilePath);

						if (newQualityPresetButton.Checked)
						{
							ActiveSliceSettings.Instance.QualityLayers.Add(newLayer);
						}
						else
						{
							// newMaterialPresetButton.Checked
							ActiveSliceSettings.Instance.MaterialLayers.Add(newLayer);
						}

						// open a wizard to ask what to import to the preset
						WizardWindow.ChangeToPage(new SelectPartsOfPrinterToImport(settingsFilePath, newLayer, sectionName));

						break;

					case ".slice": // legacy presets file extension
					case ".ini":
						var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);

						bool containsValidSetting = false;
						{
							newLayer = new PrinterSettingsLayer();
							newLayer.Name = Path.GetFileNameWithoutExtension(settingsFilePath);

							// Only be the base and oem layers (not the user, quality or material layer)
							var baseAndOEMCascade = new List<PrinterSettingsLayer>
							{
								ActiveSliceSettings.Instance.OemLayer,
								ActiveSliceSettings.Instance.BaseLayer
							};

							foreach (var keyName in PrinterSettings.KnownSettings)
							{
								if (ActiveSliceSettings.Instance.Contains(keyName))
								{
									containsValidSetting = true;
									string currentValue = ActiveSliceSettings.Instance.GetValue(keyName, baseAndOEMCascade).Trim();
									string newValue;
									// Compare the value to import to the layer cascade value and only set if different
									if (settingsToImport.TryGetValue(keyName, out newValue)
										&& currentValue != newValue)
									{
										newLayer[keyName] = newValue;
									}
								}
							}

							if (containsValidSetting)
							{
								if (newMaterialPresetButton.Checked)
								{
									ActiveSliceSettings.Instance.MaterialLayers.Add(newLayer);
								}
								else
								{
									ActiveSliceSettings.Instance.QualityLayers.Add(newLayer);
								}

								ActiveSliceSettings.Instance.Save();

								WizardWindow.ChangeToPage(new ImportSucceeded(importSettingSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath), sectionName))
								{
									WizardWindow = this.WizardWindow,
								});
							}
							else
							{
								displayFailedToImportMessage(settingsFilePath);
							}
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

		private void MergeSettings(string settingsFilePath)
		{
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ProfileManager.ProfileExtension:
						WizardWindow.ChangeToPage(new SelectPartsOfPrinterToImport(settingsFilePath, ActiveSliceSettings.Instance.UserLayer));
						break;

					case ".slice": // old presets format
					case ".ini":
						// create a scope for variables
						{
							var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);

							bool containsValidSetting = false;
							var activeSettings = ActiveSliceSettings.Instance;

							foreach (var keyName in PrinterSettings.KnownSettings)
							{
								if (activeSettings.Contains(keyName))
								{
									containsValidSetting = true;
									string currentValue = activeSettings.GetValue(keyName).Trim();

									string newValue;
									// Compare the value to import to the layer cascade value and only set if different
									if (settingsToImport.TryGetValue(keyName, out newValue)
										&& currentValue != newValue)
									{
										activeSettings.UserLayer[keyName] = newValue;
									}
								}
							}
							if (containsValidSetting)
							{
								activeSettings.Save();

								UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
							}
							else
							{
								displayFailedToImportMessage(settingsFilePath);
							}
							WizardWindow.Close();
						}
						break;

					default:
						WizardWindow.Close();
						// Did not figure out what this file is, let the user know we don't understand it
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to recognize settings file '{0}'.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
						break;
				}
			}
			Invalidate();
		}

		private void displayFailedToImportMessage(string settingsFilePath)
		{
			StyledMessageBox.ShowMessageBox(null, "Oops! Settings file '{0}' did not contain any settings we could import.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
		}
	}
}