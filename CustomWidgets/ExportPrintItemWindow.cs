using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.Queue.OptionsMenu;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	public class ExportPrintItemWindow : SystemWindow
	{
		private CheckBox showInFolderAfterSave;
		private CheckBox applyLeveling;
		private PrintItemWrapper printItemWrapper;
		private string gcodePathAndFilenameToSave;
		private string x3gPathAndFilenameToSave;
		private bool partIsGCode = false;
		private string documentsPath;

		public ExportPrintItemWindow(PrintItemWrapper printItemWrapper)
			: base(400, 300)
		{
			this.printItemWrapper = printItemWrapper;
			if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == ".GCODE")
			{
				partIsGCode = true;
			}

			string McExportFileTitleBeg = LocalizedString.Get("MatterControl");
			string McExportFileTitleEnd = LocalizedString.Get("Export File");
			string McExportFileTitleFull = string.Format("{0}: {1}", McExportFileTitleBeg, McExportFileTitleEnd);

			this.Title = McExportFileTitleFull;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Name = "Export Item Window";

			CreateWindowContent();
			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent(ReloadAfterPrinterProfileChanged, ref unregisterEvents);
			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent(ReloadAfterPrinterProfileChanged, ref unregisterEvents);
		}

		private string applyLevelingDuringExportString = "Apply leveling to G-Code during export".Localize();

		public void CreateWindowContent()
		{
			this.RemoveAllChildren();
			TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);
			topToBottom.AnchorAll();

			// Creates Header
			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Creates Text and adds into header
			{
				TextWidget elementHeader = new TextWidget("File export options:".Localize(), pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild(elementHeader);
				topToBottom.AddChild(headerRow);
			}

			// Creates container in the middle of window
			FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
				middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
				middleRowContainer.Padding = new BorderDouble(5);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			if (!partIsGCode)
			{
				string exportStlText = LocalizedString.Get("Export as");
				string exportStlTextFull = string.Format("{0} STL", exportStlText);

				Button exportAsStlButton = textImageButtonFactory.Generate(exportStlTextFull);
				exportAsStlButton.HAnchor = HAnchor.ParentLeft;
				exportAsStlButton.Cursor = Cursors.Hand;
				exportAsStlButton.Click += new EventHandler(exportSTL_Click);
				middleRowContainer.AddChild(exportAsStlButton);
			}

			if (!partIsGCode)
			{
				string exportAmfText = LocalizedString.Get("Export as");
				string exportAmfTextFull = string.Format("{0} AMF", exportAmfText);

				Button exportAsAmfButton = textImageButtonFactory.Generate(exportAmfTextFull);
				exportAsAmfButton.HAnchor = HAnchor.ParentLeft;
				exportAsAmfButton.Cursor = Cursors.Hand;
				exportAsAmfButton.Click += new EventHandler(exportAMF_Click);
				middleRowContainer.AddChild(exportAsAmfButton);
			}

			bool showExportGCodeButton = ActiveSliceSettings.Instance != null || partIsGCode;
			if (showExportGCodeButton)
			{
				string exportGCodeTextFull = string.Format("{0} G-Code", "Export as".Localize());
				Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);
				exportGCode.Name = "Export as GCode Button";
				exportGCode.HAnchor = HAnchor.ParentLeft;
				exportGCode.Cursor = Cursors.Hand;
				exportGCode.Click += new EventHandler((object sender, EventArgs e) =>
				{
					UiThread.RunOnIdle(ExportGCode_Click);
				});
				middleRowContainer.AddChild(exportGCode);

				PluginFinder<ExportGcodePlugin> exportPluginFinder = new PluginFinder<ExportGcodePlugin>();

				foreach (ExportGcodePlugin plugin in exportPluginFinder.Plugins)
				{
					//Create export button for each Plugin found

					string exportButtonText = plugin.GetButtonText().Localize();

					Button exportButton = textImageButtonFactory.Generate(exportButtonText);
					exportButton.HAnchor = HAnchor.ParentLeft;
					exportButton.Cursor = Cursors.Hand;
					exportButton.Click += (object sender, EventArgs e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							// Close the export window
							Close();

							// Open a SaveFileDialog. If Save is clicked, slice the part if needed and pass the plugin the 
							// path to the gcode file and the target save path
							FileDialog.SaveFileDialog(
								new SaveFileDialogParams(plugin.GetExtensionFilter())
								{
									Title = "MatterControl: Export File",
									FileName = printItemWrapper.Name,
									ActionButtonLabel = "Export"
								},
								(SaveFileDialogParams saveParam) =>
								{
									string extension = Path.GetExtension(saveParam.FileName);
									if (extension == "")
									{
										saveParam.FileName += plugin.GetFileExtension();
									}

									if (partIsGCode)
									{
										plugin.Generate(printItemWrapper.FileLocation, saveParam.FileName);
									}
									else
									{
										SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);

										printItemWrapper.SlicingDone += (printItem, eventArgs) =>
										{
											PrintItemWrapper sliceItem = (PrintItemWrapper)printItem;
											if (File.Exists(sliceItem.GetGCodePathAndFileName()))
											{
												plugin.Generate(sliceItem.GetGCodePathAndFileName(), saveParam.FileName);
											}
										};
									}
								});
						});
					}; // End exportButton Click handler

					middleRowContainer.AddChild(exportButton);
				}
			}

			middleRowContainer.AddChild(new VerticalSpacer());

			// If print leveling is enabled then add in a check box 'Apply Leveling During Export' and default checked.
			if (showExportGCodeButton && ActiveSliceSettings.Instance.GetValue<bool>("print_leveling_enabled"))
			{
				applyLeveling = new CheckBox(LocalizedString.Get(applyLevelingDuringExportString), ActiveTheme.Instance.PrimaryTextColor, 10);
				applyLeveling.Checked = true;
				applyLeveling.HAnchor = HAnchor.ParentLeft;
				applyLeveling.Cursor = Cursors.Hand;
				//applyLeveling.Margin = new BorderDouble(top: 10);
				middleRowContainer.AddChild(applyLeveling);
			}

			// TODO: make this work on the mac and then delete this if
			if (OsInformation.OperatingSystem == OSType.Windows
				|| OsInformation.OperatingSystem == OSType.X11)
			{
				showInFolderAfterSave = new CheckBox(LocalizedString.Get("Show file in folder after save"), ActiveTheme.Instance.PrimaryTextColor, 10);
				showInFolderAfterSave.HAnchor = HAnchor.ParentLeft;
				showInFolderAfterSave.Cursor = Cursors.Hand;
				//showInFolderAfterSave.Margin = new BorderDouble(top: 10);
				middleRowContainer.AddChild(showInFolderAfterSave);
			}

			if (!showExportGCodeButton)
			{
				string noGCodeMessageTextBeg = LocalizedString.Get("Note");
				string noGCodeMessageTextEnd = LocalizedString.Get("To enable GCode export, select a printer profile.");
				string noGCodeMessageTextFull = string.Format("{0}: {1}", noGCodeMessageTextBeg, noGCodeMessageTextEnd);
				TextWidget noGCodeMessage = new TextWidget(noGCodeMessageTextFull, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
				noGCodeMessage.HAnchor = HAnchor.ParentLeft;
				middleRowContainer.AddChild(noGCodeMessage);
			}

			//Creates button container on the bottom of window
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.ParentLeftRight;
				buttonRow.Padding = new BorderDouble(0, 3);
			}

			Button cancelButton = textImageButtonFactory.Generate("Cancel");
			cancelButton.Name = "Export Item Window Cancel Button";
			cancelButton.Cursor = Cursors.Hand;
			cancelButton.Click += (sender, e) =>
			{
				CloseOnIdle();
			};

			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(cancelButton);
			topToBottom.AddChild(middleRowContainer);
			topToBottom.AddChild(buttonRow);

			this.AddChild(topToBottom);
		}

		private string Get8Name(string longName)
		{
			longName.Replace(' ', '_');
			return longName.Substring(0, Math.Min(longName.Length, 8));
		}

		private void ExportGCode_Click()
		{
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Export GCode|*.gcode", title: "Export GCode");
			saveParams.Title = "MatterControl: Export File";
			saveParams.ActionButtonLabel = "Export";
			saveParams.FileName = Path.GetFileNameWithoutExtension(printItemWrapper.Name);

			Close();
			FileDialog.SaveFileDialog(saveParams, onExportGcodeFileSelected);
		}

		private void onExportGcodeFileSelected(SaveFileDialogParams saveParams)
		{
			if (!string.IsNullOrEmpty(saveParams.FileName))
			{
				ExportGcodeCommandLineUtility(saveParams.FileName);
			}
		}

		public void ExportGcodeCommandLineUtility(String nameOfFile)
		{
			try
			{
				if (!string.IsNullOrEmpty(nameOfFile))
				{
					gcodePathAndFilenameToSave = nameOfFile;
					string extension = Path.GetExtension(gcodePathAndFilenameToSave);
					if (extension == "")
					{
						File.Delete(gcodePathAndFilenameToSave);
						gcodePathAndFilenameToSave += ".gcode";
					}

					string sourceExtension = Path.GetExtension(printItemWrapper.FileLocation).ToUpper();
					if (MeshFileIo.ValidFileExtensions().Contains(sourceExtension))
					{
						SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
						printItemWrapper.SlicingDone += sliceItem_Done;
					}
					else if (partIsGCode)
					{
						SaveGCodeToNewLocation(printItemWrapper.FileLocation, gcodePathAndFilenameToSave);
					}
				}
			}
			catch
			{
			}
		}

		private void SaveGCodeToNewLocation(string gcodeFilename, string dest)
		{
			try
			{
				if (ActiveSliceSettings.Instance.GetValue<bool>("print_leveling_enabled"))
				{
					if (applyLeveling.Checked)
					{
						GCodeFile loadedGCode = GCodeFile.Load(gcodeFilename);
						GCodeFileStream gCodeFileStream0 = new GCodeFileStream(loadedGCode);
						PrintLevelingStream printLevelingStream4 = new PrintLevelingStream(gCodeFileStream0);
						// this is added to ensure we are rewriting the G0 G1 commands as needed
						FeedRateMultiplyerStream extrusionMultiplyerStream = new FeedRateMultiplyerStream(printLevelingStream4);

						using (StreamWriter file = new StreamWriter(dest))
						{
							string nextLine = extrusionMultiplyerStream.ReadLine();
							while (nextLine != null)
							{
								file.WriteLine(nextLine);
								nextLine = extrusionMultiplyerStream.ReadLine();
							}
						}
					}
				}
				else
				{
					File.Copy(gcodeFilename, dest, true);
				}
				ShowFileIfRequested(dest);
			}
			catch
			{
			}
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

		public override void OnClosed(EventArgs e)
		{
			printItemWrapper.SlicingDone -= sliceItem_Done;
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private event EventHandler unregisterEvents;

		private void ReloadAfterPrinterProfileChanged(object sender, EventArgs e)
		{
			CreateWindowContent();
		}

		private void exportAMF_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as AMF|*.amf", initialDirectory: documentsPath);
				saveParams.Title = "MatterControl: Export File";
				saveParams.ActionButtonLabel = "Export";
				saveParams.FileName = printItemWrapper.Name;

				Close();
				FileDialog.SaveFileDialog(saveParams, onExportAmfFileSelected);
			});
		}

		private async void onExportAmfFileSelected(SaveFileDialogParams saveParams)
		{
			await Task.Run(() => SaveAmf(saveParams));
		}

		private void SaveAmf(SaveFileDialogParams saveParams)
		{
			try
			{
				if (!string.IsNullOrEmpty(saveParams.FileName))
				{
					string filePathToSave = saveParams.FileName;
					if (filePathToSave != null && filePathToSave != "")
					{
						string extension = Path.GetExtension(filePathToSave);
						if (extension == "")
						{
							File.Delete(filePathToSave);
							filePathToSave += ".amf";
						}
						if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == Path.GetExtension(filePathToSave).ToUpper())
						{
							File.Copy(printItemWrapper.FileLocation, filePathToSave, true);
						}
						else
						{
							List<MeshGroup> meshGroups = MeshFileIo.Load(printItemWrapper.FileLocation);
							MeshFileIo.Save(meshGroups, filePathToSave);
						}
						ShowFileIfRequested(filePathToSave);
					}
				}
			}
			catch
			{
			}
		}

		private void exportSTL_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as STL|*.stl");
				saveParams.Title = "MatterControl: Export File";
				saveParams.ActionButtonLabel = "Export";
				saveParams.FileName = printItemWrapper.Name;

				Close();
				FileDialog.SaveFileDialog(saveParams, onExportStlFileSelected);
			});
		}

		private async void onExportStlFileSelected(SaveFileDialogParams saveParams)
		{
			await Task.Run(() => SaveStl(saveParams));
		}

		private void SaveStl(SaveFileDialogParams saveParams)
		{
			try
			{
				if (!string.IsNullOrEmpty(saveParams.FileName))
				{
					string filePathToSave = saveParams.FileName;
					if (filePathToSave != null && filePathToSave != "")
					{
						string extension = Path.GetExtension(filePathToSave);
						if (extension == "")
						{
							File.Delete(filePathToSave);
							filePathToSave += ".stl";
						}
						if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == Path.GetExtension(filePathToSave).ToUpper())
						{
							File.Copy(printItemWrapper.FileLocation, filePathToSave, true);
						}
						else
						{
							List<MeshGroup> meshGroups = MeshFileIo.Load(printItemWrapper.FileLocation);
							MeshFileIo.Save(meshGroups, filePathToSave);
						}
						ShowFileIfRequested(filePathToSave);
					}
				}
			}
			catch
			{
			}
		}

		private void sliceItem_Done(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

			printItemWrapper.SlicingDone -= sliceItem_Done;
			SaveGCodeToNewLocation(sliceItem.GetGCodePathAndFileName(), gcodePathAndFilenameToSave);
		}

		private void x3gItemSlice_Complete(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;
			printItemWrapper.SlicingDone -= x3gItemSlice_Complete;
			if (File.Exists(sliceItem.GetGCodePathAndFileName()))
			{
				generateX3GfromGcode(sliceItem.GetGCodePathAndFileName(), x3gPathAndFilenameToSave);
			}
		}

		private string getGpxExectutablePath()
		{
			switch (OsInformation.OperatingSystem)
			{
				case OSType.Windows:
					string gpxRelativePath = Path.Combine("..", "gpx.exe");
					if (!File.Exists(gpxRelativePath))
					{
						gpxRelativePath = Path.Combine(".", "gpx.exe");
					}
					return Path.GetFullPath(gpxRelativePath);

				case OSType.Mac:
					return Path.Combine(ApplicationDataStorage.Instance.ApplicationPath, "gpx");

				case OSType.X11:
					return Path.GetFullPath(Path.Combine(".", "gpx.exe"));

				default:
					throw new NotImplementedException();
			}
		}

		private void generateX3GfromGcode(string gcodeInputPath, string x3gOutputPath, string machineType = "r2")
		{
			string gpxExecutablePath = getGpxExectutablePath();
			string gpxArgs = string.Format("-p -m {2} \"{0}\" \"{1}\" ", gcodeInputPath, x3gOutputPath, machineType);

			ProcessStartInfo exportX3GProcess = new ProcessStartInfo(gpxExecutablePath);
			exportX3GProcess.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			exportX3GProcess.Arguments = gpxArgs;
			Process.Start(exportX3GProcess);
			ShowFileIfRequested(x3gOutputPath);
		}
	}
}