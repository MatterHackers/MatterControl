using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    public class ExportQueueItemWindow : SystemWindow
    {
        CheckBox showInFolderAfterSave;
        private PrintQueue.PrintQueueItem printQueueItem;
        string pathAndFilenameToSave;
        bool partIsGCode = false;

        public ExportQueueItemWindow(PrintQueue.PrintQueueItem printQueueItem)
            : base(400, 250)
        {
            if (Path.GetExtension(printQueueItem.PrintItemWrapper.FileLocation).ToUpper() == ".GCODE")
            {
                partIsGCode = true;
            }

			string McExportFileTitleBeg = LocalizedString.Get("MatterControl");
			string McExportFileTitleEnd = LocalizedString.Get("Export File");
			string McExportFileTitleFull = string.Format("{0}: {1}", McExportFileTitleBeg, McExportFileTitleEnd); 

			this.Title = McExportFileTitleFull;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            
            // TODO: Complete member initialization
            this.printQueueItem = printQueueItem;

            doLayout();
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(reloadAfterPrinterProfileChanged, ref unregisterEvents);
        }

        public void doLayout()
        {
            this.RemoveAllChildren();
            TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.Padding = new BorderDouble(10);
            topToBottom.AnchorAll();

			string exportLblTxt = LocalizedString.Get ("File export options");
			string exportLblTxtFull = string.Format ("{0}:", exportLblTxt);
			TextWidget exportLabel = new TextWidget(exportLblTxtFull);
            exportLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            topToBottom.AddChild(exportLabel);

            GuiWidget dividerLine = new GuiWidget();
            dividerLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            dividerLine.Height = 1;
            dividerLine.Margin = new BorderDouble(0, 3);
            dividerLine.BackgroundColor = RGBA_Bytes.White;
            topToBottom.AddChild(dividerLine);

            if (!partIsGCode)
            {
				string exportStlTxt = LocalizedString.Get("Export as");
				string exportStlTxtFull = string.Format("{0} STL", exportStlTxt);

				Button exportAsStlButton = textImageButtonFactory.Generate(exportStlTxtFull);
                exportAsStlButton.Click += new ButtonBase.ButtonEventHandler(exportSTL_Click);
                //exportSTL.HAnchor = Agg.UI.HAnchor.ParentCenter;
                topToBottom.AddChild(exportAsStlButton);
            }

            bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

            if(showExportGCodeButton)
            {
				string exportGCodeText = LocalizedString.Get("Export as");
				string exportGCodeTextFull = string.Format("{0} GCode", exportGCodeText);

				Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);

                //exportGCode.HAnchor = Agg.UI.HAnchor.ParentCenter;
				exportGCode.Click += new ButtonBase.ButtonEventHandler(exportGCode_Click);
				topToBottom.AddChild(exportGCode);
            }

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
            topToBottom.AddChild(vSpacer);

            if (!showExportGCodeButton)
            {
				string noGCodeMessageTxtBeg = LocalizedString.Get("Note");
				string noGCodeMessageTxtEnd = LocalizedString.Get ("To enable GCode export, select a printer profile.");
				string noGCodeMessageTxtFull = string.Format ("{0}: {1}", noGCodeMessageTxtBeg, noGCodeMessageTxtEnd);
				TextWidget noGCodeMessage = new TextWidget(noGCodeMessageTxtFull, textColor: RGBA_Bytes.White, pointSize: 10);
                topToBottom.AddChild(noGCodeMessage);
			}

            // TODO: make this work on the mac and then delete this if
           	if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType() == WindowsFormsAbstract.OSType.Windows)
            {
				showInFolderAfterSave = new CheckBox(LocalizedString.Get("Show file in folder after save"), RGBA_Bytes.White, 10);
                showInFolderAfterSave.Margin = new BorderDouble(top: 10);
                topToBottom.AddChild(showInFolderAfterSave);
            }

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

                if (Path.GetExtension(printQueueItem.PrintItemWrapper.FileLocation).ToUpper() == ".STL")
                {
                    Close();
                    SlicingQueue.Instance.QueuePartForSlicing(printQueueItem.PrintItemWrapper);
                    printQueueItem.PrintItemWrapper.Done += new EventHandler(sliceItem_Done);
                }
                else if (partIsGCode)
                {
                    Close();
                    SaveGCodeToNewLocation(printQueueItem.PrintItemWrapper.FileLocation, pathAndFilenameToSave);
                }
            }
        }

        private void SaveGCodeToNewLocation(string source, string dest)
        {
            if (ActivePrinterProfile.Instance.DoPrintLeveling)
            {
                GCodeFile unleveledGCode = new GCodeFile(source);
                PrintLeveling.Instance.ApplyLeveling(unleveledGCode);
                unleveledGCode.Save(dest);
            }
            else
            {
                File.Copy(source, dest, true);
            }
            ShowFileIfRequested(dest);
        }

        void ShowFileIfRequested(string filename)
        {
			if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType () == WindowsFormsAbstract.OSType.Windows) 
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

        void reloadAfterPrinterProfileChanged(object sender, EventArgs e)
        {
            doLayout();   
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

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);

			if (streamToSaveTo != null) 
			{
				streamToSaveTo.Close ();
				Close();
			}
			// windows vista +: filePathToSave 'test.stl'
			// windows xp: filePathToSave 'test'
			
			string filePathToSave = saveParams.FileName;
			string extension = Path.GetExtension(filePathToSave);
			if (extension == "") 
			{						
				File.Delete (filePathToSave);
				filePathToSave += ".stl";
			}
			File.Copy (printQueueItem.PrintItemWrapper.FileLocation, filePathToSave, true);
			ShowFileIfRequested (filePathToSave);
        }


        void sliceItem_Done(object sender, EventArgs e)
        {
            PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

            sliceItem.Done -= new EventHandler(sliceItem_Done);
            SaveGCodeToNewLocation(sliceItem.GCodePathAndFileName, pathAndFilenameToSave);
        }
    }
}
