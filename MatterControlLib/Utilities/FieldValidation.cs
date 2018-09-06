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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.FieldValidation
{
	public class ValidationStatus
	{
		public bool IsValid { get; set; }

		public string ErrorMessage { get; set; }

		public ValidationStatus()
		{
			this.IsValid = true;
			this.ErrorMessage = "";
		}
	}

	public class ValidationMethods
	{
		public ValidationMethods()
		{
		}

		public ValidationStatus StringIsNotEmpty(string value)
		{
			ValidationStatus status = new ValidationStatus();
			if (value.Trim() == "")
			{
				status.IsValid = false;
				status.ErrorMessage = "Oops! Field cannot be left blank".Localize();
			}
			return status;
		}

		public ValidationStatus StringHasNoSpecialChars(string value)
		{
			ValidationStatus status = new ValidationStatus();
			var regexItem = new Regex("^[a-zA-Z0-9 ]*$");
			if (!regexItem.IsMatch(value))
			{
				status.IsValid = false;
				status.ErrorMessage = "Oops! Field cannot have special characters".Localize();
			}
			return status;
		}

		private static Regex digitsOnly = new Regex(@"[^\d]");

		public ValidationStatus StringLooksLikePhoneNumber(string value)
		{
			ValidationStatus status = new ValidationStatus();

			value = digitsOnly.Replace(value, "");
			if (value.Length == 10)
			{
				status.IsValid = true;
			}
			else if (value.Length == 11 && value[0] == '1')
			{
				status.IsValid = true;
			}
			else
			{
				status.IsValid = false;
				status.ErrorMessage = "Sorry!  Must be a valid U.S. or Canadian phone number.".Localize();
			}

			return status;
		}

		public ValidationStatus StringLooksLikeEmail(string value)
		{
			ValidationStatus status = new ValidationStatus();
			int lastAtPos = value.IndexOf("@");
			int lastDotPos = value.LastIndexOf(".");

			if (lastAtPos < lastDotPos && lastAtPos > 0 && value.IndexOf("@@") == -1 && lastDotPos > 2 && (value.Length - lastDotPos) > 2)
			{
				status.IsValid = true;
			}
			else
			{
				status.IsValid = false;
				status.ErrorMessage = "Sorry!  Must be a valid email address.".Localize();
			}
			return status;
		}
	}

	public class FormField
	{
		public delegate ValidationStatus ValidationHandler(string valueToValidate);

		public MHTextEditWidget FieldEditWidget { get; set; }

		public TextWidget FieldErrorMessageWidget { get; set; }

		private ValidationHandler[] FieldValidationHandlers { get; set; }

		public FormField(MHTextEditWidget textEditWidget, TextWidget errorMessageWidget, ValidationHandler[] validationHandlers)
		{
			this.FieldEditWidget = textEditWidget;
			this.FieldErrorMessageWidget = errorMessageWidget;
			this.FieldValidationHandlers = validationHandlers;
		}

		public bool Validate()
		{
			bool fieldIsValid = true;
			foreach (ValidationHandler validationHandler in FieldValidationHandlers)
			{
				if (fieldIsValid)
				{
					ValidationStatus validationStatus = validationHandler(this.FieldEditWidget.Text);
					if (!validationStatus.IsValid)
					{
						fieldIsValid = false;
						FieldErrorMessageWidget.Text = validationStatus.ErrorMessage;
						FieldErrorMessageWidget.Visible = true;
					}
				}
			}
			return fieldIsValid;
		}
	}
}