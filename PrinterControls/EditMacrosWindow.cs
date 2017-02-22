﻿/*
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
	public class EditMacrosWindow : SystemWindow
	{
		public GCodeMacro ActiveMacro;
		private static EditMacrosWindow editMacrosWindow = null;

		public EditMacrosWindow()
			: base(560, 420)
		{
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
				editMacrosWindow = new EditMacrosWindow();
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
				this.AddChild(new MacroDetailWidget(this));
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
			ActiveSliceSettings.Instance.Save();
			ApplicationController.Instance.ReloadAll();
		}

		private void DoChangeToMacroList()
		{
			GuiWidget macroListWidget = new MacroListWidget(this);
			this.RemoveAllChildren();
			this.AddChild(macroListWidget);
			this.Invalidate();
		}
	}

	public class MacroDetailWidget : GuiWidget
	{
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private TextWidget macroCommandError;
		private MHTextEditWidget macroCommandInput;
		private TextWidget macroNameError;
		private MHTextEditWidget macroNameInput;
		private CheckBox showInActionMenu;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private EditMacrosWindow windowController;

		public MacroDetailWidget(EditMacrosWindow windowController)
		{
			this.windowController = windowController;

			linkButtonFactory.fontSize = 10;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string editMacroLabel = "Edit Macro".Localize();
				string editMacroLabelFull = string.Format("{0}:", editMacroLabel);
				TextWidget elementHeader = new TextWidget(editMacroLabelFull, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;
				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
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
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

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
			macroCommandLabel.HAnchor = HAnchor.ParentLeftRight;
			macroCommandLabel.Margin = new BorderDouble(0, 0, 0, 1);

			macroCommandInput = new MHTextEditWidget(windowController.ActiveMacro.GCode, pixelHeight: 120, multiLine: true, typeFace: ApplicationController.MonoSpacedTypeFace);
			macroCommandInput.DrawFromHintedCache();
			macroCommandInput.HAnchor = HAnchor.ParentLeftRight;
			macroCommandInput.VAnchor = VAnchor.ParentBottomTop;
			macroCommandInput.ActualTextEditWidget.VAnchor = VAnchor.ParentBottomTop;

			string shouldBeGCodeLabel = "This should be in 'G-Code'".Localize();
			string shouldBeGCodeLabelFull = string.Format("{0}.", shouldBeGCodeLabel);
			macroCommandError = new TextWidget(shouldBeGCodeLabelFull, 0, 0, 10);
			macroCommandError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroCommandError.HAnchor = HAnchor.ParentLeftRight;
			macroCommandError.Margin = elementMargin;

			container.AddChild(macroCommandLabel);
			container.AddChild(macroCommandInput);
			container.AddChild(macroCommandError);
			container.HAnchor = HAnchor.ParentLeftRight;
			container.VAnchor = VAnchor.ParentBottomTop;
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
			macroNameLabel.HAnchor = HAnchor.ParentLeftRight;
			macroNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

			macroNameInput = new MHTextEditWidget(GCodeMacro.FixMacroName(windowController.ActiveMacro.Name));
			macroNameInput.HAnchor = HAnchor.ParentLeftRight;

			string giveMacroANameLabel = "Give the macro a name".Localize();
			string giveMacroANameLabelFull = string.Format("{0}.", giveMacroANameLabel);
			macroNameError = new TextWidget(giveMacroANameLabelFull, 0, 0, 10);
			macroNameError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroNameError.HAnchor = HAnchor.ParentLeftRight;
			macroNameError.Margin = elementMargin;

			container.AddChild(macroNameLabel);
			container.AddChild(macroNameInput);
			container.AddChild(macroNameError);
			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		FlowLayoutWidget CreateMacroActionEdit()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			showInActionMenu = new CheckBox("Show In Action Menu".Localize())
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 0, 0, 1),
				Checked = windowController.ActiveMacro.ActionGroup,
			};

			container.AddChild(showInActionMenu);
			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void SaveActiveMacro()
		{
			windowController.ActiveMacro.Name = macroNameInput.Text;
			windowController.ActiveMacro.GCode = macroCommandInput.Text;
			windowController.ActiveMacro.ActionGroup = showInActionMenu.Checked;

			if (!ActiveSliceSettings.Instance.Macros.Contains(windowController.ActiveMacro))
			{
				ActiveSliceSettings.Instance.Macros.Add(windowController.ActiveMacro);
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

		public MacroListWidget(EditMacrosWindow windowController)
		{
			this.windowController = windowController;

			linkButtonFactory.fontSize = 10;
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string macroPresetsLabel = "Macro Presets".Localize();
				string macroPresetsLabelFull = string.Format("{0}:", macroPresetsLabel);
				TextWidget elementHeader = new TextWidget(macroPresetsLabelFull, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;
				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			topToBottom.AddChild(presetsFormContainer);

			if (ActiveSliceSettings.Instance?.Macros != null)
			{
				foreach (GCodeMacro macro in ActiveSliceSettings.Instance.Macros)
				{
					FlowLayoutWidget macroRow = new FlowLayoutWidget();
					macroRow.Margin = new BorderDouble(3, 0, 3, 3);
					macroRow.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
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
						ActiveSliceSettings.Instance.Macros.Remove(localMacroReference);

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
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

			buttonRow.AddChild(addMacroButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);
			AddChild(topToBottom);
			this.AnchorAll();
		}
	}
}