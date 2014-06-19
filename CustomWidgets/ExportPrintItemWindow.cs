using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    public class ExportPrintItemWindow : SystemWindow
    {
        CheckBox showInFolderAfterSave;
        CheckBox applyLeveling;
        private PrintItemWrapper printItemWrapper;
        string pathAndFilenameToSave;
        bool partIsGCode = false;

        public ExportPrintItemWindow(PrintItemWrapper printItemWraper)
            : base(400, 250)
        {
            this.printItemWrapper = printItemWraper;

            if (Path.GetExtension(printItemWraper.FileLocation).ToUpper() == ".GCODE")
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

        string applyLevelingDuringExportString = "Apply leveling to gcode during export".Localize();
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
                exportAsStlButton.Click += new ButtonBase.ButtonEventHandler(exportSTL_Click);
                middleRowContainer.AddChild(exportAsStlButton);
            }

            bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

            if(showExportGCodeButton)
            {
                string exportGCodeText = LocalizedString.Get("Export as");
                string exportGCodeTextFull = string.Format("{0} GCode", exportGCodeText);
                Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);
                exportGCode.HAnchor = HAnchor.ParentLeft;
                exportGCode.Cursor = Cursors.Hand;
                exportGCode.Click += new ButtonBase.ButtonEventHandler(exportGCode_Click);
                middleRowContainer.AddChild(exportGCode);
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
            if (OsInformation.GetOSType() == OsInformation.OSType.Windows
                || OsInformation.GetOSType() == OsInformation.OSType.X11)
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
				string noGCodeMessageTextEnd = LocalizedString.Get ("To enable GCode export, select a printer profile.");
				string noGCodeMessageTextFull = string.Format ("{0}: {1}", noGCodeMessageTextBeg, noGCodeMessageTextEnd);
				TextWidget noGCodeMessage = new TextWidget(noGCodeMessageTextFull, textColor:ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
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

        void exportGCode_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(DoExportGCode_Click);
        }

        void DoExportGCode_Click(object state)
        {
            SaveFileDialogParams saveParams = new SaveFileDialogParams("Export GCode|*.gcode", title: "Export GCode");
			saveParams.Title = "MatterControl: Export File";
			saveParams.ActionButtonLabel = "Export";

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);
			if (streamToSaveTo != null) 
			{
				streamToSaveTo.Close ();

                pathAndFilenameToSave = saveParams.FileName;
                string extension = Path.GetExtension(pathAndFilenameToSave);
				if(extension == "")
				{
                    File.Delete(pathAndFilenameToSave);
                    pathAndFilenameToSave += ".gcode";
				}

                if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == ".STL")
                {
                    Close();
                    SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
                    printItemWrapper.SlicingDone.RegisterEvent(sliceItem_Done, ref unregisterEvents);
                }
                else if (partIsGCode)
                {
                    Close();
                    SaveGCodeToNewLocation(printItemWrapper.FileLocation, pathAndFilenameToSave);
                }
            }
        }

        private void SaveGCodeToNewLocation(string source, string dest)
        {
            if (ActivePrinterProfile.Instance.DoPrintLeveling)
            {
                GCodeFile unleveledGCode = new GCodeFile(source);
                if (applyLeveling.Checked)
                {
                    PrintLevelingPlane.Instance.ApplyLeveling(unleveledGCode);

                    PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
                    if (levelingData != null)
                    {
                        for (int i = 0; i < unleveledGCode.Count; i++)
                        {
                            PrinterMachineInstruction instruction = unleveledGCode.Instruction(i);

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
                            foreach(string line in linesToWrite)
                            {
                                PrinterMachineInstruction newInstruction = new PrinterMachineInstruction(line);
                                unleveledGCode.Insert(++i, newInstruction); 
                            }
                        }
                    }
                }
                unleveledGCode.Save(dest);

                // Make sure we turn this back on as it can get turned off by the LevelWizard2Point.ProcessCommand and it was on.
                ActivePrinterProfile.Instance.DoPrintLeveling = true;
            }
            else
            {
                File.Copy(source, dest, true);
            }
            ShowFileIfRequested(dest);
        }

        void ShowFileIfRequested(string filename)
        {
            if (OsInformation.GetOSType() == OsInformation.OSType.Windows) 
			{
				if (showInFolderAfterSave.Checked) 
				{
					WindowsFormsAbstract.ShowFileInFolder (filename);
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

        event EventHandler unregisterEvents;

        void ReloadAfterPrinterProfileChanged(object sender, EventArgs e)
        {
            CreateWindowContent();   
        }

        void exportSTL_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(DoExportSTL_Click);
        }

        void DoExportSTL_Click(object state)
        {
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save as STL|*.stl");  
			saveParams.Title = "MatterControl: Export File";
			saveParams.ActionButtonLabel = "Export";
            saveParams.FileName = printItemWrapper.Name;

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);

			if (streamToSaveTo != null) 
			{
				streamToSaveTo.Close ();
				Close();
			}
			
			string filePathToSave = saveParams.FileName;
            if (filePathToSave != null && filePathToSave != "")
            {
                string extension = Path.GetExtension(filePathToSave);
                if (extension == "")
                {
                    File.Delete(filePathToSave);
                    filePathToSave += ".stl";
                }
                File.Copy(printItemWrapper.FileLocation, filePathToSave, true);
                ShowFileIfRequested(filePathToSave);
            }
        }

        void sliceItem_Done(object sender, EventArgs e)
        {
            PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

            printItemWrapper.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);
            SaveGCodeToNewLocation(sliceItem.GetGCodePathAndFileName(), pathAndFilenameToSave);
        }
    }
}
