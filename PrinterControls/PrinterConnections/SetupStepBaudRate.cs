/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepBaudRate : WizardPage
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

		private PrinterConfig printer;

		public SetupStepBaudRate(PrinterConfig printer)
		{
			this.printer = printer;
			printerBaudRateContainer = createPrinterBaudRateContainer();
			contentRow.AddChild(printerBaudRateContainer);
			{
				nextButton = textImageButtonFactory.Generate("Continue".Localize());
				nextButton.Click += (s, e) =>
				{
					bool canContinue = this.OnSave();
					if (canContinue)
					{
						UiThread.RunOnIdle(() =>
						{
							if (SetupStepInstallDriver.PrinterDrivers(printer).Count > 0
								&& AggContext.OperatingSystem == OSType.Windows)
							{
								this.WizardWindow.ChangeToPage(new SetupStepInstallDriver(printer));
							}
							else
							{
								this.WizardWindow.ChangeToPage(new SetupStepComPortOne(printer));
							}
						});
					}
				};

				this.AddPageAction(nextButton);
			}
			BindBaudRateHandlers();
		}

		private FlowLayoutWidget createPrinterBaudRateContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0);
			container.VAnchor = VAnchor.Stretch;
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string baudRateLabelText = "Baud Rate".Localize();
			string baudRateLabelTextFull = string.Format("{0}:", baudRateLabelText);

			TextWidget baudRateLabel = new TextWidget(baudRateLabelTextFull, 0, 0, 12);
			baudRateLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			baudRateLabel.Margin = new BorderDouble(0, 0, 0, 10);
			baudRateLabel.HAnchor = HAnchor.Stretch;

			baudRateWidget = GetBaudRateWidget();
			baudRateWidget.HAnchor = HAnchor.Stretch;

			FlowLayoutWidget baudRateMessageContainer = new FlowLayoutWidget();
			baudRateMessageContainer.Margin = elementMargin;
			baudRateMessageContainer.HAnchor = HAnchor.Stretch;

			printerBaudRateError = new TextWidget("Select the baud rate.".Localize(), 0, 0, 10);
			printerBaudRateError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerBaudRateError.AutoExpandBoundsToText = true;

			printerBaudRateHelpLink = linkButtonFactory.Generate("What's this?".Localize());
			printerBaudRateHelpLink.Margin = new BorderDouble(left: 5);
			printerBaudRateHelpLink.VAnchor = VAnchor.Bottom;
			printerBaudRateHelpLink.Click += printerBaudRateHelp_Click;

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

			container.HAnchor = HAnchor.Stretch;
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
			otherBaudRateInput.HAnchor = HAnchor.Stretch;

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
				printerBaudRateError.TextColor = Color.Red;
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
					printerBaudRateError.TextColor = Color.Red;
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