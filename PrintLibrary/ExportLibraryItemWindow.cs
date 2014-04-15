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

            string exportLibraryFileTitle = LocalizedString.Get("MatterControl");
            string exportLibraryFileTitleFull = LocalizedString.Get("Export File");
			this.Title = string.Format("{0}: {1}", exportLibraryFileTitle, exportLibraryFileTitleFull);
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
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);
            topToBottom.AnchorAll();


			FlowLayoutWidget headerContainer = new FlowLayoutWidget (FlowDirection.LeftToRight);
			headerContainer.HAnchor = HAnchor.ParentLeftRight;
			headerContainer.Padding = new BorderDouble (0, 3, 0, 3);
			headerContainer.Margin = new BorderDouble (0, 3, 0, 0);
			headerContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;


            string fileExportLabelTxt = LocalizedString.Get("File export options");
			TextWidget exportLabel = new TextWidget(string.Format("{0}:", fileExportLabelTxt));
            exportLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			headerContainer.AddChild(exportLabel);
			topToBottom.AddChild(headerContainer);


			FlowLayoutWidget exportStlGcodeButtonsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			exportStlGcodeButtonsContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			exportStlGcodeButtonsContainer.HAnchor = HAnchor.ParentLeftRight;
			exportStlGcodeButtonsContainer.VAnchor = VAnchor.ParentBottomTop;
			exportStlGcodeButtonsContainer.Padding = new BorderDouble (5);

			Button cancelButton = textImageButtonFactory.Generate ("Cancel");
			cancelButton.Padding = new BorderDouble (0);
			cancelButton.Click += (sender, e) => {
				CloseOnIdle();
			};

			GuiWidget gDSpacer = new GuiWidget ();
			gDSpacer.HAnchor = HAnchor.ParentLeftRight;


            if (!partIsGCode)
            {
                string exportSTLTxt = LocalizedString.Get("Export as");
				string exportSTLTxtFull = string.Format ("{0} STL", exportSTLTxt);

				Button exportAsSTLButton = textImageButtonFactory.Generate(exportSTLTxtFull);
				exportAsSTLButton.Click += new ButtonBase.ButtonEventHandler(exportSTL_Click);
				exportStlGcodeButtonsContainer.AddChild(exportAsSTLButton);
            }

            bool showExportGCodeButton = ActivePrinterProfile.Instance.ActivePrinter != null || partIsGCode;

            if (showExportGCodeButton)
            {
                string exportGCodeText = LocalizedString.Get("Export as");
				string exportGCodeTextFull = string.Format ("{0} GCode", exportGCodeText);

				Button exportGCode = textImageButtonFactory.Generate(exportGCodeTextFull);
				exportGCode.Click += new ButtonBase.ButtonEventHandler(exportGCode_Click);
				exportStlGcodeButtonsContainer.AddChild(exportGCode);
            }

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
			exportStlGcodeButtonsContainer.AddChild(vSpacer);

            if (!showExportGCodeButton)
            {
                string noGCodeMessageText = LocalizedString.Get("Note");
                string noGCodeMessageTextFull = LocalizedString.Get("To enable GCode export, select a printer profile");
				TextWidget noGCodeMessage = new TextWidget(string.Format("{0}: {1}.", noGCodeMessageText, noGCodeMessageTextFull), textColor: RGBA_Bytes.White, pointSize: 10);
				exportStlGcodeButtonsContainer.AddChild(noGCodeMessage);
            }

			FlowLayoutWidget buttonRow = new FlowLayoutWidget (FlowDirection.LeftToRight);
			buttonRow.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0);

            // TODO: make this work on the mac and then delete this if
            if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType() == WindowsFormsAbstract.OSType.Windows)
            {
				showInFolderAfterSave = new CheckBox(LocalizedString.Get("Show file in folder after save"), ActiveTheme.Instance.PrimaryTextColor, 10);
                showInFolderAfterSave.Margin = new BorderDouble(top: 10);
				exportStlGcodeButtonsContainer.AddChild(showInFolderAfterSave);
				buttonRow.AddChild(gDSpacer);
				buttonRow.AddChild(cancelButton);
				topToBottom.AddChild (exportStlGcodeButtonsContainer);
				topToBottom.AddChild(buttonRow);
            }

            this.AddChild(topToBottom);
        }

        void exportGCode_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(DoExportGCode_Click);
        }

		string GetExtension (string filename)
		{
			string extension;
			int indexOfDot = filename.LastIndexOf(".");
			if (indexOfDot == -1)
			{
				extension = "";			
			}
			else
			{
				extension = filename.Substring(indexOfDot);
			}
			return extension;
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


				string filePathToSave = saveParams.FileName;
				string extension = GetExtension(filePathToSave);

				if(extension == "")
				{
					File.Delete (filePathToSave);
					filePathToSave += ".gcode";
				}

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
					SaveGCodeToNewLocation(printQueueItem.printItem.FileLocation,filePathToSave);
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


		string CheckExtension(string fileName)
		{
			string extension;
			int indexOfDot = fileName.LastIndexOf(".");
			if (indexOfDot == -1)
			{
				extension = "";
			}
			else 
			{
				extension = fileName.Substring(indexOfDot);
			}
			return extension;
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
				Close ();
			}


			string filePathToSave = saveParams.FileName;
			string extension = CheckExtension(filePathToSave);

			if(extension == "") 
			{
				File.Delete (filePathToSave);
				filePathToSave +=  ".stl";
			}

			File.Copy(printQueueItem.printItem.FileLocation, filePathToSave, true);
			ShowFileIfRequested(filePathToSave);
            
        }

        void sliceItem_Done(object sender, EventArgs e)
        {
            PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

            sliceItem.Done -= new EventHandler(sliceItem_Done);
            SaveGCodeToNewLocation(sliceItem.GCodePathAndFileName, pathAndFilenameToSave);
        }

		public void CloseOnIdle()
		{
			UiThread.RunOnIdle((state) => { Close(); });
		}


    }
}
