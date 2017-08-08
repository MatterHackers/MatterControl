using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	//Normally step one of the setup process
	public class SetupStepMakeModelName : ConnectionWizardPage
	{
		private FlowLayoutWidget printerModelContainer;
		private FlowLayoutWidget printerMakeContainer;

		private MHTextEditWidget printerNameInput;

		private TextWidget printerNameError;

		private Button nextButton;

		private bool usingDefaultName;

		private static BorderDouble elementMargin = new BorderDouble(top: 3);

		private BoundDropList printerManufacturerSelector;
		private BoundDropList printerModelSelector;

		string activeMake;
		string activeModel;
		string activeName;

		public SetupStepMakeModelName()
		{
			printerManufacturerSelector = new BoundDropList(string.Format("- {0} -", "Select Make".Localize()), maxHeight: 200)
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

			printerModelSelector = new BoundDropList(string.Format("- {0} -", "Select Model".Localize()), maxHeight: 200)
			{
				Name = "Select Model",
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin,
				TabStop = true
			};
			printerModelSelector.SelectionChanged += ModelDropList_SelectionChanged;

			printerModelContainer = CreateSelectionContainer("Model".Localize() + ":", "Select the printer model".Localize(), printerModelSelector);

			//Add inputs to main container
			contentRow.AddChild(printerMakeContainer);
			contentRow.AddChild(printerModelContainer);
			contentRow.AddChild(createPrinterNameContainer());

			//Construct buttons
			nextButton = textImageButtonFactory.Generate("Save & Continue".Localize());
			nextButton.Name = "Save & Continue Button";
			nextButton.Click += async (s, e) =>
			{
				bool controlsValid = this.ValidateControls();
				if (controlsValid)
				{
					bool profileCreated = await ProfileManager.CreateProfileAsync(activeMake, activeModel, activeName);
					if(!profileCreated)
					{
						this.printerNameError.Text = "Error creating profile".Localize();
						this.printerNameError.Visible = true;
						return;
					}

					LoadCalibrationPrints();

#if __ANDROID__
					UiThread.RunOnIdle(WizardWindow.ChangeToPage<AndroidConnectDevicePage>);
#else
					if (OsInformation.OperatingSystem == OSType.Windows)
					{
						UiThread.RunOnIdle(WizardWindow.ChangeToPage<SetupStepInstallDriver>);
					}
					else
					{
						UiThread.RunOnIdle(WizardWindow.ChangeToPage<SetupStepComPortOne>);
					}
#endif
				}
			};

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			usingDefaultName = true;

			if (printerManufacturerSelector.MenuItems.Count == 1)
			{
				printerManufacturerSelector.SelectedIndex = 0;
			}

			SetElementVisibility();
		}

		private void SetElementVisibility()
		{
			nextButton.Visible = (activeModel != null && this.activeMake != null);
		}

		private FlowLayoutWidget createPrinterNameContainer()
		{
			TextWidget printerNameLabel = new TextWidget("Name".Localize() + ":", 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 4, 0, 1)
			};

			printerNameInput = new MHTextEditWidget("")
			{
				HAnchor = HAnchor.Stretch,
			};
			printerNameInput.KeyPressed += (s, e) => this.usingDefaultName = false;

			printerNameError = new TextWidget("", 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			};

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			container.AddChild(printerNameLabel);
			container.AddChild(printerNameInput);
			container.AddChild(printerNameError);
			container.HAnchor = HAnchor.Stretch;

			return container;
		}

		private FlowLayoutWidget CreateSelectionContainer(string labelText, string validationMessage, Agg.UI.DropDownList selector)
		{
			var sectionLabel = new TextWidget(labelText, 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};

			var validationTextWidget = new TextWidget(validationMessage, 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.SecondaryAccentColor,
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
					printerNameInput.Text = agg_basics.GetNonCollidingName(existingPrinterNames, $"{mappedMakeText} {activeModel}");
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
				this.printerNameError.TextColor = RGBA_Bytes.Red;
				this.printerNameError.Text = "Printer name cannot be blank".Localize();
				this.printerNameError.Visible = true;

				return false;
			}
		}
	}
}