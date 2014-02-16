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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class ExportLibraryItemWindow : SystemWindow
    {
        CheckBox showInFolderAfterSave;
        private PrintLibraryListItem printQueueItem;
        string pathAndFilenameToSave;
        bool partIsGCode = false;

        public ExportLibraryItemWindow(PrintLibraryListItem printLibraryItem)
            : base(400, 250)
        {
            if (System.IO.Path.GetExtension(printLibraryItem.printItem.FileLocation).ToUpper() == ".GCODE")
            {
                partIsGCode = true;
            }

            this.Title = "MatterControl: Export File";
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            // TODO: Complete member initialization
            this.printQueueItem = printLibraryItem;


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

            TextWidget exportLabel = new TextWidget("File export options:");
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
                Button exportSTL = textImageButtonFactory.Generate("Export as STL");
                exportSTL.Click += new ButtonBase.ButtonEventHandler(exportSTL_Click);
                //exportSTL.HAnchor = Agg.UI.HAnchor.ParentCenter;
                topToBottom.AddChild(exportSTL);
            }

            bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

            if (showExportGCodeButton)
            {
                Button exportGCode = textImageButtonFactory.Generate("Export as GCode");
                //exportGCode.HAnchor = Agg.UI.HAnchor.ParentCenter;
                exportGCode.Click += new ButtonBase.ButtonEventHandler(exportGCode_Click);
                topToBottom.AddChild(exportGCode);
            }

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
            topToBottom.AddChild(vSpacer);

            if (!showExportGCodeButton)
            {
                TextWidget noGCodeMessage = new TextWidget("Note: To enable GCode export, select a printer profile.", textColor: RGBA_Bytes.White, pointSize: 10);
                topToBottom.AddChild(noGCodeMessage);
            }

            // TODO: make this work on the mac and then delete this if
            if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType() == WindowsFormsAbstract.OSType.Windows)
            {
                showInFolderAfterSave = new CheckBox("Show file in folder after save", RGBA_Bytes.White, 10);
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
                streamToSaveTo.Close();
                if (System.IO.Path.GetExtension(printQueueItem.printItem.FileLocation).ToUpper() == ".STL")
                {
                    pathAndFilenameToSave = saveParams.FileName;
                    Close();
                    SlicingQueue.Instance.QueuePartForSlicing(printQueueItem.printItem);
                    printQueueItem.printItem.Done += new EventHandler(sliceItem_Done);
                }
                else if (partIsGCode)
                {
                    Close();
                    SaveGCodeToNewLocation(printQueueItem.printItem.FileLocation, saveParams.FileName);
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
            if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType() == WindowsFormsAbstract.OSType.Windows)
            {
                if (showInFolderAfterSave.Checked)
                {
                    WindowsFormsAbstract.ShowFileInFolder(filename);
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
                streamToSaveTo.Close();
                Close();
                File.Copy(printQueueItem.printItem.FileLocation, saveParams.FileName, true);
                ShowFileIfRequested(saveParams.FileName);
            }
        }

        void sliceItem_Done(object sender, EventArgs e)
        {
            PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

            sliceItem.Done -= new EventHandler(sliceItem_Done);
            SaveGCodeToNewLocation(sliceItem.GCodePathAndFileName, pathAndFilenameToSave);
        }
    }
}
