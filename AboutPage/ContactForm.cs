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
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected TextImageButtonFactory whiteButtonFactory = new TextImageButtonFactory();
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
			SetButtonAttributes();
			AnchorAll();

			cancelButton = textImageButtonFactory.Generate("Cancel".Localize());
			submitButton = textImageButtonFactory.Generate(LocalizedString.Get("Submit"));
			doneButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"));
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

			TextWidget formLabel = new TextWidget(LocalizedString.Get("How can we improve?"), pointSize: 16);
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

			submissionStatus = new TextWidget(LocalizedString.Get("Submitting your information..."), pointSize: 13);
			submissionStatus.AutoExpandBoundsToText = true;
			submissionStatus.Margin = new BorderDouble(0, 5);
			submissionStatus.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			submissionStatus.HAnchor = HAnchor.ParentLeft;

			messageContainer.AddChild(submissionStatus);

			formContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			formContainer.AnchorAll();
			formContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			formContainer.Padding = new BorderDouble(10);

			formContainer.AddChild(LabelGenerator(LocalizedString.Get("Subject*")));

			questionInput = new MHTextEditWidget(subjectText);
			questionInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(questionInput);

			questionErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(questionErrorMessage);

			formContainer.AddChild(LabelGenerator(LocalizedString.Get("Message*")));

			detailInput = new MHTextEditWidget(bodyText, pixelHeight: 120, multiLine: true);
			detailInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(detailInput);

			detailErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(detailErrorMessage);

			formContainer.AddChild(LabelGenerator(LocalizedString.Get("Email Address*")));

			emailInput = new MHTextEditWidget();
			emailInput.HAnchor = HAnchor.ParentLeftRight;
			formContainer.AddChild(emailInput);

			emailErrorMessage = ErrorMessageGenerator();
			formContainer.AddChild(emailErrorMessage);

			formContainer.AddChild(LabelGenerator(LocalizedString.Get("Name*")));

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
			submitButton.Click += new EventHandler(SubmitContactForm);
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
			submissionStatus.Text = LocalizedString.Get("Thank you!  Your information has been submitted.");
			doneButton.Visible = true;
		}

		private void onPostRequestFailed(object sender, ResponseErrorEventArgs e)
		{
			submissionStatus.Text = LocalizedString.Get("Sorry!  We weren't able to submit your request.");
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

		private void SetButtonAttributes()
		{
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			whiteButtonFactory.FixedWidth = 138 * GuiWidget.DeviceScale;
			whiteButtonFactory.normalFillColor = RGBA_Bytes.White;
			whiteButtonFactory.normalTextColor = RGBA_Bytes.Black;
			whiteButtonFactory.hoverTextColor = RGBA_Bytes.Black;
			whiteButtonFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 200);
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
			Title = LocalizedString.Get("MatterControl: Submit Feedback");

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			contactFormWidget = new ContactFormWidget(subject, bodyText);

#if __ANDROID__
			this.AddChild(new SoftKeyboardContentOffset(contactFormWidget));
#else
			AddChild(contactFormWidget);
#endif
			AddHandlers();

			ShowAsSystemWindow();
			MinimumSize = new Vector2(500, 550);
		}

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
			contactFormWidget.Closed += (sender, e) => { Close(); };
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.Invalidate();
		}
	}
}