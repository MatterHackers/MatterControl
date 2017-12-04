﻿/*
Copyright (c) 2016, Greg Diaz
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
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ShowAuthPanel : DialogPage
	{
		public ShowAuthPanel()
			: base ("Skip".Localize())
		{
			this.WindowTitle = "Setup Wizard".Localize();

			WrappedTextWidget userSignInPromptLabel = new WrappedTextWidget("Sign in to access your cloud printer profiles.\n\nOnce signed in you will be able to access".Localize() + ":")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
			};
			contentRow.AddChild(userSignInPromptLabel);

			AddBulletPointAndDescription(contentRow,
				"Cloud Library".Localize(),
				"Save your designs to the cloud and access them from anywhere in the world. You can also share them any time with with anyone you want.".Localize());
			AddBulletPointAndDescription(contentRow,
				"Cloud Printer Profiles".Localize(),
				"Create your machine settings once, and have them available anywhere you want to print. All your changes appear on all your devices.".Localize());
			AddBulletPointAndDescription(contentRow,
				"Remote Monitoring".Localize(),
				"Check on your prints from anywhere. With cloud monitoring, you have access to your printer no matter where you go.".Localize());

			contentRow.AddChild(new VerticalSpacer());

			CheckBox rememberChoice = new CheckBox("Don't remind me again".Localize(), ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(rememberChoice);
			rememberChoice.CheckedStateChanged += (s, e) =>
			{
				ApplicationSettings.Instance.set(ApplicationSettingsKey.SuppressAuthPanel, rememberChoice.Checked.ToString());
			};

			this.SetCancelButtonName("Connection Wizard Skip Sign In Button");

			var createAccountButton = textImageButtonFactory.Generate("Create Account".Localize());
			createAccountButton.Name = "Create Account From Connection Wizard Button";
			createAccountButton.Margin = new Agg.BorderDouble(right: 5);
			createAccountButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle (() => 
				{
					WizardWindow.Close();
					PrinterSetup.ChangeToAccountCreate();
				});
			};

			var signInButton = textImageButtonFactory.Generate("Sign In".Localize());
			signInButton.Name = "Sign In From Connection Wizard Button";
			signInButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle (() => 
				{
					WizardWindow.Close();
					PrinterSetup.ShowAuthDialog?.Invoke();
				});
			};

			this.AddPageAction(createAccountButton);
			this.AddPageAction(signInButton);
		}

		protected override void OnCancel(out bool abortCancel)
		{
			if (!ProfileManager.Instance.ActiveProfiles.Any())
			{
				abortCancel = true;

				UiThread.RunOnIdle(() =>
				{
					WizardWindow.ChangeToPage<SetupStepMakeModelName>();
				});
			}

			abortCancel = false;
		}

		private void AddBulletPointAndDescription(FlowLayoutWidget contentRow, string v1, string v2)
		{
			contentRow.AddChild(new TextWidget("• " + v1)
			{
				HAnchor = HAnchor.Left,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new Agg.BorderDouble(0, 0, 0, 10),
			});
			contentRow.AddChild(new WrappedTextWidget(v2)
			{
				TextColor = ActiveTheme.Instance.SecondaryTextColor,
				Margin = new Agg.BorderDouble(20, 5, 5, 5),
			});
		}
	}
}
 