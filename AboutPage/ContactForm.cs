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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.MatterControl.VersionManagement;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ContactForm
{
	public class ContactFormWidget : DialogPage
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

		public ContactFormWidget()
		{
			var theme = ApplicationController.Instance.Theme;

			AnchorAll();

			this.WindowTitle = "MatterControl : " + "Submit Feedback".Localize();
			this.HeaderText = "How can we improve?".Localize();

			contentRow.Padding = theme.DefaultContainerPadding;

			submitButton = theme.CreateDialogButton("Submit".Localize());
			this.AddPageAction(submitButton);

			submitButton.Click += SubmitContactForm;

			DoLayout();
		}

		private GuiWidget LabelGenerator(string labelText, int fontSize = 12, int height = 28)
		{
			GuiWidget labelContainer = new GuiWidget();
			labelContainer.HAnchor = HAnchor.Stretch;
			labelContainer.Height = height * GuiWidget.DeviceScale;

			TextWidget formLabel = new TextWidget(labelText, pointSize: fontSize);
			formLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			formLabel.VAnchor = VAnchor.Bottom;
			formLabel.HAnchor = HAnchor.Left;
			formLabel.Margin = new BorderDouble(bottom: 2);

			labelContainer.AddChild(formLabel);

			return labelContainer;
		}

		private TextWidget ErrorMessageGenerator()
		{
			TextWidget formLabel = new TextWidget("", pointSize: 11);
			formLabel.AutoExpandBoundsToText = true;
			formLabel.Margin = new BorderDouble(0, 5);
			formLabel.TextColor = Color.Red;
			formLabel.HAnchor = HAnchor.Left;
			formLabel.Visible = false;

			return formLabel;
		}

		private void DoLayout()
		{
			messageContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			submissionStatus = new TextWidget("Submitting your information...".Localize(), pointSize: 13);
			submissionStatus.AutoExpandBoundsToText = true;
			submissionStatus.Margin = new BorderDouble(0, 5);
			submissionStatus.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			submissionStatus.HAnchor = HAnchor.Left;

			messageContainer.AddChild(submissionStatus);

			// Default sizing results in too much top whitespace, revise Subject row to only be as big as content
			var subjectRow = LabelGenerator("Subject*".Localize());
			subjectRow.VAnchor = VAnchor.Fit;
			contentRow.AddChild(subjectRow);

			questionInput = new MHTextEditWidget("");
			questionInput.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(questionInput);

			questionErrorMessage = ErrorMessageGenerator();
			contentRow.AddChild(questionErrorMessage);

			contentRow.AddChild(LabelGenerator("Message*".Localize()));

			detailInput = new MHTextEditWidget("", pixelHeight: 120, multiLine: true);
			detailInput.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(detailInput);

			detailErrorMessage = ErrorMessageGenerator();
			contentRow.AddChild(detailErrorMessage);

			contentRow.AddChild(LabelGenerator("Email Address*".Localize()));

			emailInput = new MHTextEditWidget();
			emailInput.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(emailInput);

			emailErrorMessage = ErrorMessageGenerator();
			contentRow.AddChild(emailErrorMessage);

			contentRow.AddChild(LabelGenerator("Name*".Localize()));

			nameInput = new MHTextEditWidget();
			nameInput.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(nameInput);

			nameErrorMessage = ErrorMessageGenerator();
			contentRow.AddChild(nameErrorMessage);
		}

		private bool ValidateContactForm()
		{
			ValidationMethods validationMethods = new ValidationMethods();

			List<FormField> formFields = new List<FormField> { };
			FormField.ValidationHandler[] stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
			FormField.ValidationHandler[] emailValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringLooksLikeEmail };

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

		private void SubmitContactForm(object sender, EventArgs mouseEvent)
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
		}
	}
}
