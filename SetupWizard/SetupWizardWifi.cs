/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	//Normally step one of the setup process
	public class SetupWizardWifi : WizardPage
	{
		public SetupWizardWifi()
		{
			contentRow.AddChild(new TextWidget("Wifi Setup".Localize() + ":", 0, 0, labelFontSize)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(bottom: 10)
			});

			contentRow.AddChild(new TextWidget("Some features may require an internet connection.".Localize(), 0, 0, 12, textColor: ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget("Would you like to setup Wifi?".Localize(), 0, 0, 12, textColor: ActiveTheme.Instance.PrimaryTextColor));

			var connectButtonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 6)
			};

			//Construct buttons
			Button skipButton = whiteImageButtonFactory.Generate("Skip".Localize(), centerText: true);
			skipButton.Click += (s, e) => this.WizardWindow.ChangeToSetupPrinterForm();

			Button nextButton = textImageButtonFactory.Generate("Continue".Localize());
			nextButton.Click += (s, e) => this.WizardWindow.ChangeToSetupPrinterForm();
			nextButton.Visible = false;

			Button configureButton = whiteImageButtonFactory.Generate(LocalizedString.Get("Configure"), centerText: true);
			configureButton.Margin = new BorderDouble(0, 0, 10, 0);
			configureButton.Click += (s, e) =>
			{
				nextButton.Visible = true;
				skipButton.Visible = false;
				configureButton.Visible = false;
				MatterControlApplication.Instance.ConfigureWifi();
			};

			connectButtonContainer.AddChild(configureButton);
			connectButtonContainer.AddChild(skipButton);
			connectButtonContainer.AddChild(new HorizontalSpacer());

			contentRow.AddChild(connectButtonContainer);

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}
	}
}
