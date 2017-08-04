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
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.MatterControl.VersionManagement;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ContactForm
{
	public class ContactFormWidget : GuiWidget
	{
		private Button submitButton;
		private Button cancelButton;
		private Button doneButton;
		private FlowLayoutWidget formContainer;
		private FlowLayoutWidget messageContainer;

		private TextWidget submissionStatus;
		private GuiWidget centerContainer;

		private MHTextEditWidget questionInput;
		private TextWidget questionErrorMessage;

		private MHTextEditWidget detailInput;
		private TextWidget detailErrorMessage;

		private MHTextEditWidget emailInput;
		private TextWidget emailErrorMessage;

		private MHTextEditWidget nameInput;
		private TextWidget nameErrorMessage;

		public ContactFormWidget(string subjectText, string bodyText)
		{
			AnchorAll();

			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;
		
			cancelButton = buttonFactory.Generate("Cancel".Localize());
			submitButton = buttonFactory.Generate("Submit".Localize());
			doneButton = buttonFactory.Generate("Done".Localize());
			doneButton.Visible = false;

			DoLayout(subjectText, bodyText);
			AddButtonHandlers();
		}

		private GuiWidget LabelGenerator(string labelText, int fontSize = 12, int height = 28)
		{
			GuiWidget labelContainer = new GuiWidget();
			labelContainer.HAnchor = HAnchor.ParentLeftRight;
			labelContainer.Height = height * GuiWidget.DeviceScale;

			TextWidget formLabel = new TextWidget(labelText, pointSize: fontSize);
			formLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			formLabel.VAnchor = VAnchor.ParentBottom;
			formLabel.HAnchor = HAnchor.ParentLeft;
			formLabel.Margin = new BorderDouble(bottom: 2);

			labelContainer.AddChild(formLabel);

			return labelContainer;
		}

		private TextWidget ErrorMessageGenerator()
		{
			TextWidget formLabel = new TextWidget("", pointSize: 11);
			formLabel.AutoExpandBoundsToText = true;
			formLabel.Margin = new BorderDouble(0, 5);
			formLabel.TextColor = RGBA_Bytes.Red;
			formLabel.HAnchor = HAnchor.ParentLeft;
			formLabel.Visible = false;

			return formLabel;
		}

		private void DoLayout(string subjectText, string bodyText)
		{
			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();

			GuiWidget labelContainer = new GuiWidget();
			labelContainer.HAnchor = HAnchor.ParentLeftRight;
			labelContainer.Height = 30;

			TextWidget formLabel = new TextWidget("How can we improve?".Localize(), pointSize: 16);
			formLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			formLabel.VAnchor = VAnchor.ParentTop;
			formLabel.HAnchor = HAnchor.ParentLeft;
			formLabel.Margin = new BorderDouble(6, 3, 6, 6);
			labelContainer.AddChild(formLabel);
			mainContainer.AddChild(labelContainer);

			centerContainer = new GuiWidget();
			centerContainer.AnchorAll();
			centerContainer.Padding = new BorderDouble(3, 0, 3, 3);

			messageContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			messageContainer.AnchorAll();
			messageContainer.Visible = false;
			messageContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			messageContainer.Padding = new BorderDouble(10);

			submissionStatus = new TextWidget("Submitting your information...".Localize(), pointSize: 13);
			submissionStatus.AutoExpandBoundsToText = true;
			submissionStatus.Margin = new BorderDouble(0, 5);
			submissionStatus.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			submissionStatus.HAnchor = HAnchor.ParentLeft;

			messageContainer.AddChild(submissionStatus);

			formContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			formContainer.AnchorAll();
			formContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			formContainer.Padding = new BorderDouble(10);

			formContainer.AddChild(LabelGenerator("Subject*".Localize()));

			questionInput = new MHTextEditWidget(subjectText);
			questionInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(questionInput);

			questionErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(questionErrorMessage);

			formContainer.AddChild(LabelGenerator("Message*".Localize()));

			detailInput = new MHTextEditWidget(bodyText, pixelHeight: 120, multiLine: true);
			detailInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(detailInput);

			detailErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(detailErrorMessage);

			formContainer.AddChild(LabelGenerator("Email Address*".Localize()));

			emailInput = new MHTextEditWidget();
			emailInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(emailInput);

			emailErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(emailErrorMessage);

			formContainer.AddChild(LabelGenerator("Name*".Localize()));

			nameInput = new MHTextEditWidget();
			nameInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(nameInput);

			nameErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(nameErrorMessage);

			centerContainer.AddChild(formContainer);

			mainContainer.AddChild(centerContainer);

			FlowLayoutWidget buttonBottomPanel = GetButtonButtonPanel();
			buttonBottomPanel.AddChild(submitButton);
			buttonBottomPanel.AddChild(cancelButton);
			buttonBottomPanel.AddChild(doneButton);

			mainContainer.AddChild(buttonBottomPanel);

			this.AddChild(mainContainer);
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

		private void AddButtonHandlers()
		{
			cancelButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(Close);
			};
			doneButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(Close);
			};
			submitButton.Click += SubmitContactForm;
		}

		private void SubmitContactForm(object sender, EventArgs mouseEvent)
		{
			if (ValidateContactForm())
			{
				ContactFormRequest postRequest = new ContactFormRequest(questionInput.Text, detailInput.Text, emailInput.Text, nameInput.Text, "");

				formContainer.Visible = false;
				messageContainer.Visible = true;

				centerContainer.RemoveAllChildren();
				centerContainer.AddChild(messageContainer);

				cancelButton.Visible = false;
				submitButton.Visible = false;

				postRequest.RequestSucceeded += new EventHandler(onPostRequestSucceeded);
				postRequest.RequestFailed += onPostRequestFailed;
				postRequest.Request();
			}
		}

		private void onPostRequestSucceeded(object sender, EventArgs e)
		{
			submissionStatus.Text = "Thank you!  Your information has been submitted.".Localize();
			doneButton.Visible = true;
		}

		private void onPostRequestFailed(object sender, ResponseErrorEventArgs e)
		{
			submissionStatus.Text = "Sorry!  We weren't able to submit your request.".Localize();
			doneButton.Visible = true;
		}

		private FlowLayoutWidget GetButtonButtonPanel()
		{
			FlowLayoutWidget buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
			buttonBottomPanel.Padding = new BorderDouble(3, 3);
			buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			return buttonBottomPanel;
		}
	}

	public class ContactFormWindow : SystemWindow
	{
		private static ContactFormWindow contactFormWindow;
		private static bool contactFormIsOpen;

		static public void Open(string subject = "", string bodyText = "")
		{
			if (!contactFormIsOpen)
			{
				contactFormWindow = new ContactFormWindow(subject, bodyText);
				contactFormIsOpen = true;
				contactFormWindow.Closed += (sender, e) => { contactFormIsOpen = false; };
			}
			else
			{
				if (contactFormWindow != null)
				{
					contactFormWindow.BringToFront();
				}
			}
		}

		private ContactFormWidget contactFormWidget;

		private ContactFormWindow(string subject = "", string bodyText = "")
			: base(500, 550)
		{
			AlwaysOnTopOfMain = true;
			Title = "MatterControl: Submit Feedback".Localize();

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			contactFormWidget = new ContactFormWidget(subject, bodyText);

			AddChild(contactFormWidget);

			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => this.Invalidate(), ref unregisterEvents);
			contactFormWidget.Closed += (sender, e) => 
			{
				Close();
			};

			ShowAsSystemWindow();
			MinimumSize = new Vector2(500, 550);
		}

		private EventHandler unregisterEvents;

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}