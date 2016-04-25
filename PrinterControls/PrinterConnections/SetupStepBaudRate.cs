﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepBaudRate : SetupConnectionWidgetBase
	{
		private List<BaudRateRadioButton> BaudRateButtonsList = new List<BaudRateRadioButton>();
		private FlowLayoutWidget printerBaudRateContainer;
		private TextWidget printerBaudRateError;
		private GuiWidget baudRateWidget;
		private RadioButton otherBaudRateRadioButton;
		private MHTextEditWidget otherBaudRateInput;
		private Button nextButton;
		private Button printerBaudRateHelpLink;
		private TextWidget printerBaudRateHelpMessage;

		public SetupStepBaudRate(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinterStatus)
			: base(windowController, containerWindowToClose, setupPrinterStatus)
		{
			linkButtonFactory.fontSize = 8;

			printerBaudRateContainer = createPrinterBaudRateContainer();
			contentRow.AddChild(printerBaudRateContainer);
			{
				nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Continue"));
				nextButton.Click += new EventHandler(NextButton_Click);

				//Add buttons to buttonContainer
				footerRow.AddChild(nextButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
			BindBaudRateHandlers();
		}

		private FlowLayoutWidget createPrinterBaudRateContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0);
			container.VAnchor = VAnchor.ParentBottomTop;
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string baudRateLabelText = LocalizedString.Get("Baud Rate");
			string baudRateLabelTextFull = string.Format("{0}:", baudRateLabelText);

			TextWidget baudRateLabel = new TextWidget(baudRateLabelTextFull, 0, 0, 12);
			baudRateLabel.TextColor = this.defaultTextColor;
			baudRateLabel.Margin = new BorderDouble(0, 0, 0, 10);
			baudRateLabel.HAnchor = HAnchor.ParentLeftRight;

			baudRateWidget = GetBaudRateWidget();
			baudRateWidget.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget baudRateMessageContainer = new FlowLayoutWidget();
			baudRateMessageContainer.Margin = elementMargin;
			baudRateMessageContainer.HAnchor = HAnchor.ParentLeftRight;

			printerBaudRateError = new TextWidget(LocalizedString.Get("Select the baud rate."), 0, 0, 10);
			printerBaudRateError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerBaudRateError.AutoExpandBoundsToText = true;

			printerBaudRateHelpLink = linkButtonFactory.Generate(LocalizedString.Get("What's this?"));
			printerBaudRateHelpLink.Margin = new BorderDouble(left: 5);
			printerBaudRateHelpLink.VAnchor = VAnchor.ParentBottom;
			printerBaudRateHelpLink.Click += new EventHandler(printerBaudRateHelp_Click);

			printerBaudRateHelpMessage = new TextWidget(LocalizedString.Get("The term 'Baud Rate' roughly means the speed at which\ndata is transmitted.  Baud rates may differ from printer to\nprinter. Refer to your printer manual for more info.\n\nTip: If you are uncertain - try 250000."), 0, 0, 10);
			printerBaudRateHelpMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerBaudRateHelpMessage.Margin = new BorderDouble(top: 10);
			printerBaudRateHelpMessage.Visible = false;

			baudRateMessageContainer.AddChild(printerBaudRateError);
			baudRateMessageContainer.AddChild(printerBaudRateHelpLink);

			container.AddChild(baudRateLabel);
			container.AddChild(baudRateWidget);
			container.AddChild(baudRateMessageContainer);
			container.AddChild(printerBaudRateHelpMessage);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void printerBaudRateHelp_Click(object sender, EventArgs mouseEvent)
		{
			printerBaudRateHelpMessage.Visible = !printerBaudRateHelpMessage.Visible;
		}

		public GuiWidget GetBaudRateWidget()
		{
			FlowLayoutWidget baudRateContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			baudRateContainer.Margin = new BorderDouble(0);

			List<string> baudRates = new List<string> { "115200", "250000" };
			BorderDouble baudRateMargin = new BorderDouble(3, 3, 5, 0);

			foreach (string baudRate in baudRates)
			{
				BaudRateRadioButton baudOption = new BaudRateRadioButton(baudRate);
				BaudRateButtonsList.Add(baudOption);
				baudOption.Margin = baudRateMargin;
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
			otherBaudRateInput.HAnchor = HAnchor.ParentLeftRight;

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
			otherBaudRateRadioButton.CheckedStateChanged += BindBaudRate_Select;
			foreach (BaudRateRadioButton button in BaudRateButtonsList)
			{
				button.CheckedStateChanged += BindBaudRate_Select;
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

		private void RecreateCurrentWidget()
		{
			// you can call this like this
			//             AfterUiEvents.AddAction(new AfterUIAction(RecreateCurrentWidget));

			Parent.AddChild(new EditConnectionWidget((ConnectionWindow)Parent, Parent, ActivePrinter));
			Parent.RemoveChild(this);
		}

		private void ReloadCurrentWidget(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(RecreateCurrentWidget);
		}

		private void MoveToNextWidget()
		{
			// you can call this like this
			//             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));

			if (this.currentPrinterSetupStatus.DriversToInstall.Count > 0)
			{
				Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
				Parent.RemoveChild(this);
			}
			else
			{
				Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
				Parent.RemoveChild(this);
			}
		}

		private void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			bool canContinue = this.OnSave();
			if (canContinue)
			{
				UiThread.RunOnIdle(MoveToNextWidget);
			}
		}

		private bool OnSave()
		{
			string baudRate = null;
			try
			{
				baudRate = GetSelectedBaudRate();
			}
			catch
			{
				printerBaudRateHelpLink.Visible = false;
				printerBaudRateError.TextColor = RGBA_Bytes.Red;
				printerBaudRateError.Text = LocalizedString.Get("Oops! Please select a baud rate.");
			}

			if (baudRate != null)
			{
				try
				{
					int baudRateInt = Convert.ToInt32(baudRate);
					this.ActivePrinter.BaudRate = baudRate;
					return true;
				}
				catch
				{
					printerBaudRateHelpLink.Visible = false;
					printerBaudRateError.TextColor = RGBA_Bytes.Red;
					printerBaudRateError.Text = LocalizedString.Get("Oops! Baud Rate must be an integer.");
					return false;
				}
			}
			else
			{
				return false;
			}
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
	}
}