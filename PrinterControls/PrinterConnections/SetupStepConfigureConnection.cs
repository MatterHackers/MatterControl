/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepConfigureConnection : SetupConnectionWidgetBase
	{
		private Button nextButton;
		private Button skipButton;
		private TextWidget printerErrorMessage;

		public SetupStepConfigureConnection(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter)
			: base(windowController, containerWindowToClose, setupPrinter)
		{
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				//Construct buttons
				nextButton = textImageButtonFactory.Generate("Connect");
				nextButton.Click += new EventHandler(NextButton_Click);

				skipButton = textImageButtonFactory.Generate("Skip");
				skipButton.Click += new EventHandler(SkipButton_Click);

				//Add buttons to buttonContainer
				footerRow.AddChild(nextButton);
				footerRow.AddChild(skipButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			TextWidget continueMessage = new TextWidget("Would you like to connect to this printer now?", 0, 0, 12);
			continueMessage.AutoExpandBoundsToText = true;
			continueMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			continueMessage.HAnchor = HAnchor.ParentLeftRight;
			continueMessage.Margin = elementMargin;

			TextWidget continueMessageTwo = new TextWidget("You can always configure this later.", 0, 0, 10);
			continueMessageTwo.AutoExpandBoundsToText = true;
			continueMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			continueMessageTwo.HAnchor = HAnchor.ParentLeftRight;
			continueMessageTwo.Margin = elementMargin;

			printerErrorMessage = new TextWidget("", 0, 0, 10);
			printerErrorMessage.AutoExpandBoundsToText = true;
			printerErrorMessage.TextColor = RGBA_Bytes.Red;
			printerErrorMessage.HAnchor = HAnchor.ParentLeftRight;
			printerErrorMessage.Margin = elementMargin;

			container.AddChild(continueMessage);
			container.AddChild(continueMessageTwo);
			container.AddChild(printerErrorMessage);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void SkipButton_Click(object sender, EventArgs e)
		{
			//Save the printer info to the datastore and exit the setup process
			this.ActivePrinter.Commit();
			SaveAndExit();
		}

		private void MoveToNextWidget()
		{
			// you can call this like this
			//             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));
			if (this.ActivePrinter.BaudRate == null)
			{
				Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
				Parent.RemoveChild(this);
			}
			else if (this.currentPrinterSetupStatus.DriversToInstall.Count > 0)
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
			UiThread.RunOnIdle(MoveToNextWidget);
		}
	}
}