/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	//Normally step one of the setup process
	public class SetupStepMakeModelName : DialogPage
	{
		private FlowLayoutWidget printerModelContainer;
		private FlowLayoutWidget printerMakeContainer;

		private MHTextEditWidget printerNameInput;

		private TextWidget printerNameError;

		private GuiWidget nextButton;

		private bool usingDefaultName;

		private static BorderDouble elementMargin = new BorderDouble(top: 3);

		private BoundDropList printerManufacturerSelector;
		private BoundDropList printerModelSelector;

		string activeMake;
		string activeModel;
		string activeName;

		private RadioButton createPrinterRadioButton = null;

		public SetupStepMakeModelName()
		{
			bool userIsLoggedIn = !ApplicationController.GuestUserActive?.Invoke() ?? false;

			this.HeaderText = this.WindowTitle = "Printer Setup".Localize();

			var addPrinterColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			printerManufacturerSelector = new BoundDropList(string.Format("- {0} -", "Select Make".Localize()), theme, maxHeight: 200)
			{
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin,
				Name = "Select Make",
				ListSource = OemSettings.Instance.AllOems,
				TabStop = true
			};

			printerManufacturerSelector.SelectionChanged += ManufacturerDropList_SelectionChanged;

			printerMakeContainer = CreateSelectionContainer(
				"Make".Localize() + ":",
				"Select the printer manufacturer".Localize(),
				printerManufacturerSelector);

			printerModelSelector = new BoundDropList(string.Format("- {0} -", "Select Model".Localize()), theme, maxHeight: 200)
			{
				Name = "Select Model",
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin,
				TabStop = true
			};
			printerModelSelector.SelectionChanged += ModelDropList_SelectionChanged;

			printerModelContainer = CreateSelectionContainer(
				"Model".Localize() + ":",
				"Select the printer model".Localize(),
				printerModelSelector);

			//Add inputs to main container
			addPrinterColumn.AddChild(printerMakeContainer);
			addPrinterColumn.AddChild(printerModelContainer);
			addPrinterColumn.AddChild(createPrinterNameContainer());

			RadioButton signInRadioButton = null;

			if (userIsLoggedIn)
			{
				contentRow.AddChild(addPrinterColumn);
			}
			else
			{
				contentRow.Padding = 10;
				addPrinterColumn.Margin = new BorderDouble(28, 15, 15, 5);

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
					addPrinterColumn.Enabled = createPrinterRadioButton.Checked;
					this.SetElementVisibility();
				};

				contentRow.AddChild(addPrinterColumn);
			}

			nextButton = theme.CreateDialogButton("Next".Localize());
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
					bool controlsValid = this.ValidateControls();
					if (controlsValid)
					{
						var printer = await ProfileManager.CreatePrinterAsync(activeMake, activeModel, activeName);
						if (printer == null)
						{
							this.printerNameError.Text = "Error creating profile".Localize();
							this.printerNameError.Visible = true;
							return;
						}

						this.LoadCalibrationPrints();

#if __ANDROID__
						UiThread.RunOnIdle(() => DialogWindow.ChangeToPage(new AndroidConnectDevicePage(printer)));
#else
						UiThread.RunOnIdle(() =>
						{
							DialogWindow.ChangeToPage(new SetupStepComPortOne(printer));
						});
#endif
					}
				}
			});

			this.AddPageAction(nextButton);

			usingDefaultName = true;

			if (printerManufacturerSelector.MenuItems.Count == 1)
			{
				printerManufacturerSelector.SelectedIndex = 0;
			}

			SetElementVisibility();
		}

		private void SetElementVisibility()
		{
				nextButton.Enabled = createPrinterRadioButton == null
					|| !createPrinterRadioButton.Checked
					|| (createPrinterRadioButton.Checked && (activeModel != null && this.activeMake != null));
		}

		private FlowLayoutWidget createPrinterNameContainer()
		{
			TextWidget printerNameLabel = new TextWidget("Name".Localize() + ":", 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 4, 0, 1)
			};

			printerNameInput = new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch,
			};
			printerNameInput.KeyPressed += (s, e) => this.usingDefaultName = false;

			printerNameError = new TextWidget("", 0, 0, 10)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 5),
				HAnchor = HAnchor.Stretch
			};
			container.AddChild(printerNameLabel);
			container.AddChild(printerNameInput);
			container.AddChild(printerNameError);

			return container;
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

		private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
		{
			activeMake = ((DropDownList)sender).SelectedValue;
			activeModel = null;

			// Select the dictionary containing the printerName->printerToken mappings for the current OEM
			Dictionary<string, PublicDevice> printers;
			if (!OemSettings.Instance.OemProfiles.TryGetValue(activeMake, out printers))
			{
				// Fall back to an empty dictionary if no match
				printers = new Dictionary<string, PublicDevice>();
			}

			// Models - sort dictionary results by key and assign to .ListSource
			printerModelSelector.ListSource = printers.OrderBy(p => p.Key).Select(p => new KeyValuePair<string, string>(p.Key, p.Value.ProfileToken)).ToList();
			if (printerModelSelector.MenuItems.Count == 1)
			{
				printerModelSelector.SelectedIndex = 0;
			}

			contentRow.Invalidate();

			SetElementVisibility();
		}

		private void ModelDropList_SelectionChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				DropDownList dropList = (DropDownList) sender;
				activeModel = dropList.SelectedLabel;

				SetElementVisibility();
				if (usingDefaultName)
				{
					// Use ManufacturerDropList.SelectedLabel instead of activeMake to ensure the mapped Unicode values are picked up
					string mappedMakeText = printerManufacturerSelector.SelectedLabel;

					var existingPrinterNames = ProfileManager.Instance.ActiveProfiles.Select(p => p.Name);
					printerNameInput.Text = agg_basics.GetNonCollidingName($"{mappedMakeText} {activeModel}", existingPrinterNames);
				}
			});
		}

		// TODO: Do we still want to do this - constantly adding items to the queue as printers are added? What about a LibraryContainer for '[PrinterName] Calibration Files' - much cleaner to implement, never an extra files on disk or one-time processing that remain becomes inconsistent over time
		public void LoadCalibrationPrints()
		{
			/*
			// Load the calibration file names
			string calibrationFiles = ActiveSliceSettings.Instance.GetValue("calibration_files");
			if(string.IsNullOrEmpty(calibrationFiles))
			{
				return;
			}

			string[] calibrationPrintFileNames = calibrationFiles.Split(';');
			if (calibrationPrintFileNames.Length < 1)
			{
				return;
			}

			var queueItems = QueueData.Instance.GetItemNames();

			// Finally, ensure missing calibration parts are added to the queue if missing
			var filenamesWithoutExtensions = calibrationPrintFileNames.Select(f => Path.GetFileNameWithoutExtension(f));
			foreach (string nameOnly in filenamesWithoutExtensions)
			{
				if (queueItems.Contains(nameOnly))
				{
					continue;
				}

				// Find the first library item with the given name and add it to the queue
				PrintItem libraryItem = libraryProvider.GetLibraryItems(nameOnly).FirstOrDefault();
				if (libraryItem != null)
				{
					QueueData.Instance.AddItem(new PrintItemWrapper(libraryItem));
				}
			}

			libraryProvider.Dispose();
			*/
		}

		private bool ValidateControls()
		{
			if (!string.IsNullOrEmpty(printerNameInput.Text))
			{
				activeName = printerNameInput.Text;

				if (this.activeMake == null || activeModel == null)
				{
					return false;
				}
				else
				{
					return true;
				}
			}
			else
			{
				this.printerNameError.TextColor = Color.Red;
				this.printerNameError.Text = "Printer name cannot be blank".Localize();
				this.printerNameError.Visible = true;

				return false;
			}
		}
	}
}