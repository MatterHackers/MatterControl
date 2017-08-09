using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.Queue.OptionsMenu;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl
{
	public class ExportPrintItemPage : WizardPage
	{
		private CheckBox showInFolderAfterSave;
		private CheckBox applyLeveling;
		private PrintItemWrapper printItemWrapper;
		private string gcodePathAndFilenameToSave;
		private bool partIsGCode = false;
		private string documentsPath;

		private EventHandler unregisterEvents;

		public ExportPrintItemPage(PrintItemWrapper printItemWrapper)
			: base(unlocalizedTextForTitle: "File export options:")
		{
			this.printItemWrapper = printItemWrapper;
			if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == ".GCODE")
			{
				partIsGCode = true;
			}

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Name = "Export Item Window";

			CreateWindowContent();

			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((s, e) => CreateWindowContent(), ref unregisterEvents);
		}

		public void CreateWindowContent()
		{
			bool modelCanBeExported = !partIsGCode;
			
			if(modelCanBeExported
				&& printItemWrapper != null
				&& (printItemWrapper.PrintItem.Protected
				|| printItemWrapper.PrintItem.ReadOnly))
			{
				modelCanBeExported = false;
			}

			var commonMargin = new BorderDouble(4, 2);

			if (modelCanBeExported)
			{

				// put in stl export
				Button exportAsStlButton = textImageButtonFactory.Generate("Export as".Localize() + " STL");
				exportAsStlButton.Name = "Export as STL button";
				exportAsStlButton.Margin = commonMargin;
				exportAsStlButton.HAnchor = HAnchor.Left;
				exportAsStlButton.Cursor = Cursors.Hand;
				exportAsStlButton.Click += (s, e) =>
				{
					this.Parent.CloseOnIdle();
					UiThread.RunOnIdle(() =>
					{
						FileDialog.SaveFileDialog(
							new SaveFileDialogParams("Save as STL|*.stl")
							{
								Title = "MatterControl: Export File",
								ActionButtonLabel = "Export",
								FileName = printItemWrapper.Name
							},
							(saveParams) =>
							{
								Task.Run(() => SaveStl(saveParams.FileName));
							});
					});
				};
				contentRow.AddChild(exportAsStlButton);

				// put in amf export
				Button exportAsAmfButton = textImageButtonFactory.Generate("Export as".Localize() + " AMF");
				exportAsAmfButton.Name = "Export as AMF button";
				exportAsAmfButton.Margin = commonMargin;
				exportAsAmfButton.HAnchor = HAnchor.Left;
				exportAsAmfButton.Cursor = Cursors.Hand;
				exportAsAmfButton.Click += (s, e) =>
				{
					this.Parent.CloseOnIdle();

					UiThread.RunOnIdle(() =>
					{
						FileDialog.SaveFileDialog(
							new SaveFileDialogParams("Save as AMF|*.amf", initialDirectory: documentsPath)
							{
								Title = "MatterControl: Export File",
								ActionButtonLabel = "Export",
								FileName = printItemWrapper.Name
							},
							(saveParams) =>
							{
								Task.Run(() => SaveAmf(saveParams));
							});
					});
				};
				contentRow.AddChild(exportAsAmfButton);
			}

			bool showExportGCodeButton = ActiveSliceSettings.Instance.PrinterSelected || partIsGCode;
			if (showExportGCodeButton)
			{
				Button exportGCode = textImageButtonFactory.Generate("Export as".Localize() + " G-Code");
				exportGCode.Name = "Export as GCode Button";
				exportGCode.Margin = commonMargin;
				exportGCode.HAnchor = HAnchor.Left;
				exportGCode.Cursor = Cursors.Hand;
				exportGCode.Click += (s, e) =>
				{
					this.Parent.CloseOnIdle();
					UiThread.RunOnIdle(() =>
					{
						FileDialog.SaveFileDialog(
							new SaveFileDialogParams("Export GCode|*.gcode", title: "Export GCode")
							{
								Title = "MatterControl: Export File",
								ActionButtonLabel = "Export",
								FileName = Path.GetFileNameWithoutExtension(printItemWrapper.Name)
							},
							(saveParams) =>
							{
								if (!string.IsNullOrEmpty(saveParams.FileName))
								{
									ExportGcodeCommandLineUtility(saveParams.FileName);
								}
							});
					});
				};
				contentRow.AddChild(exportGCode);

				foreach (ExportGcodePlugin plugin in PluginFinder.CreateInstancesOf<ExportGcodePlugin>())
				{
					if (plugin.EnabledForCurrentPart(printItemWrapper))
					{
						// Create export button for each Plugin found
						string exportButtonText = plugin.GetButtonText().Localize();

						Button exportButton = textImageButtonFactory.Generate(exportButtonText);
						exportButton.HAnchor = HAnchor.Left;
						exportButton.Margin = commonMargin;
						exportButton.Cursor = Cursors.Hand;
						exportButton.Click += (s, e) =>
						{
							this.Parent.CloseOnIdle();
							UiThread.RunOnIdle(() =>
							{
								// Open a SaveFileDialog. If Save is clicked, slice the part if needed and pass the plugin the 
								// path to the gcode file and the target save path
								FileDialog.SaveFileDialog(
									new SaveFileDialogParams(plugin.GetExtensionFilter())
									{
										Title = "MatterControl: Export File",
										FileName = printItemWrapper.Name,
										ActionButtonLabel = "Export"
									},
									(saveParam) =>
									{
										string extension = Path.GetExtension(saveParam.FileName);
										if (extension == "")
										{
											saveParam.FileName += plugin.GetFileExtension();
										}

										if (partIsGCode)
										{
											try
											{
												plugin.Generate(printItemWrapper.FileLocation, saveParam.FileName);
											}
											catch (Exception exception)
											{
												UiThread.RunOnIdle(() =>
												{
													StyledMessageBox.ShowMessageBox(null, exception.Message, "Couldn't save file".Localize());
												});
											}
										}
										else
										{
											SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);

											printItemWrapper.SlicingDone += (printItem, eventArgs) =>
											{
												PrintItemWrapper sliceItem = (PrintItemWrapper)printItem;
												if (File.Exists(sliceItem.GetGCodePathAndFileName()))
												{
													try
													{
														plugin.Generate(sliceItem.GetGCodePathAndFileName(), saveParam.FileName);
													}
													catch (Exception exception)
													{
														UiThread.RunOnIdle(() =>
														{
															StyledMessageBox.ShowMessageBox(null, exception.Message, "Couldn't save file".Localize());
														});
													}
												}
											};
										}
									});
							});
						}; // End exportButton Click handler

						contentRow.AddChild(exportButton);
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

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
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

				ShowFileIfRequested(dest);
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
				});
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
							IObject3D item = Object3D.Load(printItemWrapper.FileLocation, CancellationToken.None);
							MeshFileIo.Save(item, filePathToSave);
						}
						ShowFileIfRequested(filePathToSave);
					}
				}
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle (() => {
					StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
				});
			}
		}

		private void SaveStl(string filePathToSave)
		{
			try
			{
				if (!string.IsNullOrEmpty(filePathToSave))
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
						IObject3D loadedItem = Object3D.Load(printItemWrapper.FileLocation, CancellationToken.None);

						if (!MeshFileIo.Save(new List<MeshGroup> { loadedItem.Flatten() }, filePathToSave))
						{
							UiThread.RunOnIdle(() =>
							{
								StyledMessageBox.ShowMessageBox(null, "AMF to STL conversion failed", "Couldn't save file".Localize());
							});
						}
					}

					ShowFileIfRequested(filePathToSave);
				}
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle (() => {
					StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
				});

			}
		}

		private void sliceItem_Done(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

			printItemWrapper.SlicingDone -= sliceItem_Done;
			SaveGCodeToNewLocation(sliceItem.GetGCodePathAndFileName(), gcodePathAndFilenameToSave);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			printItemWrapper.SlicingDone -= sliceItem_Done;
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}