using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepBaudRate : ConnectionWizardPage
	{
		private List<RadioButton> BaudRateButtonsList = new List<RadioButton>();
		private FlowLayoutWidget printerBaudRateContainer;
		private TextWidget printerBaudRateError;
		private GuiWidget baudRateWidget;
		private RadioButton otherBaudRateRadioButton;
		private MHTextEditWidget otherBaudRateInput;
		private Button nextButton;
		private Button printerBaudRateHelpLink;
		private TextWidget printerBaudRateHelpMessage;

		public SetupStepBaudRate()
		{
			printerBaudRateContainer = createPrinterBaudRateContainer();
			contentRow.AddChild(printerBaudRateContainer);
			{
				nextButton = textImageButtonFactory.Generate("Continue".Localize());
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

			string baudRateLabelText = "Baud Rate".Localize();
			string baudRateLabelTextFull = string.Format("{0}:", baudRateLabelText);

			TextWidget baudRateLabel = new TextWidget(baudRateLabelTextFull, 0, 0, 12);
			baudRateLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			baudRateLabel.Margin = new BorderDouble(0, 0, 0, 10);
			baudRateLabel.HAnchor = HAnchor.ParentLeftRight;

			baudRateWidget = GetBaudRateWidget();
			baudRateWidget.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget baudRateMessageContainer = new FlowLayoutWidget();
			baudRateMessageContainer.Margin = elementMargin;
			baudRateMessageContainer.HAnchor = HAnchor.ParentLeftRight;

			printerBaudRateError = new TextWidget("Select the baud rate.".Localize(), 0, 0, 10);
			printerBaudRateError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerBaudRateError.AutoExpandBoundsToText = true;

			printerBaudRateHelpLink = linkButtonFactory.Generate("What's this?".Localize());
			printerBaudRateHelpLink.Margin = new BorderDouble(left: 5);
			printerBaudRateHelpLink.VAnchor = VAnchor.ParentBottom;
			printerBaudRateHelpLink.Click += new EventHandler(printerBaudRateHelp_Click);

			printerBaudRateHelpMessage = new TextWidget("The term 'Baud Rate' roughly means the speed at which\ndata is transmitted.  Baud rates may differ from printer to\nprinter. Refer to your printer manual for more info.\n\nTip: If you are uncertain - try 250000.".Localize(), 0, 0, 10);
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
				RadioButton baudOption = new RadioButton(baudRate);
				BaudRateButtonsList.Add(baudOption);
				baudOption.Margin = baudRateMargin;
				baudOption.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				if (ActiveSliceSettings.Instance.GetValue(SettingsKey.baud_rate) == baudRate)
				{
					baudOption.Checked = true;
				}
				baudRateContainer.AddChild(baudOption);
			}

			otherBaudRateRadioButton = new RadioButton("Other".Localize());
			otherBaudRateRadioButton.Margin = baudRateMargin;
			otherBaudRateRadioButton.TextColor = ActiveTheme.Instance.PrimaryTextColor;

			baudRateContainer.AddChild(otherBaudRateRadioButton);

			//See if the baud rate of the current print is in the list of displayed rates,
			//flag the 'other' option if it is not and prefill the rate.
			otherBaudRateInput = new MHTextEditWidget("");
			otherBaudRateInput.Visible = false;
			otherBaudRateInput.HAnchor = HAnchor.ParentLeftRight;

			string currentBaudRate = ActiveSliceSettings.Instance.GetValue(SettingsKey.baud_rate);
			if (currentBaudRate != null)
			{
				if (!baudRates.Contains(currentBaudRate))
				{
					otherBaudRateRadioButton.Checked = true;
					otherBaudRateInput.Text = currentBaudRate;
					otherBaudRateInput.Visible = true;
				}
			}

			baudRateContainer.AddChild(otherBaudRateInput);
			return baudRateContainer;
		}

		private void BindBaudRateHandlers()
		{
			otherBaudRateRadioButton.CheckedStateChanged += BindBaudRate_Select;
			foreach (RadioButton button in BaudRateButtonsList)
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

		private void MoveToNextWidget()
		{
			WizardWindow.ChangeToInstallDriverOrComPortOne();
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
				printerBaudRateError.Text = "Oops! Please select a baud rate.".Localize();
			}

			if (baudRate != null)
			{
				try
				{
					ActiveSliceSettings.Instance.Helpers.SetBaudRate(baudRate);
					return true;
				}
				catch
				{
					printerBaudRateHelpLink.Visible = false;
					printerBaudRateError.TextColor = RGBA_Bytes.Red;
					printerBaudRateError.Text = "Oops! Baud Rate must be an integer.".Localize();
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
			foreach (RadioButton button in BaudRateButtonsList)
			{
				if (button.Checked)
				{
					return button.Text;
				}
			}
			if (otherBaudRateRadioButton.Checked)
			{
				return otherBaudRateInput.Text;
			}

			throw new Exception("Could not find a selected button.".Localize());
		}
	}
}