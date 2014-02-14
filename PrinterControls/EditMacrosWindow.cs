/*
Copyright (c) 2013, Lars Brubaker
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
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class MacroDetailWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        EditMacrosWindow windowController;
        MHTextEditWidget macroNameInput;
        TextWidget macroNameError;
        MHTextEditWidget macroCommandInput;
        TextWidget macroCommandError;

        public MacroDetailWidget(EditMacrosWindow windowController)
        {
            this.windowController = windowController;
            if (this.windowController.ActiveMacro == null)
            {
                initMacro();
            }


            linkButtonFactory.fontSize = 10;
            
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);

            {
				string editMacroLabel = new LocalizedString("Edit Macro").Translated;
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

            presetsFormContainer.AddChild(createMacroNameContainer());
            presetsFormContainer.AddChild(createMacroCommandContainer());


			Button addMacroButton = textImageButtonFactory.Generate(new LocalizedString("Save").Translated);
            addMacroButton.Click += new ButtonBase.ButtonEventHandler(saveMacro_Click);

			Button cancelPresetsButton = textImageButtonFactory.Generate(new LocalizedString("Cancel").Translated);
            cancelPresetsButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
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

        private FlowLayoutWidget createMacroNameContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string macroNameLabelTxt = new LocalizedString("Macro Name").Translated;
			string macroNameLabelTxtFull = string.Format("{0}:", macroNameLabelTxt);
			TextWidget macroNameLabel = new TextWidget( macroNameLabelTxtFull, 0, 0, 12);
            macroNameLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            macroNameLabel.HAnchor = HAnchor.ParentLeftRight;
            macroNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

            macroNameInput = new MHTextEditWidget(windowController.ActiveMacro.Name);
            macroNameInput.HAnchor = HAnchor.ParentLeftRight;

			string giveMacroANameLbl = new LocalizedString("Give your macro a name").Translated;
			string giveMacroANameLblFull = string.Format ("{0}.", giveMacroANameLbl);
			macroNameError = new TextWidget(giveMacroANameLblFull, 0, 0, 10);
            macroNameError.TextColor = RGBA_Bytes.White;
            macroNameError.HAnchor = HAnchor.ParentLeftRight;
            macroNameError.Margin = elementMargin;

            container.AddChild(macroNameLabel);
            container.AddChild(macroNameInput);
            container.AddChild(macroNameError);
            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private FlowLayoutWidget createMacroCommandContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string macroCommandLblTxt = new LocalizedString("Macro Commands").Translated;
			string macroCommandLblTxtFull = string.Format ("{0}:", macroCommandLblTxt);
			TextWidget macroCommandLabel = new TextWidget(macroCommandLblTxtFull, 0, 0, 12);
            macroCommandLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            macroCommandLabel.HAnchor = HAnchor.ParentLeftRight;
            macroCommandLabel.Margin = new BorderDouble(0, 0, 0, 1);

            macroCommandInput = new MHTextEditWidget(windowController.ActiveMacro.Value, pixelHeight: 120, multiLine: true);
            macroCommandInput.HAnchor = HAnchor.ParentLeftRight;

			string shouldBeGCodeLbl = new LocalizedString("This should be in 'Gcode'").Translated;
			string shouldBeGCodeLblFull = string.Format("{0}.", shouldBeGCodeLbl);
			macroCommandError = new TextWidget(shouldBeGCodeLblFull, 0, 0, 10);
            macroCommandError.TextColor = RGBA_Bytes.White;
            macroCommandError.HAnchor = HAnchor.ParentLeftRight;
            macroCommandError.Margin = elementMargin;

            container.AddChild(macroCommandLabel);
            container.AddChild(macroCommandInput);
            container.AddChild(macroCommandError);
            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private bool ValidateMacroForm()
        {
            ValidationMethods validationMethods = new ValidationMethods();

            List<FormField> formFields = new List<FormField> { };
            FormField.ValidationHandler[] stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
            FormField.ValidationHandler[] nameValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringHasNoSpecialChars };

            formFields.Add(new FormField(macroNameInput, macroNameError, nameValidationHandlers));
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

        void initMacro()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                windowController.ActiveMacro = new CustomCommands();
                windowController.ActiveMacro.PrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;
                windowController.ActiveMacro.Name = "Home All";
                windowController.ActiveMacro.Value = "G28 ; Home All Axes";
            }
            else
            {
                throw new Exception("Macros require a printer profile");
            }
        }

        void saveMacro_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                if (ValidateMacroForm())
                {
                    saveActiveMacro();
                    windowController.functionToCallOnSave(this, null);
                    windowController.ChangeToMacroList();
                }
            });
        }

        void saveActiveMacro()
        {   
            windowController.ActiveMacro.Name = macroNameInput.Text;
            windowController.ActiveMacro.Value = macroCommandInput.Text;
            windowController.ActiveMacro.Commit();
        }

    }
    
    public class MacroListWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        EditMacrosWindow windowController;

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
				string macroPresetsLabel = new LocalizedString("Macro Presets").Translated;
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

            IEnumerable<DataStorage.CustomCommands> macroList = GetMacros();
            int buttonCount = 0;
            foreach (DataStorage.CustomCommands m in macroList)
            {                
                FlowLayoutWidget macroRow = new FlowLayoutWidget();
                macroRow.Margin = new BorderDouble(3, 0, 3, 3);
                macroRow.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                macroRow.Padding = new BorderDouble(3);
                macroRow.BackgroundColor = RGBA_Bytes.White;

                TextWidget buttonLabel = new TextWidget(m.Name);
                macroRow.AddChild(buttonLabel);

                FlowLayoutWidget hSpacer = new FlowLayoutWidget();
                hSpacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                macroRow.AddChild(hSpacer);

				Button editLink = linkButtonFactory.Generate(new LocalizedString("edit").Translated);
                editLink.Margin = new BorderDouble(right: 5);
                editLink.Click += (sender, e) =>
                {
                    windowController.ChangeToMacroDetail(m);
                };
                macroRow.AddChild(editLink);

				Button removeLink = linkButtonFactory.Generate(new LocalizedString("remove").Translated);
                removeLink.Click += (sender, e) => 
                {
                    m.Delete();
                    windowController.functionToCallOnSave(this, null);
                    windowController.ChangeToMacroList();
                };
                macroRow.AddChild(removeLink);

                presetsFormContainer.AddChild(macroRow);

            }


			Button addMacroButton = textImageButtonFactory.Generate(new LocalizedString("Add").Translated, "icon_circle_plus.png");
            addMacroButton.Click += new ButtonBase.ButtonEventHandler(addMacro_Click);

			Button cancelPresetsButton = textImageButtonFactory.Generate(new LocalizedString("Close").Translated);
            cancelPresetsButton.Click += (sender, e) => { this.windowController.Close(); };

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

        IEnumerable<DataStorage.CustomCommands> GetMacros()
        {
            IEnumerable<DataStorage.CustomCommands> results = Enumerable.Empty<DataStorage.CustomCommands>();
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                //Retrieve a list of saved printers from the Datastore
                string query = string.Format("SELECT * FROM CustomCommands WHERE PrinterId = {0};", ActivePrinterProfile.Instance.ActivePrinter.Id);
                results = (IEnumerable<DataStorage.CustomCommands>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.CustomCommands>(query);
                return results;
            }
            return results;
        }

        void addMacro_Click(object sender, MouseEventArgs mouseEvent)
        {            
            windowController.ChangeToMacroDetail();
        }
    }
    
    public class EditMacrosWindow : SystemWindow
    {
        
        public EventHandler functionToCallOnSave;

        public DataStorage.CustomCommands ActiveMacro;

        public EditMacrosWindow(IEnumerable<DataStorage.CustomCommands> macros, EventHandler functionToCallOnSave)
            : base(360, 420)
        {
			Title = new LocalizedString("Macro Editor").Translated;
            this.functionToCallOnSave = functionToCallOnSave;
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            ChangeToMacroList();
            ShowAsSystemWindow();
        }


        public void ChangeToMacroList()
        {
            this.ActiveMacro = null;
            UiThread.RunOnIdle(DoChangeToMacroList);
        }

        private void DoChangeToMacroList(object state)
        {
            GuiWidget macroListWidget = new MacroListWidget(this);
            this.RemoveAllChildren();
            this.AddChild(macroListWidget);
            this.Invalidate();
        }

        public void ChangeToMacroDetail(CustomCommands macro = null)
        {
            this.ActiveMacro = macro;
            UiThread.RunOnIdle(DoChangeToMacroDetail);
        }

        private void DoChangeToMacroDetail(object state)
        {
            GuiWidget macroDetailWidget = new MacroDetailWidget(this);
            this.RemoveAllChildren();
            this.AddChild(macroDetailWidget);
            this.Invalidate();
        }


        
    }
}