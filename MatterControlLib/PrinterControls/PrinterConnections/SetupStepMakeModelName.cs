/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	// Normally step one of the setup process
	public class SetupStepMakeModelName : DialogPage
	{
		private TextButton nextButton;
		private AddPrinterWidget printerPanel;
		private bool usingDefaultName;

		private static BorderDouble elementMargin = new BorderDouble(top: 3);

		private RadioButton createPrinterRadioButton = null;
		private RadioButton signInRadioButton;

		public SetupStepMakeModelName()
		{
			bool userIsLoggedIn = !ApplicationController.GuestUserActive?.Invoke() ?? false;

			this.HeaderText = this.WindowTitle = "Printer Setup".Localize();
			this.WindowSize = new VectorMath.Vector2(800, 600);

			contentRow.BackgroundColor = theme.SectionBackgroundColor;
			nextButton = theme.CreateDialogButton("Next".Localize());

			printerPanel = new AddPrinterWidget(theme, nextButton)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			if (userIsLoggedIn)
			{
				contentRow.AddChild(printerPanel);
				contentRow.Padding = 0;
			}
			else
			{
				contentRow.Padding = theme.DefaultContainerPadding;
				printerPanel.Margin = new BorderDouble(left: 15, top: theme.DefaultContainerPadding);

				var commonMargin = new BorderDouble(4, 2);

				// Create export button for each plugin
				signInRadioButton = new RadioButton(new RadioButtonViewText("Sign in to access your existing printers".Localize(), theme.TextColor))
				{
					HAnchor = HAnchor.Left,
					Margin = commonMargin.Clone(bottom: 10),
					Cursor = Cursors.Hand,
					Name = "Sign In Radio Button",
				};
				contentRow.AddChild(signInRadioButton);

				createPrinterRadioButton = new RadioButton(new RadioButtonViewText("Create a new printer", theme.TextColor))
				{
					HAnchor = HAnchor.Left,
					Margin = commonMargin,
					Cursor = Cursors.Hand,
					Name = "Create Printer Radio Button",
					Checked = true
				};
				contentRow.AddChild(createPrinterRadioButton);

				createPrinterRadioButton.CheckedStateChanged += (s, e) =>
				{
					printerPanel.Enabled = createPrinterRadioButton.Checked;
					this.SetElementVisibility();
				};

				contentRow.AddChild(printerPanel);
			}

			nextButton.Name = "Next Button";
			nextButton.Click += (s, e) => UiThread.RunOnIdle(async () =>
			{
				if (signInRadioButton?.Checked == true)
				{
					var authContext = new AuthenticationContext();
					authContext.SignInComplete += (s2, e2) =>
					{
						this.DialogWindow.ChangeToPage(new OpenPrinterPage("Finish".Localize()));
					};

					this.DialogWindow.ChangeToPage(ApplicationController.GetAuthPage(authContext));
				}
				else
				{
					bool controlsValid = printerPanel.ValidateControls();
					if (controlsValid
						&& printerPanel.SelectedPrinter is AddPrinterWidget.MakeModelInfo selectedPrinter)
					{
						var printer = await ProfileManager.CreatePrinterAsync(selectedPrinter.Make, selectedPrinter.Model, printerPanel.NewPrinterName);
						if (printer == null)
						{
							printerPanel.SetError("Error creating profile".Localize());
							return;
						}

						UiThread.RunOnIdle(() =>
						{
							DialogWindow.ChangeToPage(AppContext.Platform.GetConnectDevicePage(printer) as DialogPage);
						});
					}
				}
			});

			this.AddPageAction(nextButton);

			usingDefaultName = true;

			SetElementVisibility();
		}

		private void SetElementVisibility()
		{
			nextButton.Enabled = signInRadioButton?.Checked == true
				|| printerPanel.SelectedPrinter != null;
		}

		private FlowLayoutWidget CreateSelectionContainer(string labelText, string validationMessage, DropDownList selector)
		{
			var sectionLabel = new TextWidget(labelText, 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};

			var validationTextWidget = new TextWidget(validationMessage, 0, 0, 10)
			{
				TextColor = theme.PrimaryAccentColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};

			selector.SelectionChanged += (s, e) =>
			{
				validationTextWidget.Visible = selector.SelectedLabel.StartsWith("-"); // The default values have "- Title -"
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 5),
				HAnchor = HAnchor.Stretch
			};

			container.AddChild(sectionLabel);
			container.AddChild(selector);
			container.AddChild(validationTextWidget);

			return container;
		}
	}
}