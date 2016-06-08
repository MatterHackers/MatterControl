using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using System;
using MatterHackers.MatterControl.SettingsManagement;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	//Normally step one of the setup process
	public class SetupStepMakeModelName : ConnectionWizardPanel
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

		public SetupStepMakeModelName()
		{
			printerManufacturerSelector = new BoundDropList(string.Format("- {0} -", "Select Make".Localize()), maxHeight: 200)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin,
				Name = "Select Make",
				ListSource = OemSettings.Instance.AllOems
			};

			printerManufacturerSelector.SelectionChanged += ManufacturerDropList_SelectionChanged;

			printerMakeContainer = CreateSelectionContainer(
				"Make".Localize() + ":",
				"Select the printer manufacturer".Localize(), 
				printerManufacturerSelector);

			if (printerManufacturerSelector.MenuItems.Count == 1)
			{
				ActivePrinter.Make = printerManufacturerSelector.SelectedValue;
			}

			printerModelSelector = new BoundDropList(string.Format("- {0} -", "Select Model".Localize()), maxHeight: 200)
			{
				Name = "Select Model",
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin,
			};
			printerModelSelector.SelectionChanged += ModelDropList_SelectionChanged;

			if (printerModelSelector.MenuItems.Count == 1)
			{
				// SelectIfOnlyOneModel
				printerModelSelector.SelectedIndex = 0;
			}

			printerModelContainer = CreateSelectionContainer("Model".Localize(), "Select the printer model".Localize(), printerModelSelector);

			//Add inputs to main container
			contentRow.AddChild(printerMakeContainer);
			contentRow.AddChild(printerModelContainer);
			contentRow.AddChild(createPrinterNameContainer());

			//Construct buttons
			nextButton = textImageButtonFactory.Generate("Save & Continue".Localize());
			nextButton.Name = "Save & Continue Button";
			nextButton.Click += (s, e) =>
			{
				bool canContinue = this.OnSave();
				if (canContinue)
				{
#if __ANDROID__
					WizardWindow.ChangeToPanel<SetupWizardConnect>();
#else
					WizardWindow.ChangeToPanel<SetupStepInstallDriver>();
#endif
				}
			};

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			usingDefaultName = true;

			SetElementVisibility();
		}

		private void SetElementVisibility()
		{
			printerModelContainer.Visible = (this.ActivePrinter.Make != null);
			nextButton.Visible = (this.ActivePrinter.Model != null && this.ActivePrinter.Make != null);
		}

		private FlowLayoutWidget createPrinterNameContainer()
		{
			TextWidget printerNameLabel = new TextWidget("Name".Localize() + ":", 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 0, 0, 1)
			};

			printerNameInput = new MHTextEditWidget("")
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			printerNameInput.KeyPressed += (s, e) => this.usingDefaultName = false;

			printerNameError = new TextWidget("", 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(top: 3)
			};

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			container.AddChild(printerNameLabel);
			container.AddChild(printerNameInput);
			container.AddChild(printerNameError);
			container.HAnchor = HAnchor.ParentLeftRight;

			return container;
		}

		private FlowLayoutWidget CreateSelectionContainer(string labelText, string validationMessage, Agg.UI.DropDownList selector)
		{
			var sectionLabel = new TextWidget(labelText, 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			var validationTextWidget = new TextWidget(validationMessage, 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.SecondaryAccentColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			selector.SelectionChanged += (s, e) =>
			{
				validationTextWidget.Visible = selector.SelectedLabel.StartsWith("-"); // The default values have "- Title -"
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 5),
				HAnchor = HAnchor.ParentLeftRight
			};

			container.AddChild(sectionLabel);
			container.AddChild(selector);
			container.AddChild(validationTextWidget);

			return container;
		}

		private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
		{
			ActivePrinter.Make = ((Agg.UI.DropDownList)sender).SelectedValue;
			ActivePrinter.Model = null;

			List<string> printers;
			if (!OemSettings.Instance.OemProfiles.TryGetValue(ActivePrinter.Make, out printers))
			{
				printers = new List<string>();
			}

			printerModelSelector.ListSource = printers.Select(name => new KeyValuePair<string, string>(name, name)).ToList();

			contentRow.Invalidate();

			SetElementVisibility();
		}

		private void ModelDropList_SelectionChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				DropDownList dropList = (DropDownList) sender;
				ActivePrinter.Model = dropList.SelectedLabel;

				SetElementVisibility();
				if (usingDefaultName)
				{
					// Use ManufacturerDropList.SelectedLabel instead of ActivePrinter.Make to ensure the mapped Unicode values are picked up
					string mappedMakeText = printerManufacturerSelector.SelectedLabel;

					string printerInputName = string.Format("{0} {1}", mappedMakeText, this.ActivePrinter.Model);
					var names = ActiveSliceSettings.ProfileData.Profiles.Where(p => p.Name.StartsWith(printerInputName)).Select(p => p.Name).ToList();
					if (!names.Contains(printerInputName))
					{
						printerNameInput.Text = printerInputName;
					}
					else
					{

						int printerModelCount = 1; //Used to keep track of how many of the printer models we run into before and empty one
						string possiblePrinterName;

						do
						{
							possiblePrinterName = string.Format("{0} ({1})", printerInputName, printerModelCount++);
						} while (names.Contains(possiblePrinterName));

						printerNameInput.Text = possiblePrinterName;
					}
				}
			});
		}

		private bool OnSave()
		{
			if (!string.IsNullOrEmpty(printerNameInput.Text))
			{
				this.ActivePrinter.Name = printerNameInput.Text;

				if (this.ActivePrinter.Make == null || this.ActivePrinter.Model == null)
				{
					return false;
				}
				else
				{
					ActiveSliceSettings.AcquireNewProfile(ActivePrinter.Make, ActivePrinter.Model, ActivePrinter.Name);
					return true;
				}
			}
			else
			{
				this.printerNameError.TextColor = RGBA_Bytes.Red;
				this.printerNameError.Text = "Printer name cannot be blank";
				this.printerNameError.Visible = true;

				return false;
			}
		}
	}
}