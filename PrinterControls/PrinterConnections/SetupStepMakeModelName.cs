using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{   
    //Normally step one of the setup process
    public class SetupStepMakeModelName : SetupConnectionWidgetBase
    {        
        FlowLayoutWidget printerModelContainer;
        FlowLayoutWidget printerMakeContainer;
        FlowLayoutWidget printerNameContainer;

        MHTextEditWidget printerNameInput;

        List<CustomCommands> printerCustomCommands;

        TextWidget printerNameError;
        TextWidget printerModelError;
        TextWidget printerMakeError;
        
        string driverFile;

        Button nextButton;

        public SetupStepMakeModelName(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter = null)
            : base(windowController, containerWindowToClose, setupPrinter)
        {
            //Construct inputs
            printerNameContainer = createPrinterNameContainer();
            printerMakeContainer = createPrinterMakeContainer();
            printerModelContainer = createPrinterModelContainer();

            //Add inputs to main container
            contentRow.AddChild(printerNameContainer);
            contentRow.AddChild(printerMakeContainer);
            contentRow.AddChild(printerModelContainer);

            //Construct buttons
			nextButton = textImageButtonFactory.Generate(new LocalizedString("Save & Continue").Translated);
            nextButton.Click += new ButtonBase.ButtonEventHandler(NextButton_Click);

            GuiWidget hSpacer = new GuiWidget();
            hSpacer.HAnchor = HAnchor.ParentLeftRight;

            //Add buttons to buttonContainer
            footerRow.AddChild(nextButton);
            footerRow.AddChild(hSpacer);
            footerRow.AddChild(cancelButton);

            SetElementState();
        }

        private void SetElementState()
        {
            printerModelContainer.Visible = (this.ActivePrinter.Make != null);
            nextButton.Visible = (this.ActivePrinter.Model != null && this.ActivePrinter.Make !=null);
        }

        private FlowLayoutWidget createPrinterNameContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerNameLabelTxt = new LocalizedString("Printer Name").Translated;
			string printerNameLabelTxtFull = string.Format ("{0}:", printerNameLabelTxt);
			TextWidget printerNameLabel = new TextWidget(printerNameLabelTxtFull, 0, 0, 12);
            printerNameLabel.TextColor = this.defaultTextColor;
            printerNameLabel.HAnchor = HAnchor.ParentLeftRight;
            printerNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

            printerNameInput = new MHTextEditWidget(this.ActivePrinter.Name);
            printerNameInput.HAnchor = HAnchor.ParentLeftRight;

			printerNameError = new TextWidget(new LocalizedString("Give your printer a name.").Translated, 0, 0, 10);
            printerNameError.TextColor = RGBA_Bytes.White;
            printerNameError.HAnchor = HAnchor.ParentLeftRight;
            printerNameError.Margin = elementMargin;

            container.AddChild(printerNameLabel);
            container.AddChild(printerNameInput);
            container.AddChild(printerNameError);
            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private FlowLayoutWidget createPrinterMakeContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerManufacturerLabelTxt = new LocalizedString("Select Make").Translated;
			string printerManufacturerLabelTxtFull = string.Format("{0}:", printerManufacturerLabelTxt);
			TextWidget printerManufacturerLabel = new TextWidget(printerManufacturerLabelTxtFull, 0, 0, 12);
            printerManufacturerLabel.TextColor = this.defaultTextColor;
            printerManufacturerLabel.HAnchor = HAnchor.ParentLeftRight;
            printerManufacturerLabel.Margin = elementMargin;

            PrinterChooser printerManufacturerSelector = new PrinterChooser();
            printerManufacturerSelector.HAnchor = HAnchor.ParentLeftRight;
            printerManufacturerSelector.Margin = elementMargin;
            printerManufacturerSelector.ManufacturerDropList.SelectionChanged += new EventHandler(ManufacturerDropList_SelectionChanged);

			printerMakeError = new TextWidget(new LocalizedString("Select the printer manufacturer").Translated, 0, 0, 10);
            printerMakeError.TextColor = RGBA_Bytes.White;
            printerMakeError.HAnchor = HAnchor.ParentLeftRight;
            printerMakeError.Margin = elementMargin;

            container.AddChild(printerManufacturerLabel);
            container.AddChild(printerManufacturerSelector);
            container.AddChild(printerMakeError);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

		private FlowLayoutWidget createPrinterModelContainer(string make = "Other")
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerModelLabelTxt = new LocalizedString("Select Model").Translated;
			string printerModelLabelTxtFull = string.Format ("{0}:", printerModelLabelTxt);
			TextWidget printerModelLabel = new TextWidget(printerModelLabelTxtFull, 0, 0, 12);
            printerModelLabel.TextColor = this.defaultTextColor;
            printerModelLabel.HAnchor = HAnchor.ParentLeftRight;
            printerModelLabel.Margin = elementMargin;

            ModelChooser printerModelSelector = new ModelChooser(make);
            printerModelSelector.HAnchor = HAnchor.ParentLeftRight;
            printerModelSelector.Margin = elementMargin;
            printerModelSelector.ModelDropList.SelectionChanged += new EventHandler(ModelDropList_SelectionChanged);

			printerModelError = new TextWidget(new LocalizedString("Select the printer model").Translated, 0, 0, 10);
            printerModelError.TextColor = RGBA_Bytes.White;
            printerModelError.HAnchor = HAnchor.ParentLeftRight;
            printerModelError.Margin = elementMargin;

            container.AddChild(printerModelLabel);
            container.AddChild(printerModelSelector);
            container.AddChild(printerModelError);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
        {
            ActivePrinter.Make = ((DropDownList)sender).SelectedValue;
            ActivePrinter.Model = null;

            //reconstruct model selector
            int currentIndex = contentRow.GetChildIndex(printerModelContainer);
            contentRow.RemoveChild(printerModelContainer);

            printerModelContainer = createPrinterModelContainer(ActivePrinter.Make);
            contentRow.AddChild(printerModelContainer, currentIndex);
            contentRow.Invalidate();

            printerMakeError.Visible = false;
            SetElementState();
        }

        private void ModelDropList_SelectionChanged(object sender, EventArgs e)
        {
            ActivePrinter.Model = ((DropDownList)sender).SelectedValue;
            LoadSetupSettings(ActivePrinter.Make, ActivePrinter.Model);            
            printerModelError.Visible = false;
            SetElementState();
        }
        

        private void LoadSetupSettings(string make, string model)
        {
            Dictionary<string, string> settingsDict = LoadPrinterSetupFromFile(make, model);
            Dictionary<string, string> macroDict = new Dictionary<string, string>();
            macroDict["Lights On"] = "M42 P6 S255";
            macroDict["Lights Off"] = "M42 P6 S0";
            
            //Determine if baud rate is needed and show controls if required
            string baudRate;
            if (settingsDict.TryGetValue("baud_rate", out baudRate))
            {
                ActivePrinter.BaudRate = baudRate;
            }

            string defaultSliceEngine;
            if (settingsDict.TryGetValue("default_slice_engine", out defaultSliceEngine))
            {
                if (Enum.IsDefined(typeof(PrinterCommunication.SlicingEngine), defaultSliceEngine))
                {
                    ActivePrinter.CurrentSlicingEngine = defaultSliceEngine;
                }
            }

            string defaultMacros;
            printerCustomCommands = new List<CustomCommands>();
            if (settingsDict.TryGetValue("default_macros", out defaultMacros))
            {
                string[] macroList = defaultMacros.Split(',');
                foreach (string macroName in macroList)
                {
                    string macroValue;
                    if (macroDict.TryGetValue(macroName.Trim(), out macroValue))
                    {
                        CustomCommands customMacro = new CustomCommands();
                        customMacro.Name = macroName.Trim();
                        customMacro.Value = macroValue;

                        printerCustomCommands.Add(customMacro);
                        
                    }
                }
            }

            //Determine what if any drivers are needed
            if (settingsDict.TryGetValue("windows_driver", out driverFile))
            {
                string infPathAndFileToInstall = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Drivers", driverFile);
                switch (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType())
                {
                    case Agg.UI.WindowsFormsAbstract.OSType.Windows:
                        if (File.Exists(infPathAndFileToInstall))
                        {
                            PrinterSetupStatus.DriverNeedsToBeInstalled = true;
                            PrinterSetupStatus.DriverFilePath = infPathAndFileToInstall;
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        

        private Dictionary<string, string> LoadPrinterSetupFromFile(string make, string model)
        {
            string setupSettingsPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", make, model, "setup.ini");
            Dictionary<string, string> settingsDict = new Dictionary<string, string>();

            if (System.IO.File.Exists(setupSettingsPathAndFile))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            settingsDict.Add(keyName, settingDefaultValue);
                        }
                    }
                }
                catch
                {

                }
            }
            return settingsDict;
        }

        private SliceSettingsCollection GetSliceSettings(string make, string model)
        {
            SliceSettingsCollection collection = null;
            Dictionary<string, string> settingsDict = LoadSliceSettingsFromFile(make, model);
            
            if (settingsDict.Count > 0)
            {
                collection = new DataStorage.SliceSettingsCollection();
                collection.Name = this.ActivePrinter.Name;
                collection.Commit();

                this.ActivePrinter.DefaultSettingsCollectionId = collection.Id;
                this.ActivePrinter.Commit();

                foreach (KeyValuePair<string, string> item in settingsDict)
                {
                    DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
                    sliceSetting.Name = item.Key;
                    sliceSetting.Value = item.Value;
                    sliceSetting.SettingsCollectionId = collection.Id;
                    sliceSetting.Commit();
                }
            }
            return collection;

        }

        private Dictionary<string, string> LoadSliceSettingsFromFile(string make, string model)
        {
            string setupSettingsPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", make, model, "config.ini");
            Dictionary<string, string> settingsDict = new Dictionary<string, string>();

            if (System.IO.File.Exists(setupSettingsPathAndFile))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            settingsDict.Add(keyName, settingDefaultValue);
                        }
                    }
                }
                catch
                {

                }
            }
            return settingsDict;
        }


        void MoveToNextWidget(object state)
        {
            // you can call this like this
            //             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));
            if (this.ActivePrinter.BaudRate == null)
            {
                Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else if (this.PrinterSetupStatus.DriverNeedsToBeInstalled)
            {
                Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else
            {
                Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
        }

        void NextButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            bool canContinue = this.OnSave();
            if (canContinue)
            {
                this.PrinterSetupStatus.LoadCalibrationPrints();
                UiThread.RunOnIdle(MoveToNextWidget);
            }
        }

        bool OnSave()
        {
            if (printerNameInput.Text != "")
            {
                this.ActivePrinter.Name = printerNameInput.Text;
                if (this.ActivePrinter.Make == null || this.ActivePrinter.Model == null)
                {
                    return false;
                }
                else
                {
                    //Load the default slice settings for the make and model combination - if they exist
                    SliceSettingsCollection collection = GetSliceSettings(this.ActivePrinter.Make, this.ActivePrinter.Model);
                    if (collection != null)
                    {
                        this.ActivePrinter.DefaultSettingsCollectionId = collection.Id;
                    }
                    this.ActivePrinter.AutoConnectFlag = true;
                    this.ActivePrinter.Commit();

                    foreach (CustomCommands customCommand in printerCustomCommands)
                    {
                        customCommand.PrinterId = ActivePrinter.Id;
                        customCommand.Commit();
                    }

                    return true;
                }
            }
            else
            {
                this.printerNameError.TextColor = RGBA_Bytes.Red;
				this.printerNameError.Text = "Printer name cannot be blank";
                this.printerNameError.Visible = true;
                return false;
            }
        }
    }
}
