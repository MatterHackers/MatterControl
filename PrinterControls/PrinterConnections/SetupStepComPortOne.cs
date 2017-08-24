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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortOne : WizardPage
	{
		private Button nextButton;

		public SetupStepComPortOne()
		{
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				//Construct buttons
				nextButton = textImageButtonFactory.Generate("Continue".Localize());
				nextButton.Click += (s, e) => UiThread.RunOnIdle(() =>
				{
					WizardWindow.ChangeToPage<SetupStepComPortTwo>();
				});

				this.AddPageAction(nextButton);
			}
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.Stretch;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			TextWidget printerMessageOne = new TextWidget("MatterControl will now attempt to auto-detect printer.".Localize(), 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.Stretch;
			printerMessageOne.Margin = elementMargin;

			string printerMessageTwoTxt = "Disconnect printer".Localize();
			string printerMessageTwoTxtEnd = "if currently connected".Localize();
			string printerMessageTwoTxtFull = string.Format("1.) {0} ({1}).", printerMessageTwoTxt, printerMessageTwoTxtEnd);
			TextWidget printerMessageTwo = new TextWidget(printerMessageTwoTxtFull, 0, 0, 12);
			printerMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageTwo.HAnchor = HAnchor.Stretch;
			printerMessageTwo.Margin = elementMargin;

			string printerMessageThreeTxt = "Press".Localize();
			string printerMessageThreeTxtEnd = "Continue".Localize();
			string printerMessageThreeFull = string.Format("2.) {0} '{1}'.", printerMessageThreeTxt, printerMessageThreeTxtEnd);
			TextWidget printerMessageThree = new TextWidget(printerMessageThreeFull, 0, 0, 12);
			printerMessageThree.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageThree.HAnchor = HAnchor.Stretch;
			printerMessageThree.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.Stretch;

			string setupManualConfigurationOrSkipConnectionText = LocalizedString.Get(("You can also"));
			string setupManualConfigurationOrSkipConnectionTextFull = String.Format("{0}:", setupManualConfigurationOrSkipConnectionText);
			TextWidget setupManualConfigurationOrSkipConnectionWidget = new TextWidget(setupManualConfigurationOrSkipConnectionTextFull, 0, 0, 10);
			setupManualConfigurationOrSkipConnectionWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupManualConfigurationOrSkipConnectionWidget.HAnchor = HAnchor.Stretch;
			setupManualConfigurationOrSkipConnectionWidget.Margin = elementMargin;

			Button manualLink = linkButtonFactory.Generate("Manually Configure Connection".Localize());
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				WizardWindow.ChangeToPage<SetupStepComPortManual>();
			});

			string printerMessageFourText = "or".Localize();
			TextWidget printerMessageFour = new TextWidget(printerMessageFourText, 0, 0, 10);
			printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFour.HAnchor = HAnchor.Stretch;
			printerMessageFour.Margin = elementMargin;

			Button skipConnectionLink = linkButtonFactory.Generate("Skip Connection Setup".Localize());
			skipConnectionLink.Margin = new BorderDouble(0, 8);
			skipConnectionLink.Click += SkipConnectionLink_Click;

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageTwo);
			container.AddChild(printerMessageThree);
			container.AddChild(vSpacer);
			container.AddChild(setupManualConfigurationOrSkipConnectionWidget);
			container.AddChild(manualLink);
			container.AddChild(printerMessageFour);
			container.AddChild(skipConnectionLink);

			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		private void SkipConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() => {
				PrinterConnection.Instance.HaltConnectionThread();
				Parent.Close();
			});
		}
	}
}