using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.VersionManagement;
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.ContactForm
{
    
    public class ContactFormWidget : GuiWidget
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        protected TextImageButtonFactory whiteButtonFactory = new TextImageButtonFactory();
        Button submitButton;
        Button cancelButton;
        Button doneButton;
        FlowLayoutWidget formContainer;
        FlowLayoutWidget messageContainer;

        TextWidget submissionStatus;
        GuiWidget centerContainer;

        MHTextEditWidget questionInput;
        TextWidget questionErrorMessage;

        MHTextEditWidget detailInput;
        TextWidget detailErrorMessage;

        MHTextEditWidget emailInput;
        TextWidget emailErrorMessage;

        MHTextEditWidget nameInput;
        TextWidget nameErrorMessage;

        public ContactFormWidget(string subjectText, string bodyText)
        {
            SetButtonAttributes();
            AnchorAll();

			cancelButton = textImageButtonFactory.Generate(new LocalizedString("Cancel").Translated);
			submitButton = textImageButtonFactory.Generate(new LocalizedString("Submit").Translated);
			doneButton = textImageButtonFactory.Generate(new LocalizedString("Done").Translated);
            doneButton.Visible = false;

            DoLayout(subjectText, bodyText);
            AddButtonHandlers();
        }

        private GuiWidget LabelGenerator(string labelText, int fontSize = 12, int height = 28)
        {
            GuiWidget labelContainer = new GuiWidget();
            labelContainer.HAnchor = HAnchor.ParentLeftRight;
            labelContainer.Height = height;

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
            TextWidget formLabel = new TextWidget("", pointSize:11);
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

			TextWidget formLabel = new TextWidget(new LocalizedString("How can we help?").Translated, pointSize:16);
            formLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            formLabel.VAnchor = VAnchor.ParentTop;
            formLabel.HAnchor = HAnchor.ParentLeft;
            formLabel.Margin = new BorderDouble(10, 10);
            labelContainer.AddChild(formLabel);
            mainContainer.AddChild(labelContainer);

            centerContainer = new GuiWidget();
            centerContainer.AnchorAll();
            centerContainer.Padding = new BorderDouble(10);

            messageContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            messageContainer.AnchorAll();
            messageContainer.Visible = false;
            messageContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            messageContainer.Padding = new BorderDouble(10);
            
			submissionStatus = new TextWidget(new LocalizedString("Submitting your information...").Translated, pointSize: 13);
            submissionStatus.AutoExpandBoundsToText = true;
            submissionStatus.Margin = new BorderDouble(0, 5);
            submissionStatus.TextColor = RGBA_Bytes.White;
            submissionStatus.HAnchor = HAnchor.ParentLeft;

            messageContainer.AddChild(submissionStatus);

            formContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            formContainer.AnchorAll();
            formContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            formContainer.Padding = new BorderDouble(10);

			formContainer.AddChild(LabelGenerator(new LocalizedString("Question*").Translated));
			formContainer.AddChild(LabelGenerator(new LocalizedString("Briefly describe your question").Translated, 9, 14));

            questionInput = new MHTextEditWidget(subjectText);
            questionInput.HAnchor = HAnchor.ParentLeftRight;
            formContainer.AddChild(questionInput);

            questionErrorMessage = ErrorMessageGenerator();
            formContainer.AddChild(questionErrorMessage);

			formContainer.AddChild(LabelGenerator(new LocalizedString("Details*").Translated));
			formContainer.AddChild(LabelGenerator(new LocalizedString("Fill in the details here").Translated, 9, 14));

            detailInput = new MHTextEditWidget(bodyText, pixelHeight: 120, multiLine: true);
            detailInput.HAnchor = HAnchor.ParentLeftRight;
            formContainer.AddChild(detailInput);

            detailErrorMessage = ErrorMessageGenerator();
            formContainer.AddChild(detailErrorMessage);

			formContainer.AddChild(LabelGenerator(new LocalizedString("Your Email Address*").Translated));

            emailInput = new MHTextEditWidget();
            emailInput.HAnchor = HAnchor.ParentLeftRight;
            formContainer.AddChild(emailInput);

            emailErrorMessage = ErrorMessageGenerator();
            formContainer.AddChild(emailErrorMessage);

			formContainer.AddChild(LabelGenerator(new LocalizedString("Your Name*").Translated));

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
            
            List<FormField> formFields = new List<FormField>{};
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
            cancelButton.Click += (sender, e) => { Close(); };
            doneButton.Click += (sender, e) => { Close(); };
            submitButton.Click += new ButtonBase.ButtonEventHandler(SubmitContactForm);
        }

        void SubmitContactForm(object sender, MouseEventArgs mouseEvent)
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
                postRequest.RequestFailed += new EventHandler(onPostRequestFailed);
                postRequest.Request();                
            }
        }

        void onPostRequestSucceeded(object sender, EventArgs e)
        {
			submissionStatus.Text = new LocalizedString("Thank you!  Your information has been submitted.").Translated;
            doneButton.Visible = true;
        }

        void onPostRequestFailed(object sender, EventArgs e)
        {
			submissionStatus.Text = new LocalizedString("Sorry!  We weren't able to submit your request.").Translated;
            doneButton.Visible = true;
        }

        private FlowLayoutWidget GetButtonButtonPanel()
        {
            FlowLayoutWidget buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
            buttonBottomPanel.Padding = new BorderDouble(10, 3);
            buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            return buttonBottomPanel;
        }

        private void SetButtonAttributes()
        {
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;

            whiteButtonFactory.FixedWidth = 138;
            whiteButtonFactory.normalFillColor = RGBA_Bytes.White;
            whiteButtonFactory.normalTextColor = RGBA_Bytes.Black;
            whiteButtonFactory.hoverTextColor = RGBA_Bytes.Black;
            whiteButtonFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 200);
        }
    }

    public class ContactFormWindow : SystemWindow
    {
        static ContactFormWindow contactFormWindow;
        static bool contactFormIsOpen;

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

        ContactFormWidget contactFormWidget;

        private ContactFormWindow(string subject = "", string bodyText = "")
            : base(500, 550)
        {
			Title = new LocalizedString("MatterControl: Submit an Issue").Translated;

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            contactFormWidget = new ContactFormWidget(subject, bodyText);

            AddChild(contactFormWidget);
            AddHandlers();

            ShowAsSystemWindow();
            MinimumSize = new Vector2(500, 550);
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(Instance_ThemeChanged, ref unregisterEvents);
            contactFormWidget.Closed += (sender, e) => { Close(); };
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void Instance_ThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }
    }
}
