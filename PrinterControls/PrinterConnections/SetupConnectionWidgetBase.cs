using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.PrintLibrary;


namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    //Wraps the printer record. Includes temporary information that we don't need in the DB.
    public class PrinterSetupStatus
    {
        public Printer ActivePrinter;
        public List<string> DriversToInstall = new List<string>();
        public Type PreviousSetupWidget;
        public Type NextSetupWidget;

        public PrinterSetupStatus(Printer printer = null)
        {
            if (printer == null)
            {
                this.ActivePrinter = new Printer();
                this.ActivePrinter.Make = null;
                this.ActivePrinter.Model = null;
                this.ActivePrinter.Name = "Default Printer ({0})".FormatWith(ExistingPrinterCount() + 1);
                this.ActivePrinter.BaudRate = null;
                this.ActivePrinter.ComPort = null;
            }
            else
            {
                this.ActivePrinter = printer;
            }
        }

        public int ExistingPrinterCount()
        {
            string query = string.Format("SELECT COUNT(*) FROM Printer;");
            string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
            return Convert.ToInt32(result);
        }

        public void LoadCalibrationPrints()
        {
            if (this.ActivePrinter.Make != null && this.ActivePrinter.Model != null)    
            {
                // Load the calibration file names
                List<string> calibrationPrintFileNames = LoadCalibrationPartNamesForPrinter(this.ActivePrinter.Make, this.ActivePrinter.Model);

                string[] itemsToAdd = LibraryData.SyncCalibrationFilesToDisk(calibrationPrintFileNames);
                if (itemsToAdd.Length > 0)
                {
                    // Import any files sync'd to disk into the library, then add them to the queue
                    LibraryData.Instance.LoadFilesIntoLibrary(itemsToAdd, null, (sender, e) =>
                    {
                        AddItemsToQueue(calibrationPrintFileNames, QueueData.Instance.GetItemNames());
                    });
                }
                else
                {
                    // Otherwise, just ensure the item gets into the queue
                    AddItemsToQueue(calibrationPrintFileNames, QueueData.Instance.GetItemNames());
                }
            }
        }

        private static void AddItemsToQueue(List<string> calibrationPrintFileNames, string[] queueItems)
        {
            // After the import has completed, add each of the calibration items into the print queue
            foreach (string fileName in calibrationPrintFileNames)
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                if (queueItems.Contains(nameOnly))
                {
                    continue;
                }

                // If the library item does not exist in the queue, add it
                var libraryItem = LibraryData.Instance.GetLibraryItems(nameOnly).FirstOrDefault();
                if (libraryItem != null)
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(libraryItem));
                }
            }
        }

        private List<string> LoadCalibrationPartNamesForPrinter(string make, string model)
        {
            List<string> calibrationFiles = new List<string>();
            string setupSettingsPathAndFile = Path.Combine("PrinterSettings", make, model, "calibration.ini");
            if (StaticData.Instance.FileExists(setupSettingsPathAndFile))
            {
                try
                {
                    var lines = StaticData.Instance.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string settingLine = line.Trim();
                            calibrationFiles.Add(settingLine);
                        }
                    }
                }
                catch
                {

                }
            }
            return calibrationFiles;
        }
    }
    
    public class SetupConnectionWidgetBase : ConnectionWidgetBase
    {        
        public PrinterSetupStatus PrinterSetupStatus;

        //private GuiWidget mainContainer;

        protected FlowLayoutWidget headerRow;
        protected FlowLayoutWidget contentRow;
        protected FlowLayoutWidget footerRow;
        protected TextWidget headerLabel;
        protected Button cancelButton;
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

        public Printer ActivePrinter 
        { 
            get { return PrinterSetupStatus.ActivePrinter; } 
        }

        public SetupConnectionWidgetBase(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus printerSetupStatus = null)
            : base(windowController, containerWindowToClose)
        {
            SetDisplayAttributes();

            if (printerSetupStatus == null)
            {
                this.PrinterSetupStatus = new PrinterSetupStatus();
            }
            else
            {
                this.PrinterSetupStatus = printerSetupStatus;
            }

            
			cancelButton = textImageButtonFactory.Generate (LocalizedString.Get ("Cancel"));
            cancelButton.Click += new EventHandler(CancelButton_Click);

            //Create the main container
            GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.AnchorAll();
			mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
            mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            //Create the header row for the widget
            headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            {
				string defaultHeaderTitle = LocalizedString.Get("3D Printer Setup");                
                headerLabel = new TextWidget(defaultHeaderTitle, pointSize: 14);
                headerLabel.AutoExpandBoundsToText = true;
                headerLabel.TextColor = this.defaultTextColor;
                headerRow.AddChild(headerLabel);
            }

            //Create the main control container
            contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom);
            contentRow.Padding = new BorderDouble(5);
            contentRow.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            contentRow.HAnchor = HAnchor.ParentLeftRight;
            contentRow.VAnchor = VAnchor.ParentBottomTop;

            //Create the footer (button) container
            footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            footerRow.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
            footerRow.Margin = new BorderDouble(0, 3);

            mainContainer.AddChild(headerRow);
            mainContainer.AddChild(contentRow);
            mainContainer.AddChild(footerRow);
            this.AddChild(mainContainer);
        }

        protected void SaveAndExit()
        {
            this.ActivePrinter.Commit();
            ActivePrinterProfile.Instance.ActivePrinter = this.ActivePrinter;
            this.containerWindowToClose.Close();

        }

        private void SetDisplayAttributes()
        {
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.borderWidth = 0;

			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
            linkButtonFactory.fontSize = 10;

            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            this.AnchorAll();
            this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
        }

        void CloseWindow(object o, EventArgs e)
        {
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            this.containerWindowToClose.Close();
        }

        void CancelButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            if (GetPrinterRecordCount() > 0)
            {
                this.windowController.ChangeToChoosePrinter();
            }
            else
            {
                UiThread.RunOnIdle((state) =>
                {
                    Parent.Close();
                });
            }
        }
    }
}
