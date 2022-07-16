﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class MacroDetailPage : DialogPage
	{
		private List<FormField> formFields;
		private PrinterSettings printerSettings;

		public MacroDetailPage(GCodeMacro gcodeMacro, PrinterSettings printerSettings)
		{
			// Form validation fields
			ThemedTextEditWidget macroNameInput;
			ThemedTextEditWidget macroCommandInput;
			WrappedTextWidget macroCommandError;
			WrappedTextWidget macroNameError;

			this.HeaderText = "Edit Macro".Localize();
			this.printerSettings = printerSettings;

			var elementMargin = new BorderDouble(top: 3);

			contentRow.Padding += 3;

			contentRow.AddChild(new TextWidget("Macro Name".Localize() + ":", 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 1)
			});

			contentRow.AddChild(macroNameInput = new ThemedTextEditWidget(GCodeMacro.FixMacroName(gcodeMacro.Name), theme)
			{
				HAnchor = HAnchor.Stretch
			});

			contentRow.AddChild(macroNameError = new WrappedTextWidget("Give the macro a name".Localize() + ".", 10)
			{
				TextColor = theme.TextColor,
				Margin = elementMargin
			});

			contentRow.AddChild(new TextWidget("Macro Commands".Localize() + ":", 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 0, 0, 1)
			});

			macroCommandInput = new ThemedTextEditWidget(gcodeMacro.GCode, theme, pixelHeight: 120, multiLine: true, typeFace: ApplicationController.GetTypeFace(NamedTypeFace.Liberation_Mono))
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			macroCommandInput.ActualTextEditWidget.VAnchor = VAnchor.Stretch;
			macroCommandInput.DrawFromHintedCache();
			contentRow.AddChild(macroCommandInput);

			contentRow.AddChild(macroCommandError = new WrappedTextWidget("This should be in 'G-Code'".Localize() + ".", 10)
			{
				TextColor = theme.TextColor,
				Margin = elementMargin
			});

			var container = new FlowLayoutWidget
			{
				Margin = new BorderDouble(0, 5),
				HAnchor = HAnchor.Stretch
			};

			contentRow.AddChild(container);

			var saveMacroButton = theme.CreateDialogButton("Save".Localize());
			saveMacroButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (ValidateMacroForm())
					{
						// SaveActiveMacro
						gcodeMacro.Name = macroNameInput.Text;
						gcodeMacro.GCode = macroCommandInput.Text;

						if (!printerSettings.Macros.Contains(gcodeMacro))
						{
							printerSettings.Macros.Add(gcodeMacro);
						}

						printerSettings.NotifyMacrosChanged();
						printerSettings.Save();

						this.DialogWindow.ChangeToPage(new MacroListPage(printerSettings));
					}
				});
			};

			this.AddPageAction(saveMacroButton);

			// Define field validation
			var validationMethods = new ValidationMethods();
			var stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };

			formFields = new List<FormField>
			{
				new FormField(macroNameInput, macroNameError, stringValidationHandlers),
				new FormField(macroCommandInput, macroCommandError, stringValidationHandlers)
			};
		}

		protected override void OnCancel(out bool abortCancel)
		{
			abortCancel = true;
			this.DialogWindow.ChangeToPage(new MacroListPage(printerSettings));
		}

		private bool ValidateMacroForm()
		{
			bool formIsValid = true;
			foreach (FormField formField in formFields)
			{
				formField.FieldErrorMessageWidget.Visible = false;

				if (!formField.Validate())
				{
					formIsValid = false;
				}
			}

			return formIsValid;
		}
	}
}