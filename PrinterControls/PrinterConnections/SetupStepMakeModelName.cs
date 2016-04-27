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

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	//Normally step one of the setup process
	public class SetupStepMakeModelName : SetupConnectionWidgetBase
	{
		private FlowLayoutWidget printerModelContainer;
		private FlowLayoutWidget printerMakeContainer;
		private FlowLayoutWidget printerNameContainer;

		private MHTextEditWidget printerNameInput;

		private TextWidget printerNameError;
		private TextWidget printerModelError;
		private TextWidget printerMakeError;

		private PrinterChooser printerManufacturerSelector;

		private Button nextButton;

		private bool usingDefaultName;

		public SetupStepMakeModelName(ConnectionWizard windowController) : base(windowController)
		{
			//Construct inputs
			printerNameContainer = createPrinterNameContainer();
			printerMakeContainer = createPrinterMakeContainer();

			if (printerManufacturerSelector.CountOfMakes == 1)
			{
				ActivePrinter.Make = printerManufacturerSelector.ManufacturerDropList.SelectedValue;

				printerMakeContainer.Visible = false;
				printerModelContainer = createPrinterModelContainer(printerManufacturerSelector.ManufacturerDropList.SelectedValue);
				printerModelContainer.Visible = true;
			}
			else
			{
				printerModelContainer = createPrinterModelContainer();
			}

			//Add inputs to main container
			contentRow.AddChild(printerNameContainer);
			contentRow.AddChild(printerMakeContainer);
			contentRow.AddChild(printerModelContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Save & Continue"));
			nextButton.Name = "Save & Continue Button";
			nextButton.Click += (s, e) =>
			{
				bool canContinue = this.OnSave();
				if (canContinue)
				{
					UiThread.RunOnIdle(connectionWizard.Close);
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

			printerNameInput = new MHTextEditWidget(this.ActivePrinter.Name)
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			printerNameInput.KeyPressed += (s, e) => this.usingDefaultName = false;

			printerNameError = new TextWidget("Give your printer a name.".Localize(), 0, 0, 10)
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

		private FlowLayoutWidget createPrinterMakeContainer()
		{
			BorderDouble elementMargin = new BorderDouble(top: 3);

			TextWidget printerManufacturerLabel = new TextWidget("Make".Localize() + ":", 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			printerManufacturerSelector = new PrinterChooser()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};
			printerManufacturerSelector.ManufacturerDropList.SelectionChanged += ManufacturerDropList_SelectionChanged;

			printerMakeError = new TextWidget("Select the printer manufacturer".Localize(), 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			container.AddChild(printerManufacturerLabel);
			container.AddChild(printerManufacturerSelector);
			container.AddChild(printerMakeError);
			container.HAnchor = HAnchor.ParentLeftRight;

			return container;
		}

		private FlowLayoutWidget createPrinterModelContainer(string make = "Other")
		{
			BorderDouble elementMargin = new BorderDouble(top: 3);

			TextWidget printerModelLabel = new TextWidget("Model".Localize() + ":", 0, 0, 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			ModelChooser printerModelSelector = new ModelChooser(make)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};
			printerModelSelector.ModelDropList.SelectionChanged += new EventHandler(ModelDropList_SelectionChanged);
			printerModelSelector.SelectIfOnlyOneModel();

			printerModelError = new TextWidget("Select the printer model".Localize(), 0, 0, 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = elementMargin
			};

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			container.AddChild(printerModelLabel);
			container.AddChild(printerModelSelector);
			container.AddChild(printerModelError);
			container.HAnchor = HAnchor.ParentLeftRight;

			return container;
		}

		private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
		{
			ActivePrinter.Make = ((DropDownList)sender).SelectedValue;
			ActivePrinter.Model = null;
			ReconstructModelSelector();
			SetElementVisibility();
		}

		private void ReconstructModelSelector()
		{
			//reconstruct model selector
			int currentIndex = contentRow.GetChildIndex(printerModelContainer);
			contentRow.RemoveChild(printerModelContainer);

			printerModelContainer = createPrinterModelContainer(ActivePrinter.Make);
			contentRow.AddChild(printerModelContainer, currentIndex);
			contentRow.Invalidate();

			printerMakeError.Visible = false;
		}

		private void ModelDropList_SelectionChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				ActivePrinter.Model = ((DropDownList)sender).SelectedLabel;

				printerModelError.Visible = false;
				SetElementVisibility();
				if (usingDefaultName)
				{
					// Use ManufacturerDropList.SelectedLabel instead of ActivePrinter.Make to ensure the mapped Unicode values are picked up
					string mappedMakeText = printerManufacturerSelector.ManufacturerDropList.SelectedLabel;

					string printerInputName = String.Format("{0} {1}", mappedMakeText, this.ActivePrinter.Model);
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
							possiblePrinterName = String.Format("{0} ({1})", printerInputName, printerModelCount++);
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
					// TODO: Plumb in saving the profile to disk, then setting the instance to be the active profile
					System.Diagnostics.Debugger.Launch();

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