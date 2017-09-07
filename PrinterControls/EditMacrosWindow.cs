/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public enum MacroUiLocation { Controls, Extruder_1, Extruder_2, Extruder_3, Extruder_4 }

	public class EditMacrosWindow : SystemWindow
	{
		public GCodeMacro ActiveMacro;
		private static EditMacrosWindow editMacrosWindow = null;
		PrinterSettings printerSettings;

		public EditMacrosWindow(PrinterSettings printerSettings)
			: base(560, 420)
		{
			this.printerSettings = printerSettings;
			AlwaysOnTopOfMain = true;
			Title = "Macro Editor".Localize();
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			ChangeToMacroList();
			ShowAsSystemWindow();
			MinimumSize = new Vector2(360, 420);
		}

		public static void Show()
		{
			if (editMacrosWindow == null)
			{
				editMacrosWindow = new EditMacrosWindow(ActiveSliceSettings.Instance);
				editMacrosWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editMacrosWindow = null; };
			}
			else
			{
				editMacrosWindow.BringToFront();
			}
		}

		public void ChangeToMacroDetail(GCodeMacro macro)
		{
			this.ActiveMacro = macro;
			UiThread.RunOnIdle(() =>
			{
				this.RemoveAllChildren();
				this.AddChild(new MacroDetailWidget(printerSettings, this));
				this.Invalidate();
			});
		}

		public void ChangeToMacroList()
		{
			this.ActiveMacro = null;
			UiThread.RunOnIdle(DoChangeToMacroList);
		}

		public void RefreshMacros()
		{
			printerSettings.Save();
			ApplicationController.Instance.ReloadAll();
		}

		private void DoChangeToMacroList()
		{
			GuiWidget macroListWidget = new MacroListWidget(printerSettings, this);
			this.RemoveAllChildren();
			this.AddChild(macroListWidget);
			this.Invalidate();
		}
	}

	public class MacroDetailWidget : GuiWidget
	{
		DropDownList macroUiLocation;
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private TextWidget macroCommandError;
		private MHTextEditWidget macroCommandInput;
		private TextWidget macroNameError;
		private MHTextEditWidget macroNameInput;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private EditMacrosWindow windowController;
		PrinterSettings printerSettings;

		public MacroDetailWidget(PrinterSettings printerSettings, EditMacrosWindow windowController)
		{
			this.printerSettings = printerSettings;
			this.windowController = windowController;

			linkButtonFactory.fontSize = 10;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string editMacroLabel = "Edit Macro".Localize();
				string editMacroLabelFull = string.Format("{0}:", editMacroLabel);
				TextWidget elementHeader = new TextWidget(editMacroLabelFull, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.Stretch;
				elementHeader.VAnchor = Agg.UI.VAnchor.Bottom;
				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				presetsFormContainer.HAnchor = HAnchor.Stretch;
				presetsFormContainer.VAnchor = VAnchor.Stretch;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			topToBottom.AddChild(presetsFormContainer);

			presetsFormContainer.AddChild(CreateMacroNameContainer());
			presetsFormContainer.AddChild(CreateMacroCommandContainer());
			presetsFormContainer.AddChild(CreateMacroActionEdit());

			Button addMacroButton = textImageButtonFactory.Generate("Save".Localize());
			addMacroButton.Click += SaveMacro_Click;

			Button cancelPresetsButton = textImageButtonFactory.Generate("Cancel".Localize());
			cancelPresetsButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					windowController.ChangeToMacroList();
				});
			};

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.Stretch;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.Stretch;

			buttonRow.AddChild(addMacroButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);
			AddChild(topToBottom);
			this.AnchorAll();
		}

		private FlowLayoutWidget CreateMacroCommandContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string macroCommandLabelTxt = "Macro Commands".Localize();
			string macroCommandLabelTxtFull = string.Format("{0}:", macroCommandLabelTxt);
			TextWidget macroCommandLabel = new TextWidget(macroCommandLabelTxtFull, 0, 0, 12);
			macroCommandLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroCommandLabel.HAnchor = HAnchor.Stretch;
			macroCommandLabel.Margin = new BorderDouble(0, 0, 0, 1);

			macroCommandInput = new MHTextEditWidget(windowController.ActiveMacro.GCode, pixelHeight: 120, multiLine: true, typeFace: ApplicationController.MonoSpacedTypeFace);
			macroCommandInput.DrawFromHintedCache();
			macroCommandInput.HAnchor = HAnchor.Stretch;
			macroCommandInput.VAnchor = VAnchor.Stretch;
			macroCommandInput.ActualTextEditWidget.VAnchor = VAnchor.Stretch;

			string shouldBeGCodeLabel = "This should be in 'G-Code'".Localize();
			string shouldBeGCodeLabelFull = string.Format("{0}.", shouldBeGCodeLabel);
			macroCommandError = new TextWidget(shouldBeGCodeLabelFull, 0, 0, 10);
			macroCommandError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroCommandError.HAnchor = HAnchor.Stretch;
			macroCommandError.Margin = elementMargin;

			container.AddChild(macroCommandLabel);
			container.AddChild(macroCommandInput);
			container.AddChild(macroCommandError);
			container.HAnchor = HAnchor.Stretch;
			container.VAnchor = VAnchor.Stretch;
			return container;
		}

		private FlowLayoutWidget CreateMacroNameContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string macroNameLabelTxtFull = string.Format("{0}:", "Macro Name".Localize());
			TextWidget macroNameLabel = new TextWidget(macroNameLabelTxtFull, 0, 0, 12);
			macroNameLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroNameLabel.HAnchor = HAnchor.Stretch;
			macroNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

			macroNameInput = new MHTextEditWidget(GCodeMacro.FixMacroName(windowController.ActiveMacro.Name));
			macroNameInput.HAnchor = HAnchor.Stretch;

			string giveMacroANameLabel = "Give the macro a name".Localize();
			string giveMacroANameLabelFull = string.Format("{0}.", giveMacroANameLabel);
			macroNameError = new TextWidget(giveMacroANameLabelFull, 0, 0, 10);
			macroNameError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroNameError.HAnchor = HAnchor.Stretch;
			macroNameError.Margin = elementMargin;

			container.AddChild(macroNameLabel);
			container.AddChild(macroNameInput);
			container.AddChild(macroNameError);
			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		FlowLayoutWidget CreateMacroActionEdit()
		{
			FlowLayoutWidget container = new FlowLayoutWidget();
			container.Margin = new BorderDouble(0, 5);

			container.AddChild(new TextWidget("Where to show this macro:")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.Center
			});

			macroUiLocation = new DropDownList("Default", Direction.Up)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0),
				VAnchor = VAnchor.Center
			};
			foreach (var location in Enum.GetValues(typeof(MacroUiLocation)))
			{
				macroUiLocation.AddItem(location.ToString().Replace("_", " ").Localize(), location.ToString());
			}

			macroUiLocation.SelectedValue = windowController.ActiveMacro.MacroUiLocation.ToString();

			container.AddChild(macroUiLocation);
			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		private void SaveActiveMacro()
		{
			windowController.ActiveMacro.Name = macroNameInput.Text;
			windowController.ActiveMacro.GCode = macroCommandInput.Text;
			MacroUiLocation result;
			if (!Enum.TryParse(macroUiLocation.SelectedValue, out result))
			{
				result = MacroUiLocation.Controls;
			}
			windowController.ActiveMacro.MacroUiLocation = result;

			if (!printerSettings.Macros.Contains(windowController.ActiveMacro))
			{
				printerSettings.Macros.Add(windowController.ActiveMacro);
			}
		}

		private void SaveMacro_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				if (ValidateMacroForm())
				{
					SaveActiveMacro();

					windowController.RefreshMacros();
					windowController.ChangeToMacroList();
				}
			});
		}

		private bool ValidateMacroForm()
		{
			ValidationMethods validationMethods = new ValidationMethods();

			List<FormField> formFields = new List<FormField> { };
			FormField.ValidationHandler[] stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
			FormField.ValidationHandler[] nameValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringHasNoSpecialChars };

			formFields.Add(new FormField(macroNameInput, macroNameError, stringValidationHandlers));
			formFields.Add(new FormField(macroCommandInput, macroCommandError, stringValidationHandlers));

			bool formIsValid = true;
			foreach (FormField formField in formFields)
			{
				formField.FieldErrorMessageWidget.Visible = false;
				bool fieldIsValid = formField.Validate();
				if (!fieldIsValid)
				{
					formIsValid = false;
				}
			}
			return formIsValid;
		}
	}

	public class MacroListWidget : GuiWidget
	{
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private EditMacrosWindow windowController;
		PrinterSettings printerSettings;

		public MacroListWidget(PrinterSettings printerSettings, EditMacrosWindow windowController)
		{
			this.windowController = windowController;

			linkButtonFactory.fontSize = 10;
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string macroPresetsLabel = "Macro Presets".Localize();
				string macroPresetsLabelFull = string.Format("{0}:", macroPresetsLabel);
				TextWidget elementHeader = new TextWidget(macroPresetsLabelFull, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.Stretch;
				elementHeader.VAnchor = Agg.UI.VAnchor.Bottom;
				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				presetsFormContainer.HAnchor = HAnchor.Stretch;
				presetsFormContainer.VAnchor = VAnchor.Stretch;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			topToBottom.AddChild(presetsFormContainer);

			if (printerSettings?.Macros != null)
			{
				foreach (GCodeMacro macro in printerSettings.Macros)
				{
					FlowLayoutWidget macroRow = new FlowLayoutWidget();
					macroRow.Margin = new BorderDouble(3, 0, 3, 3);
					macroRow.HAnchor = Agg.UI.HAnchor.Stretch;
					macroRow.Padding = new BorderDouble(3);
					macroRow.BackgroundColor = RGBA_Bytes.White;

					TextWidget buttonLabel = new TextWidget(GCodeMacro.FixMacroName(macro.Name));
					macroRow.AddChild(buttonLabel);

					macroRow.AddChild(new HorizontalSpacer());

					// You can't use the foreach variable inside the lambda functions directly or it will always be the last item.
					// We make a local variable to create a closure around it to ensure we get the correct instance
					var localMacroReference = macro;

					Button editLink = linkButtonFactory.Generate("edit".Localize());
					editLink.Margin = new BorderDouble(right: 5);
					editLink.Click += (sender, e) =>
					{
						windowController.ChangeToMacroDetail(localMacroReference);
					};
					macroRow.AddChild(editLink);

					Button removeLink = linkButtonFactory.Generate("remove".Localize());
					removeLink.Click += (sender, e) =>
					{
						printerSettings.Macros.Remove(localMacroReference);

						windowController.RefreshMacros();
						windowController.ChangeToMacroList();
					};
					macroRow.AddChild(removeLink);

					presetsFormContainer.AddChild(macroRow);
				}
			}

			Button addMacroButton = textImageButtonFactory.Generate("Add".Localize(), "icon_circle_plus.png");
			addMacroButton.ToolTipText = "Add a new Macro".Localize();
			addMacroButton.Click += (s, e) =>
			{
				windowController.ChangeToMacroDetail(new GCodeMacro()
				{
					Name = "Home All",
					GCode = "G28 ; Home All Axes"
				});
			};

			Button cancelPresetsButton = textImageButtonFactory.Generate("Close".Localize());
			cancelPresetsButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() => this.windowController.Close());
			};

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.Stretch;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.Stretch;

			buttonRow.AddChild(addMacroButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);
			AddChild(topToBottom);
			this.AnchorAll();
		}
	}
}