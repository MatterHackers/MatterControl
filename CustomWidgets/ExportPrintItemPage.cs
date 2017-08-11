using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ExportPrintItemPage : WizardPage
	{
		private CheckBox showInFolderAfterSave;
		private CheckBox applyLeveling;
		private string gcodePathAndFilenameToSave;
		private bool partIsGCode = false;
		private string documentsPath;

		private EventHandler unregisterEvents;

		private Dictionary<RadioButton, IExportPlugin> exportPluginButtons;

		private ILibraryContentStream libraryContent;

		public ExportPrintItemPage(ILibraryContentStream libraryContent)
			: base(unlocalizedTextForTitle: "File export options:")
		{
			partIsGCode = Path.GetExtension(libraryContent.FileName).ToUpper() == ".GCODE";

			this.libraryContent = libraryContent;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Name = "Export Item Window";

			CreateWindowContent();

			// TODO: Why? ***************************************************************************************************
			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((s, e) => CreateWindowContent(), ref unregisterEvents);
		}

		public void CreateWindowContent()
		{
			var commonMargin = new BorderDouble(4, 2);

			// GCode export
			var exportGCode = new RadioButton("Export as".Localize() + " G-Code", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Name = "Export as GCode Button",
				Margin = commonMargin,
				HAnchor = HAnchor.Left,
				Cursor = Cursors.Hand
			};

			bool showExportGCodeButton = ActiveSliceSettings.Instance.PrinterSelected || partIsGCode;
			if (showExportGCodeButton)
			{
				contentRow.AddChild(exportGCode);

				exportPluginButtons = new Dictionary<RadioButton, IExportPlugin>();

				foreach (IExportPlugin plugin in PluginFinder.CreateInstancesOf<IExportPlugin>())
				{
					if (plugin.EnabledForCurrentPart(libraryContent))
					{
						// Create export button for each plugin
						var pluginButton = new RadioButton(plugin.ButtonText.Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor)
						{
							HAnchor = HAnchor.Left,
							Margin = commonMargin,
							Cursor = Cursors.Hand
						};
						contentRow.AddChild(pluginButton);

						exportPluginButtons.Add(pluginButton, plugin);
					}
				}
			}

			contentRow.AddChild(new VerticalSpacer());

			// If print leveling is enabled then add in a check box 'Apply Leveling During Export' and default checked.
			if (showExportGCodeButton && ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				applyLeveling = new CheckBox("Apply leveling to G-Code during export".Localize(), ActiveTheme.Instance.PrimaryTextColor, 10)
				{
					Checked = true,
					HAnchor = HAnchor.Left,
					Cursor = Cursors.Hand
				};
				contentRow.AddChild(applyLeveling);
			}

			// TODO: make this work on the mac and then delete this if
			if (OsInformation.OperatingSystem == OSType.Windows
				|| OsInformation.OperatingSystem == OSType.X11)
			{
				showInFolderAfterSave = new CheckBox("Show file in folder after save".Localize(), ActiveTheme.Instance.PrimaryTextColor, 10)
				{
					HAnchor = HAnchor.Left,
					Cursor = Cursors.Hand
				};
				contentRow.AddChild(showInFolderAfterSave);
			}

			if (!showExportGCodeButton)
			{
				var noGCodeMessage = new TextWidget(
					"Note".Localize() + ": " + "To enable GCode export, select a printer profile.".Localize(), 
					textColor: ActiveTheme.Instance.PrimaryTextColor, 
					pointSize: 10);
				noGCodeMessage.HAnchor = HAnchor.Left;
				contentRow.AddChild(noGCodeMessage);
			}

			var exportButton = textImageButtonFactory.Generate("Export".Localize());
			exportButton.Click += (s, e) =>
			{
				string fileTypeFilter = "";
				string targetExtension = "";

				IExportPlugin activePlugin = null;

				if (exportGCode.Checked)
				{
					fileTypeFilter = "Export GCode|*.gcode";
					targetExtension = ".gcode";
				}
				else
				{
					// Loop over all plugin buttons, break on the first checked item found
					foreach(var button in this.exportPluginButtons.Keys)
					{
						if (button.Checked)
						{
							activePlugin = exportPluginButtons[button];
							break;
						}
					}

					// Early exit if no plugin radio button is selected
					if (activePlugin == null)
					{
						return;
					}

					fileTypeFilter = activePlugin.ExtensionFilter;
					targetExtension = activePlugin.FileExtension;
				}

				this.Parent.CloseOnIdle();
				UiThread.RunOnIdle(() =>
				{
					string title = "MatterControl: " + "Export File".Localize();
					FileDialog.SaveFileDialog(
						new SaveFileDialogParams(fileTypeFilter)
						{
							Title = title,
							ActionButtonLabel = "Export".Localize(),
							FileName = Path.GetFileNameWithoutExtension(libraryContent.Name)
						},
						(saveParams) =>
						{
							string savePath = saveParams.FileName;

							if (!string.IsNullOrEmpty(savePath))
							{
								Task.Run(async () =>
								{
									string extension = Path.GetExtension(savePath);
									if (extension != targetExtension)
									{
										savePath += targetExtension;
									}

									bool succeeded = false;

									if (exportGCode.Checked)
									{
										succeeded = await SaveGCode(savePath);
									}
									else if (activePlugin != null)
									{
										succeeded = await activePlugin.Generate(libraryContent, savePath);
										//await SaveAmf(libraryContent, savePath)
										//succeeded = await ExportToPlugin(activePlugin, savePath);
									}

									if (succeeded)
									{
										ShowFileIfRequested(savePath);
									}
									else
									{
										UiThread.RunOnIdle(() =>
										{
											StyledMessageBox.ShowMessageBox(null, "Export failed".Localize(), title);
										});
									}
								});
							}
						});
				});
			};

			footerRow.AddChild(exportButton);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private void ShowFileIfRequested(string filename)
		{
			if (OsInformation.OperatingSystem == OSType.Windows)
			{
				if (showInFolderAfterSave.Checked)
				{
#if IS_WINDOWS_FORMS
					WindowsFormsAbstract.ShowFileInFolder(filename);
#endif
				}
			}
		}

		public async Task<bool> ExportToPlugin(IExportPlugin plugin, string filePathToSave)
		{
			try
			{
				string generatedOrExistingFilePath = await SliceFileIfNeeded();

				var gcodeFileItem = new FileSystemFileItem(generatedOrExistingFilePath);


				if (File.Exists(generatedOrExistingFilePath))
				{
					plugin.Generate(gcodeFileItem, filePathToSave);
					return true;
				}
			}
			catch
			{
			}

			return false;
		}

		public async Task<bool> SaveGCode(string filePathToSave)
		{
			try
			{
				string newGCodePath = await SliceFileIfNeeded();

				if (File.Exists(newGCodePath))
				{
					SaveGCodeToNewLocation(newGCodePath, filePathToSave);
					return true;
				}
			}
			catch
			{
			}

			return false;
		}

		private async Task<string> SliceFileIfNeeded()
		{
			// TODO: How to handle gcode files in library content?
			//string fileToProcess = partIsGCode ?  printItemWrapper.FileLocation : "";
			string fileToProcess = "";

			string sourceExtension = Path.GetExtension(libraryContent.FileName).ToUpper();
			if (MeshFileIo.ValidFileExtensions().Contains(sourceExtension)
				|| sourceExtension == ".MCX")
			{
				// Save any pending changes before starting the print
				await ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded();

				var printItem = ApplicationController.Instance.ActivePrintItem;

				await SlicingQueue.SliceFileAsync(printItem, null);

				fileToProcess = printItem.GetGCodePathAndFileName();
			}

			return fileToProcess;
		}

		private void SaveGCodeToNewLocation(string gcodeFilename, string dest)
		{
			try
			{
				GCodeFileStream gCodeFileStream = new GCodeFileStream(GCodeFile.Load(gcodeFilename, CancellationToken.None));

				bool addLevelingStream = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled) && applyLeveling.Checked;
				var queueStream = new QueuedCommandsStream(gCodeFileStream);

				// this is added to ensure we are rewriting the G0 G1 commands as needed
				GCodeStream finalStream = addLevelingStream
					? new ProcessWriteRegexStream(new PrintLevelingStream(queueStream, false), queueStream)
					: new ProcessWriteRegexStream(queueStream, queueStream);

				using (StreamWriter file = new StreamWriter(dest))
				{
					string nextLine = finalStream.ReadLine();
					while (nextLine != null)
					{
						if (nextLine.Trim().Length > 0)
						{
							file.WriteLine(nextLine);
						}
						nextLine = finalStream.ReadLine();
					}
				}
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
				});
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}