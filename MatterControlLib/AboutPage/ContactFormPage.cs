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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.MatterControl.VersionManagement;

namespace MatterHackers.MatterControl.ContactForm
{
	public class ContactFormPage : DialogPage
	{
		private TextButton submitButton;

		private FlowLayoutWidget messageContainer;

		private TextWidget submissionStatus;

		internal MHTextEditWidget questionInput;
		private TextWidget questionErrorMessage;

		internal MHTextEditWidget detailInput;
		private TextWidget detailErrorMessage;

		private MHTextEditWidget emailInput;
		private TextWidget emailErrorMessage;

		private MHTextEditWidget nameInput;
		private TextWidget nameErrorMessage;

		public ContactFormPage()
		{
			this.WindowTitle = "MatterControl : " + "Submit Feedback".Localize();
			this.HeaderText = "How can we improve?".Localize();

			contentRow.Padding = theme.DefaultContainerPadding;

			submitButton = theme.CreateDialogButton("Submit".Localize());
			submitButton.Click += (sender, eventArgs) =>
			{
				if (ValidateContactForm())
				{
					ContactFormRequest postRequest = new ContactFormRequest(questionInput.Text, detailInput.Text, emailInput.Text, nameInput.Text, "");

					contentRow.RemoveAllChildren();

					contentRow.AddChild(messageContainer);

					submitButton.Visible = false;

					postRequest.RequestSucceeded += (s, e) =>
					{
						submissionStatus.Text = "Thank you!  Your information has been submitted.".Localize();
						this.SetCancelButtonText("Done".Localize());
					};
					postRequest.RequestFailed += (s, e) =>
					{
						submissionStatus.Text = "Sorry!  We weren't able to submit your request.".Localize();
					};
					postRequest.Request();
				}
			};
			this.AddPageAction(submitButton);

			messageContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			submissionStatus = new TextWidget("Submitting your information...".Localize(), pointSize: 13)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(0, 5),
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Left
			};

			messageContainer.AddChild(submissionStatus);

			// Default sizing results in too much top whitespace, revise Subject row to only be as big as content
			var subjectRow = CreateLabelRow("Subject".Localize());
			subjectRow.VAnchor = VAnchor.Fit;
			contentRow.AddChild(subjectRow);
			contentRow.AddChild(questionInput = new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch
			});
			contentRow.AddChild(questionErrorMessage = CreateErrorRow());

			contentRow.AddChild(CreateLabelRow("Message".Localize()));
			contentRow.AddChild(detailInput = new MHTextEditWidget("", theme, pixelHeight: 120, multiLine: true)
			{
				HAnchor = HAnchor.Stretch
			});
			contentRow.AddChild(detailErrorMessage = CreateErrorRow());

			contentRow.AddChild(CreateLabelRow("Email Address".Localize()));
			contentRow.AddChild(emailInput = new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch
			});
			contentRow.AddChild(emailErrorMessage = CreateErrorRow());

			contentRow.AddChild(CreateLabelRow("Name".Localize()));
			contentRow.AddChild(nameInput = new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch
			});
			contentRow.AddChild(nameErrorMessage = CreateErrorRow());
		}

		private GuiWidget CreateLabelRow(string labelText, int fontSize = 12, int height = 28)
		{
			var labelContainer = new GuiWidget
			{
				HAnchor = HAnchor.Stretch,
				Height = height * GuiWidget.DeviceScale
			};

			labelContainer.AddChild(new TextWidget(labelText, pointSize: fontSize)
			{
				TextColor = theme.TextColor,
				VAnchor = VAnchor.Bottom,
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(bottom: 2)
			});

			return labelContainer;
		}

		private TextWidget CreateErrorRow()
		{
			return new TextWidget("", pointSize: 11)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(0, 5),
				TextColor = Color.Red,
				HAnchor = HAnchor.Left,
				Visible = false
			};
		}

		private bool ValidateContactForm()
		{
			var validationMethods = new ValidationMethods();

			var formFields = new List<FormField> { };
			var stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
			var emailValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringLooksLikeEmail };

			formFields.Add(new FormField(questionInput, questionErrorMessage, stringValidationHandlers));
			formFields.Add(new FormField(detailInput, detailErrorMessage, stringValidationHandlers));
			formFields.Add(new FormField(emailInput, emailErrorMessage, emailValidationHandlers));
			formFields.Add(new FormField(nameInput, nameErrorMessage, stringValidationHandlers));

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
}
