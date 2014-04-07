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
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class SlicePresetDetailWidget : GuiWidget
    {
        TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        SlicePresetsWindow windowController;
        TextWidget presetNameError;
        MHTextEditWidget presetNameInput;
        Button savePresetButton;

        public SlicePresetDetailWidget(SlicePresetsWindow windowController)
        {
            this.windowController = windowController;
            this.AnchorAll();
            if (this.windowController.ActivePresetLayer == null)
            {
                initSlicePreset();
            }
            linkButtonFactory.fontSize = 10;
            AddElements();
            AddHandlers();
        }

        void AddHandlers()
        {
            savePresetButton.Click += savePresets_Click;
        }


        void AddElements()
        {
            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.Padding = new BorderDouble(3);
            mainContainer.AnchorAll();

            mainContainer.AddChild(GetTopRow());
            mainContainer.AddChild(GetMiddleRow());
            mainContainer.AddChild(GetBottomRow());

            this.AddChild(mainContainer);
        }

        FlowLayoutWidget GetTopRow()
        {
            FlowLayoutWidget metaContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            metaContainer.HAnchor = HAnchor.ParentLeftRight;
            metaContainer.Padding = new BorderDouble(0, 3);            
            
            FlowLayoutWidget firstRow = new FlowLayoutWidget();
            firstRow.HAnchor = HAnchor.ParentLeftRight;
            
            TextWidget labelText = new TextWidget("Edit Preset:".FormatWith(windowController.filterLabel.Localize()), pointSize: 14);
            labelText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            labelText.VAnchor = VAnchor.ParentCenter;
            labelText.Margin = new BorderDouble(right: 4);

            presetNameInput = new MHTextEditWidget(windowController.ActivePresetLayer.settingsCollectionData.Name);
            presetNameInput.HAnchor = HAnchor.ParentLeftRight;

            firstRow.AddChild(labelText);
            firstRow.AddChild(presetNameInput);

            presetNameError = new TextWidget("This is an error message", 0, 0, 10);
            presetNameError.TextColor = RGBA_Bytes.Red;
            presetNameError.HAnchor = HAnchor.ParentLeftRight;
            presetNameError.Margin = new BorderDouble(top: 3);
            presetNameError.Visible = false;

            FlowLayoutWidget secondRow = new FlowLayoutWidget();
            secondRow.HAnchor = HAnchor.ParentLeftRight;
            
            secondRow.AddChild(new GuiWidget(labelText.Width + 4,1));
            secondRow.AddChild(presetNameError);

            metaContainer.AddChild(firstRow);
            metaContainer.AddChild(secondRow);

            return metaContainer;
        }

        FlowLayoutWidget GetMiddleRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
            container.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            container.Margin = new BorderDouble(0, 3);
            return container;
        }

        FlowLayoutWidget GetBottomRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;

            savePresetButton = buttonFactory.Generate(LocalizedString.Get("Save"));
            Button cancelButton = buttonFactory.Generate(LocalizedString.Get("Cancel"));
            cancelButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    windowController.ChangeToSlicePresetList();
                });
            };

            container.AddChild(savePresetButton);
            container.AddChild(new HorizontalSpacer());
            container.AddChild(cancelButton);

            return container;
        }

        private bool ValidatePresetsForm()
        {
            ValidationMethods validationMethods = new ValidationMethods();

            List<FormField> formFields = new List<FormField> { };
            FormField.ValidationHandler[] stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
            FormField.ValidationHandler[] nameValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringHasNoSpecialChars };

            formFields.Add(new FormField(presetNameInput, presetNameError, stringValidationHandlers));

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

        void initSlicePreset()
        {
            int noExistingPresets = ExistingPresetsCount() + 1;
            
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
            DataStorage.SliceSettingsCollection collection = new DataStorage.SliceSettingsCollection();

            collection.Name = string.Format("{0} ({1})", windowController.filterLabel, noExistingPresets.ToString());
            collection.Tag = windowController.filterTag;

            windowController.ActivePresetLayer = new SettingsLayer(collection, settingsDictionary);
        }

        public int ExistingPresetsCount()
        {            
            string query = string.Format("SELECT COUNT(*) FROM SliceSettingsCollection WHERE Tag = '{0}';", windowController.filterTag);
            string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
            return Convert.ToInt32(result);
        }

        void savePresets_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                if (ValidatePresetsForm())
                {
                    saveActivePresets();
                    windowController.functionToCallOnSave(this, null);
                    windowController.ChangeToSlicePresetList();
                }
            });
        }

        void saveActivePresets()
        {
            windowController.ActivePresetLayer.settingsCollectionData.Name = presetNameInput.Text;
            windowController.ActivePresetLayer.settingsCollectionData.Commit();
        }
    }
}