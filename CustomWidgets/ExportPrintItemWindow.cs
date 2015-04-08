using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

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

			CreateWindowContent();
			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ReloadAfterPrinterProfileChanged, ref unregisterEvents);
			ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent(ReloadAfterPrinterProfileChanged, ref unregisterEvents);
		}

		private string applyLevelingDuringExportString = "Apply leveling to gcode during export".Localize();

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

			bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

			if (showExportGCodeButton)
			{
				string exportGCodeText = LocalizedString.Get("Export as");
				string exportGCodeTextFull = string.Format("{0} GCode", exportGCodeText);
				Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);
				exportGCode.HAnchor = HAnchor.ParentLeft;
				exportGCode.Cursor = Cursors.Hand;
				exportGCode.Click += new EventHandler((object sender, EventArgs e) =>
				{
					UiThread.RunOnIdle(ExportGCode_Click);
				});
				middleRowContainer.AddChild(exportGCode);

				bool showExportX3GButton = ActiveSliceSettings.Instance.IsMakerbotGCodeFlavor();
				if (showExportX3GButton)
				{
					string exportAsX3GText = "Export as X3G".Localize();
					Button exportAsX3G = textImageButtonFactory.Generate(exportAsX3GText);
					exportAsX3G.HAnchor = HAnchor.ParentLeft;
					exportAsX3G.Cursor = Cursors.Hand;
					exportAsX3G.Click += new EventHandler((object sender, EventArgs e) =>
						{
							UiThread.RunOnIdle(ExportX3G_Click);
						});
					middleRowContainer.AddChild(exportAsX3G);
				}
			}

			middleRowContainer.AddChild(new VerticalSpacer());

			// If print leveling is enabled then add in a check box 'Apply Leveling During Export' and default checked.
			if (showExportGCodeButton && ActivePrinterProfile.Instance.DoPrintLeveling)
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

		private void ExportGCode_Click(object state)
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
			if (saveParams.FileName != null)
			{
				gcodePathAndFilenameToSave = saveParams.FileName;
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
					printItemWrapper.SlicingDone.RegisterEvent(sliceItem_Done, ref unregisterEvents);
				}
				else if (partIsGCode)
				{
					SaveGCodeToNewLocation(printItemWrapper.FileLocation, gcodePathAndFilenameToSave);
				}
			}
		}

		private void ExportX3G_Click(object state)
		{
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Export X3G|*.x3g", title: "Export X3G");
			saveParams.Title = "MatterControl: Export File";
			saveParams.ActionButtonLabel = "Export";

			FileDialog.SaveFileDialog(saveParams, onExportX3gFileSelected);
		}

		private void onExportX3gFileSelected(SaveFileDialogParams saveParams)
		{
			if (saveParams.FileName != null)
			{
				x3gPathAndFilenameToSave = saveParams.FileName;
				string extension = Path.GetExtension(x3gPathAndFilenameToSave);
				if (extension == "")
				{
					File.Delete(gcodePathAndFilenameToSave);
					x3gPathAndFilenameToSave += ".x3g";
				}

				string saveExtension = Path.GetExtension(printItemWrapper.FileLocation).ToUpper();
				if (MeshFileIo.ValidFileExtensions().Contains(saveExtension))
				{
					Close();
					SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
					printItemWrapper.SlicingDone.RegisterEvent(x3gItemSlice_Complete, ref unregisterEvents);
				}
				else if (partIsGCode)
				{
					Close();
					generateX3GfromGcode(printItemWrapper.FileLocation, x3gPathAndFilenameToSave);
				}
			}
		}

		private void SaveGCodeToNewLocation(string source, string dest)
		{
			if (ActivePrinterProfile.Instance.DoPrintLeveling)
			{
				GCodeFileLoaded unleveledGCode = new GCodeFileLoaded(source);
				if (applyLeveling.Checked)
				{
					PrintLevelingPlane.Instance.ApplyLeveling(unleveledGCode);

					PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
					if (levelingData != null)
					{
						for (int lineIndex = 0; lineIndex < unleveledGCode.LineCount; lineIndex++)
						{
							PrinterMachineInstruction instruction = unleveledGCode.Instruction(lineIndex);

							List<string> linesToWrite = null;
							switch (levelingData.levelingSystem)
							{
								case PrintLevelingData.LevelingSystem.Probe2Points:
									linesToWrite = LevelWizard2Point.ProcessCommand(instruction.Line);
									break;

								case PrintLevelingData.LevelingSystem.Probe3Points:
									linesToWrite = LevelWizard3Point.ProcessCommand(instruction.Line);
									break;
							}

							instruction.Line = linesToWrite[0];
							linesToWrite.RemoveAt(0);

							// now insert any new lines
							foreach (string line in linesToWrite)
							{
								PrinterMachineInstruction newInstruction = new PrinterMachineInstruction(line);
								unleveledGCode.Insert(++lineIndex, newInstruction);
							}
						}
					}
				}
				unleveledGCode.Save(dest);
			}
			else
			{
				File.Copy(source, dest, true);
			}
			ShowFileIfRequested(dest);
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
			UiThread.RunOnIdle((state) =>
			{
				SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as AMF|*.amf", initialDirectory: documentsPath);
				saveParams.Title = "MatterControl: Export File";
				saveParams.ActionButtonLabel = "Export";
				saveParams.FileName = printItemWrapper.Name;

				Close();
				FileDialog.SaveFileDialog(saveParams, onExportAmfFileSelected);
			});
		}

		private void onExportAmfFileSelected(SaveFileDialogParams saveParams)
		{
			BackgroundWorker saveWorker = new BackgroundWorker();
			saveWorker.DoWork += amfSaveWorker_DoWork;
			saveWorker.RunWorkerAsync(saveParams);
		}

		private void amfSaveWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			SaveFileDialogParams saveParams = e.Argument as SaveFileDialogParams;

			if (saveParams.FileName != null)
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

		private void exportSTL_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle((state) =>
			{
				SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as STL|*.stl");
				saveParams.Title = "MatterControl: Export File";
				saveParams.ActionButtonLabel = "Export";
				saveParams.FileName = printItemWrapper.Name;

				Close();
				FileDialog.SaveFileDialog(saveParams, onExportStlFileSelected);
			});
		}

		private void onExportStlFileSelected(SaveFileDialogParams saveParams)
		{
			BackgroundWorker saveWorker = new BackgroundWorker();
			saveWorker.DoWork += stlSaveWorker_DoWork;
			saveWorker.RunWorkerAsync(saveParams);
		}

		private void stlSaveWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			SaveFileDialogParams saveParams = e.Argument as SaveFileDialogParams;
			if (saveParams.FileName != null)
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

		private void sliceItem_Done(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

			printItemWrapper.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);
			SaveGCodeToNewLocation(sliceItem.GetGCodePathAndFileName(), gcodePathAndFilenameToSave);
		}

		private void x3gItemSlice_Complete(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;
			printItemWrapper.SlicingDone.UnregisterEvent(x3gItemSlice_Complete, ref unregisterEvents);
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