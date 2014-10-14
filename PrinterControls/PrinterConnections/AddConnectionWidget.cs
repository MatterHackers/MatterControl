using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class EditConnectionWidget : ConnectionWidgetBase
    {
        List<SerialPortIndexRadioButton> SerialPortButtonsList = new List<SerialPortIndexRadioButton>();
        List<BaudRateRadioButton> BaudRateButtonsList = new List<BaudRateRadioButton>();
        FlowLayoutWidget ConnectionControlContainer;
        Printer ActivePrinter;
        MHTextEditWidget printerNameInput;
		MHTextEditWidget otherBaudRateInput;
        MHTextEditWidget printerModelInput;
        MHTextEditWidget printerMakeInput;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        Button saveButton;
        Button cancelButton;
        GuiWidget comPortLabelWidget;
        GuiWidget baudRateWidget;
		RadioButton otherBaudRateRadioButton;
        CheckBox enableAutoconnect;
        bool printerComPortIsAvailable = false;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        bool addNewPrinterFlag = false;

        public EditConnectionWidget(ConnectionWindow windowController, GuiWidget containerWindowToClose, Printer activePrinter = null, object state = null)
            : base(windowController, containerWindowToClose)
        {
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.borderWidth = 0;

			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
            linkButtonFactory.fontSize = 8;

            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            this.AnchorAll();
            this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off

            GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.AnchorAll();
            mainContainer.Padding = new BorderDouble(3, 0, 3, 5);
            mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            string headerTitle;

            if (activePrinter == null)
            {
                headerTitle = string.Format("Add a Printer");
                this.addNewPrinterFlag = true;
                this.ActivePrinter = new Printer();
                this.ActivePrinter.Name = "Default Printer";
                this.ActivePrinter.BaudRate = "250000";
                try
                {
                    this.ActivePrinter.ComPort = FrostedSerialPort.GetPortNames()[0];
                }
                catch
                {
                    //No active COM ports
                }
            }
            else
            {
                this.ActivePrinter = activePrinter;
                string editHeaderTitleTxt = LocalizedString.Get("Edit"); 
				headerTitle = string.Format("{1} - {0}", this.ActivePrinter.Name, editHeaderTitleTxt);
                if (this.ActivePrinter.BaudRate == null)
                {
                    this.ActivePrinter.BaudRate = "250000";
                }
                if (this.ActivePrinter.ComPort == null)
                {
                    try
                    {
                        this.ActivePrinter.ComPort = FrostedSerialPort.GetPortNames()[0];
                    }
                    catch
                    {
                        //No active COM ports
                    }
                }
            }

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            {

                TextWidget headerLabel = new TextWidget(headerTitle, pointSize:14);
                headerLabel.TextColor = this.defaultTextColor;

                headerRow.AddChild(headerLabel);
            }

            ConnectionControlContainer = new FlowLayoutWidget(FlowDirection.TopToBottom); 
            ConnectionControlContainer.Padding = new BorderDouble(5);
            ConnectionControlContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            ConnectionControlContainer.HAnchor = HAnchor.ParentLeftRight;            
            {
                TextWidget printerNameLabel = new TextWidget(LocalizedString.Get("Printer Name"), 0, 0, 10);
                printerNameLabel.TextColor = this.defaultTextColor;
                printerNameLabel.HAnchor = HAnchor.ParentLeftRight;
                printerNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

                printerNameInput = new MHTextEditWidget(this.ActivePrinter.Name);
                
                printerNameInput.HAnchor |= HAnchor.ParentLeftRight;

                comPortLabelWidget = new FlowLayoutWidget();

                Button refreshComPorts = linkButtonFactory.Generate(LocalizedString.Get("(refresh)"));
                refreshComPorts.Margin = new BorderDouble(left: 5);
                refreshComPorts.VAnchor = VAnchor.ParentBottom;
                refreshComPorts.Click += new ButtonBase.ButtonEventHandler(RefreshComPorts);

                TextWidget comPortLabel = new TextWidget(LocalizedString.Get("Serial Port"), 0, 0, 10);
                comPortLabel.TextColor = this.defaultTextColor;

                comPortLabelWidget.AddChild(comPortLabel);
                comPortLabelWidget.AddChild(refreshComPorts);
                comPortLabelWidget.Margin = new BorderDouble(0, 0, 0, 10);
                comPortLabelWidget.HAnchor = HAnchor.ParentLeftRight;

                FlowLayoutWidget comPortContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                comPortContainer.Margin = new BorderDouble(0);
                comPortContainer.HAnchor = HAnchor.ParentLeftRight;
                

                int portIndex = 0;
                foreach (string serialPort in FrostedSerialPort.GetPortNames())
                {
                    //Filter com port list based on usb type (applies to Mac mostly)
                    bool looks_like_mac = serialPort.StartsWith("/dev/tty.");
                    bool looks_like_pc = serialPort.StartsWith("COM");
                    if (looks_like_mac || looks_like_pc)
                    {
                        SerialPortIndexRadioButton comPortOption = createComPortOption(serialPort);
                        comPortContainer.AddChild(comPortOption);
                        portIndex++;
                    }
                }

                //If there are no com ports in the filtered list assume we are missing something and show the unfiltered list
                if (portIndex == 0)
                {

                    foreach (string serialPort in FrostedSerialPort.GetPortNames())
                    {
                        SerialPortIndexRadioButton comPortOption = createComPortOption(serialPort);
                        comPortContainer.AddChild(comPortOption);
                        portIndex++;
                    }
                }

                if (!printerComPortIsAvailable && this.ActivePrinter.ComPort != null)
                {
                    SerialPortIndexRadioButton comPortOption = createComPortOption(this.ActivePrinter.ComPort);
                    comPortOption.Enabled = false;
                    comPortContainer.AddChild(comPortOption);
                    portIndex++;
                }

                //If there are still no com ports show a message to that effect
                if (portIndex == 0)
                {
                    TextWidget comPortOption = new TextWidget(LocalizedString.Get("No COM ports available"));
                    comPortOption.Margin = new BorderDouble(3, 6, 5, 6);
                    comPortOption.TextColor = this.subContainerTextColor;
                    comPortContainer.AddChild(comPortOption);
                }


                TextWidget baudRateLabel = new TextWidget(LocalizedString.Get("Baud Rate"), 0, 0, 10);
                baudRateLabel.TextColor = this.defaultTextColor;
                baudRateLabel.Margin = new BorderDouble(0, 0, 0, 10);
                baudRateLabel.HAnchor = HAnchor.ParentLeftRight;

                baudRateWidget = GetBaudRateWidget();
                baudRateWidget.HAnchor = HAnchor.ParentLeftRight;

                FlowLayoutWidget printerMakeContainer = createPrinterMakeContainer();
                FlowLayoutWidget printerModelContainer = createPrinterModelContainer();

                enableAutoconnect = new CheckBox(LocalizedString.Get("Auto Connect"));
				enableAutoconnect.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                enableAutoconnect.Margin = new BorderDouble(top: 10);
                enableAutoconnect.HAnchor = HAnchor.ParentLeft;
                if (this.ActivePrinter.AutoConnectFlag)
                {
                    enableAutoconnect.Checked = true;
                }

                if (state as StateBeforeRefresh != null)
                {
                    enableAutoconnect.Checked = ((StateBeforeRefresh)state).autoConnect;
                }

				SerialPortControl serialPortScroll = new SerialPortControl();
				serialPortScroll.AddChild(comPortContainer);

                ConnectionControlContainer.VAnchor = VAnchor.ParentBottomTop;
                ConnectionControlContainer.AddChild(printerNameLabel);
                ConnectionControlContainer.AddChild(printerNameInput);
                ConnectionControlContainer.AddChild(printerMakeContainer);
                ConnectionControlContainer.AddChild(printerModelContainer);
                ConnectionControlContainer.AddChild(comPortLabelWidget);
				ConnectionControlContainer.AddChild(serialPortScroll);
                ConnectionControlContainer.AddChild(baudRateLabel);
                ConnectionControlContainer.AddChild(baudRateWidget);
                ConnectionControlContainer.AddChild(enableAutoconnect);
            }

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
            //buttonContainer.VAnchor = VAnchor.BottomTop;
            buttonContainer.Margin = new BorderDouble(0, 3);
            {
                //Construct buttons
                saveButton = textImageButtonFactory.Generate(LocalizedString.Get("Save"));
                //saveButton.VAnchor = VAnchor.Bottom;

                cancelButton = textImageButtonFactory.Generate(LocalizedString.Get("Cancel"));
                //cancelButton.VAnchor = VAnchor.Bottom;
                cancelButton.Click += new ButtonBase.ButtonEventHandler(CancelButton_Click);

                //Add buttons to buttonContainer
                buttonContainer.AddChild(saveButton);
                buttonContainer.AddChild(cancelButton);
            }

            //mainContainer.AddChild(new PrinterChooser());

            mainContainer.AddChild(headerRow);
            mainContainer.AddChild(ConnectionControlContainer);
            mainContainer.AddChild(buttonContainer);

            this.AddChild(mainContainer);

            BindSaveButtonHandlers();
			BindBaudRateHandlers();
        }

        private FlowLayoutWidget createPrinterMakeContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

            TextWidget printerManufacturerLabel = new TextWidget(LocalizedString.Get("Printer Make"), 0, 0, 10);
            printerManufacturerLabel.TextColor = this.defaultTextColor;
            printerManufacturerLabel.HAnchor = HAnchor.ParentLeftRight;
            printerManufacturerLabel.Margin = elementMargin;

            string printerMake = "";
            if (this.ActivePrinter.Make != null)
            {
                printerMake = this.ActivePrinter.Make;
            }

            printerMakeInput = new MHTextEditWidget(printerMake);
            printerMakeInput.HAnchor |= HAnchor.ParentLeftRight;
            printerMakeInput.Margin = elementMargin;

            container.AddChild(printerManufacturerLabel);
            container.AddChild(printerMakeInput);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private FlowLayoutWidget createPrinterModelContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

            TextWidget printerModelLabel = new TextWidget(LocalizedString.Get("Printer Model"), 0, 0, 10);
            printerModelLabel.TextColor = this.defaultTextColor;
            printerModelLabel.HAnchor = HAnchor.ParentLeftRight;
            printerModelLabel.Margin = elementMargin;

            string printerModel = "";
            if (this.ActivePrinter.Model != null)
            {
                printerModel = this.ActivePrinter.Model;
            }

            printerModelInput = new MHTextEditWidget(printerModel);
            printerModelInput.HAnchor |= HAnchor.ParentLeftRight;
            printerModelInput.Margin = elementMargin;

            container.AddChild(printerModelLabel);
            container.AddChild(printerModelInput);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        public GuiWidget GetBaudRateWidget()
        {
            FlowLayoutWidget baudRateContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            baudRateContainer.Margin = new BorderDouble(0);
            
            List<string> baudRates = new List<string> { "115200", "250000"};
			BorderDouble baudRateMargin = new BorderDouble(3,3,5,3);

            foreach (string baudRate in baudRates)
            {
                BaudRateRadioButton baudOption = new BaudRateRadioButton(baudRate);
                BaudRateButtonsList.Add(baudOption);
				baudOption.Margin = baudRateMargin;
                baudOption.HAnchor = HAnchor.ParentLeft;
                baudOption.TextColor = this.subContainerTextColor;                
                if (this.ActivePrinter.BaudRate == baudRate)
                {
                    baudOption.Checked = true;
                }
                baudRateContainer.AddChild(baudOption);
            }

            otherBaudRateRadioButton = new RadioButton(LocalizedString.Get("Other"));
			otherBaudRateRadioButton.Margin = baudRateMargin;
			otherBaudRateRadioButton.TextColor = this.subContainerTextColor;

			baudRateContainer.AddChild(otherBaudRateRadioButton);

			//See if the baud rate of the current print is in the list of displayed rates,
			//flag the 'other' option if it is not and prefill the rate.
            otherBaudRateInput = new MHTextEditWidget("");
            otherBaudRateInput.Visible = false;
            otherBaudRateInput.HAnchor |= HAnchor.ParentLeftRight;

            if (this.ActivePrinter.BaudRate != null)
            {
                if (!baudRates.Contains(this.ActivePrinter.BaudRate.ToString()))
                {
                    otherBaudRateRadioButton.Checked = true;
                    otherBaudRateInput.Text = this.ActivePrinter.BaudRate.ToString();
                    otherBaudRateInput.Visible = true;
                }
            }

			baudRateContainer.AddChild(otherBaudRateInput);
            return baudRateContainer;
        }

		private void BindBaudRateHandlers()
		{
			otherBaudRateRadioButton.CheckedStateChanged += new RadioButton.CheckedStateChangedEventHandler(BindBaudRate_Select);
			foreach(BaudRateRadioButton button in BaudRateButtonsList)
			{
				button.CheckedStateChanged += new RadioButton.CheckedStateChangedEventHandler(BindBaudRate_Select);
			}
			BindBaudRate_Select(null, null);
		}
		
		private void BindBaudRate_Select(object sender, EventArgs e)
		{
			if (otherBaudRateRadioButton.Checked == true)
			{
				otherBaudRateInput.Visible = true;
			}
			else
			{
				otherBaudRateInput.Visible = false;
			}
		}

        public SerialPortIndexRadioButton createComPortOption(string serialPort)
        {
            //Add formatting here to make port names prettier
            string portName = serialPort;

            SerialPortIndexRadioButton comPortOption = new SerialPortIndexRadioButton(portName, serialPort);
            SerialPortButtonsList.Add(comPortOption);

            comPortOption.HAnchor = HAnchor.ParentLeft;
            comPortOption.Margin = new BorderDouble(3, 3, 5, 3);
            comPortOption.TextColor = this.subContainerTextColor;
                        
            if (this.ActivePrinter.ComPort == serialPort)
            {
                comPortOption.Checked = true;
                printerComPortIsAvailable = true;
            }
            return comPortOption;
        }

        internal class StateBeforeRefresh
        {
            internal bool autoConnect;

            internal StateBeforeRefresh(bool autoConnect)
            {
                this.autoConnect = autoConnect;
            }
        }

        void RefreshComPorts(object sender, MouseEventArgs mouseEvent)
        {
            try
            {
                this.ActivePrinter.Name = printerNameInput.Text;
                this.ActivePrinter.BaudRate = GetSelectedBaudRate();
                this.ActivePrinter.ComPort = GetSelectedSerialPort();
            }
            catch
            {

            }
            this.windowController.ChangedToEditPrinter(this.ActivePrinter, new StateBeforeRefresh(enableAutoconnect.Checked));
        }

        void ReloadCurrentWidget(object sender, MouseEventArgs mouseEvent)
        {
            if (this.addNewPrinterFlag == true)
            {
                this.windowController.ChangeToAddPrinter();
            }
            else
            {
                this.windowController.ChangedToEditPrinter(this.ActivePrinter);
            }            
        }

        void BindSaveButtonHandlers()
        {
            saveButton.UnbindClickEvents();
            saveButton.Text = "Save";
            saveButton.Enabled = true;
            saveButton.Click += new ButtonBase.ButtonEventHandler(SaveButton_Click);
        }

        void CloseWindow(object o, MouseEventArgs e)
        {
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            this.containerWindowToClose.Close();
        }

        void CancelButton_Click(object sender, MouseEventArgs mouseEvent)
        {            
            if (GetPrinterRecordCount() > 0)
            {
                this.windowController.ChangeToChoosePrinter();
            }
            else
            {
                this.containerWindowToClose.Close();
            }
        }

        void SaveButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            this.ActivePrinter.Name = printerNameInput.Text;
            try
            {
                this.ActivePrinter.BaudRate = GetSelectedBaudRate();
                this.ActivePrinter.ComPort = GetSelectedSerialPort();
                this.ActivePrinter.Make = printerMakeInput.Text;
                this.ActivePrinter.Model = printerModelInput.Text;
                this.ActivePrinter.AutoConnectFlag = enableAutoconnect.Checked;
            }
            catch
            {
                //Unable to retrieve Baud or Port, possibly because they weren't shown as options - needs better handling
            }
            this.ActivePrinter.Commit();

            this.windowController.ChangeToChoosePrinter();
        }
        
        private string GetSelectedBaudRate()
        {
            foreach (BaudRateRadioButton button in BaudRateButtonsList)
            {
                if (button.Checked)
                {
                    return button.BaudRate.ToString();
                }
            }
			if (otherBaudRateRadioButton.Checked)
			{
				return otherBaudRateInput.Text;
			}

            throw new Exception(LocalizedString.Get("Could not find a selected button."));
        }

        private string GetSelectedSerialPort()
        {
            foreach (SerialPortIndexRadioButton button in SerialPortButtonsList)
            {
                if (button.Checked)
                {
                    return button.PortValue;
                }
            }

            throw new Exception(LocalizedString.Get("Could not find a selected button."));
        }
    }

	class SerialPortControl : ScrollableWidget
	{
		FlowLayoutWidget topToBottomItemList;

		public SerialPortControl()
		{                
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			topToBottomItemList.Margin = new BorderDouble(top: 3);

			base.AddChild(topToBottomItemList);
		}

		public void RemoveScrollChildren()
		{
			topToBottomItemList.RemoveAllChildren();
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			FlowLayoutWidget itemHolder = new FlowLayoutWidget();
			itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
			itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			itemHolder.AddChild(child);
			itemHolder.VAnchor = VAnchor.FitToChildren;

			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
		}
	}

}
