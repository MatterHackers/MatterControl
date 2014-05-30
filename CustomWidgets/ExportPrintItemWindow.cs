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
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
    public class ExportPrintItemWindow : SystemWindow
    {
        CheckBox showInFolderAfterSave;
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
            
            doLayout();
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(reloadAfterPrinterProfileChanged, ref unregisterEvents);
        }

        public void doLayout()
        {
            this.RemoveAllChildren();
            TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);
            topToBottom.AnchorAll();

			FlowLayoutWidget headerContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerContainer.HAnchor = HAnchor.ParentLeftRight;
			headerContainer.Padding = new BorderDouble (0, 3, 0, 3);
			headerContainer.Margin = new BorderDouble (0, 3, 0, 0);
			headerContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			string exportLabelText = LocalizedString.Get ("File export options");
			string exportLabelTextFull = string.Format ("{0}:", exportLabelText);
			TextWidget exportLabel = new TextWidget(exportLabelTextFull);
            exportLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			headerContainer.AddChild (exportLabel);
			topToBottom.AddChild(headerContainer);

			FlowLayoutWidget exportSTLGCodeButtonsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			exportSTLGCodeButtonsContainer.HAnchor = HAnchor.ParentLeftRight;
			exportSTLGCodeButtonsContainer.VAnchor = VAnchor.ParentBottomTop;
			exportSTLGCodeButtonsContainer.Padding = new BorderDouble (5);
			exportSTLGCodeButtonsContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			Button cancelButton = textImageButtonFactory.Generate ("Cancel");
			cancelButton.Padding = new BorderDouble(0);
			cancelButton.Click += ( sender, e) => {
				CloseOnIdle ();
			};
				
            if (!partIsGCode)
            {
				string exportStlText = LocalizedString.Get("Export as");
				string exportStlTextFull = string.Format("{0} STL", exportStlText);

				Button exportAsStlButton = textImageButtonFactory.Generate(exportStlTextFull);
                exportAsStlButton.Click += new ButtonBase.ButtonEventHandler(exportSTL_Click);
				exportSTLGCodeButtonsContainer.AddChild (exportAsStlButton);
            }

            bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

            if(showExportGCodeButton)
            {
				string exportGCodeText = LocalizedString.Get("Export as");
				string exportGCodeTextFull = string.Format("{0} GCode", exportGCodeText);

				Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);
				exportGCode.Click += new ButtonBase.ButtonEventHandler(exportGCode_Click);
				exportSTLGCodeButtonsContainer.AddChild (exportGCode);
            }

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
			exportSTLGCodeButtonsContainer.AddChild(vSpacer);

            if (!showExportGCodeButton)
            {
				string noGCodeMessageTextBeg = LocalizedString.Get("Note");
				string noGCodeMessageTextEnd = LocalizedString.Get ("To enable GCode export, select a printer profile.");
				string noGCodeMessageTextFull = string.Format ("{0}: {1}", noGCodeMessageTextBeg, noGCodeMessageTextEnd);
				TextWidget noGCodeMessage = new TextWidget(noGCodeMessageTextFull, textColor:ActiveTheme.Instance.PrimaryTextColor, pointSize: 10); 
				exportSTLGCodeButtonsContainer.AddChild(noGCodeMessage);
			}

			FlowLayoutWidget buttonRow = new FlowLayoutWidget (FlowDirection.LeftToRight);
			buttonRow.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble (0);

            // TODO: make this work on the mac and then delete this if
			if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType () == WindowsFormsAbstract.OSType.Windows) 
			{
				showInFolderAfterSave = new CheckBox (LocalizedString.Get ("Show file in folder after save"), ActiveTheme.Instance.PrimaryTextColor, 10);
				showInFolderAfterSave.Margin = new BorderDouble (top: 10);
				exportSTLGCodeButtonsContainer.AddChild (showInFolderAfterSave);
			}

			buttonRow.AddChild (new HorizontalSpacer());
			buttonRow.AddChild(cancelButton);
			topToBottom.AddChild(exportSTLGCodeButtonsContainer);
			topToBottom.AddChild (buttonRow);            

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
