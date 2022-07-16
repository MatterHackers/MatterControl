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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library.Widgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	// Normally step one of the setup process
	public class SetupStepMakeModelName : DialogPage
	{
		private readonly ThemedTextButton nextButton;
		private readonly AddPrinterWidget printerPanel;

		private readonly RadioButton createPrinterRadioButton = null;
		private readonly RadioButton signInRadioButton;

		public SetupStepMakeModelName(bool filterToPulse)
		{
			bool userIsLoggedIn = !ApplicationController.GuestUserActive?.Invoke() ?? false;

			this.HeaderText = this.WindowTitle = "Printer Setup".Localize();
			this.WindowSize = new VectorMath.Vector2(800 * GuiWidget.DeviceScale, 600 * GuiWidget.DeviceScale);

			contentRow.BackgroundColor = theme.SectionBackgroundColor;
			nextButton = theme.CreateDialogButton("Next".Localize());

			printerPanel = new AddPrinterWidget(nextButton, theme, (enabled) =>
			{
				nextButton.Enabled = enabled;
			}, filterToPulse)
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

			var printerNotListedButton = theme.CreateDialogButton("Define New".Localize());
			printerNotListedButton.ToolTipText = "Select this option only if your printer does not appear in the list".Localize();

			printerNotListedButton.Click += async (s, e) =>
			{
				var printer = await ProfileManager.CreatePrinterAsync("Other", "Other", "Custom Printer");
				if (printer == null)
				{
					printerPanel.SetError("Error creating profile".Localize());
					return;
				}

				UiThread.RunOnIdle(() =>
				{
					DialogWindow.ChangeToPage(new SetupCustomPrinter(printer) as DialogPage);
				});
			};

			this.AddPageAction(printerNotListedButton, false);
			this.AddPageAction(nextButton);

			SetElementVisibility();
		}

		private void SetElementVisibility()
		{
			nextButton.Enabled = signInRadioButton?.Checked == true
				|| printerPanel.SelectedPrinter != null;
		}
	}

	public class SetupCustomPrinter : DialogPage
	{
		public SetupCustomPrinter(PrinterConfig printer)
			: base("Done".Localize())
		{
			var scrollable = new ScrollableWidget(autoScroll: true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(scrollable);

			var settingsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};
			scrollable.AddChild(settingsContainer);

			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);
			var menuTheme = ApplicationController.Instance.MenuTheme;
			var tabIndex = 0;

			void AddSettingsRow(string key)
			{
				var settingsRow = SliceSettingsTabView.CreateItemRow(
				PrinterSettings.SettingsData[key],
				settingsContext,
				printer,
				menuTheme,
				ref tabIndex);

				settingsContainer.AddChild(settingsRow);
			}

			void AddSettingsRows(string[] keys)
			{
				foreach (var key in keys)
				{
					AddSettingsRow(key);
				}
			}

			settingsContainer.AddChild(
				new WrappedTextWidget(
					"Set the information below to configure your printer. After completing this step, you can customize additional settings under the 'Settings' and 'Printer' options for this printer.".Localize(),
					pointSize: theme.DefaultFontSize,
					textColor: theme.TextColor)
				{
					Margin = new BorderDouble(5, 5, 5, 15)
				});


			// turn off the port wizard button in this context
			AddSettingsRow(SettingsKey.printer_name);

			settingsContainer.AddChild(new WrappedTextWidget("Bed Settings".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(5, 5, 5, 15)
			});

			AddSettingsRows(new[] { SettingsKey.bed_shape, SettingsKey.bed_size, SettingsKey.print_center, SettingsKey.build_height, SettingsKey.has_heated_bed });
			settingsContainer.AddChild(new WrappedTextWidget("Filament Settings".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(5, 5, 5, 15)
			});
			AddSettingsRows(new[] { SettingsKey.nozzle_diameter, SettingsKey.filament_diameter });
		}
	}
}