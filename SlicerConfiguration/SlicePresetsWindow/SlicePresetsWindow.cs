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
        MHTextEditWidget macroNameInput;
        TextWidget macroNameError;
        MHTextEditWidget macroCommandInput;
        TextWidget macroCommandError;

        public SlicePresetDetailWidget(SlicePresetsWindow windowController)
        {
            this.windowController = windowController;
            if (this.windowController.ActivePresetLayer == null)
            {
                initSlicePreset();
            }


            linkButtonFactory.fontSize = 10;

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
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.Padding = new BorderDouble(0, 6);
            TextWidget labelText = new TextWidget("{0} Presets:".FormatWith(windowController.filterLabel.Localize()), pointSize: 14);

            container.AddChild(labelText);
            container.AddChild(new HorizontalSpacer());
            return container;
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

            Button addPresetButton = buttonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
            Button cancelButton = buttonFactory.Generate(LocalizedString.Get("Cancel"));
            cancelButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    Close();
                });
            };

            container.AddChild(addPresetButton);
            container.AddChild(new HorizontalSpacer());
            container.AddChild(cancelButton);

            return container;
        }

        private FlowLayoutWidget createMacroNameContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

            string macroNameLabelTxt = LocalizedString.Get("Macro Name");
            string macroNameLabelTxtFull = string.Format("{0}:", macroNameLabelTxt);
            TextWidget macroNameLabel = new TextWidget(macroNameLabelTxtFull, 0, 0, 12);
            macroNameLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            macroNameLabel.HAnchor = HAnchor.ParentLeftRight;
            macroNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

            macroNameInput = new MHTextEditWidget(windowController.ActivePresetLayer.settingsCollectionData.Name);
            macroNameInput.HAnchor = HAnchor.ParentLeftRight;

            string giveMacroANameLbl = LocalizedString.Get("Give your macro a name");
            string giveMacroANameLblFull = string.Format("{0}.", giveMacroANameLbl);
            macroNameError = new TextWidget(giveMacroANameLblFull, 0, 0, 10);
            macroNameError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            macroNameError.HAnchor = HAnchor.ParentLeftRight;
            macroNameError.Margin = elementMargin;

            container.AddChild(macroNameLabel);
            container.AddChild(macroNameInput);
            container.AddChild(macroNameError);
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

        void initSlicePreset()
        {
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
            DataStorage.SliceSettingsCollection collection = new DataStorage.SliceSettingsCollection();
            collection.Name = "Default";
            collection.Tag = windowController.filterTag;

            windowController.ActivePresetLayer = new SettingsLayer(collection, settingsDictionary);
            
        }

        void saveMacro_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                if (ValidateMacroForm())
                {
                    saveActivePresets();
                    windowController.functionToCallOnSave(this, null);
                    windowController.ChangeToSlicePresetList();
                }
            });
        }

        void saveActivePresets()
        {
            windowController.ActivePresetLayer.settingsCollectionData.Name = macroNameInput.Text;
            windowController.ActivePresetLayer.settingsCollectionData.Commit();
        }
    }

    public class SlicePresetListWidget : GuiWidget
    {
        SlicePresetsWindow windowController;
        TextImageButtonFactory buttonFactory;
        LinkButtonFactory linkButtonFactory;

        public SlicePresetListWidget(SlicePresetsWindow windowController)
        {
            this.windowController = windowController;
            this.AnchorAll();

            linkButtonFactory = new LinkButtonFactory();

            buttonFactory = new TextImageButtonFactory();
            buttonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            buttonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            buttonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            buttonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            buttonFactory.borderWidth = 0;

            AddElements();
        }

        void AddElements()
        {
            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.Padding = new BorderDouble(3);
            mainContainer.AnchorAll();

            mainContainer.AddChild(GetTopRow());
            mainContainer.AddChild(GetMiddleRow());
            mainContainer.AddChild(GetBottmRow());

            this.AddChild(mainContainer);
        }

        FlowLayoutWidget GetTopRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.Padding = new BorderDouble(0, 6);
            TextWidget labelText = new TextWidget("{0} Presets:".FormatWith(windowController.filterLabel.Localize()), pointSize:14);           

            container.AddChild(labelText);
            container.AddChild(new HorizontalSpacer());
            return container;
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

        FlowLayoutWidget GetBottmRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;

            Button addPresetButton = buttonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
            Button cancelButton = buttonFactory.Generate(LocalizedString.Get("Cancel"));
            cancelButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    Close();
                });
            };

            container.AddChild(addPresetButton);
            container.AddChild(new HorizontalSpacer());
            container.AddChild(cancelButton);

            return container;
        }

        IEnumerable<DataStorage.SliceSettingsCollection> GetCollections()
        {
            IEnumerable<DataStorage.SliceSettingsCollection> results = Enumerable.Empty<DataStorage.SliceSettingsCollection>();
            
            //Retrieve a list of collections matching from the Datastore
            string query = string.Format("SELECT * FROM SliceSettingsCollection WHERE Tag = {0};", windowController.filterTag);
            results = (IEnumerable<DataStorage.SliceSettingsCollection>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSettingsCollection>(query);
            return results;
        }
    }

    public class SlicePresetsWindow : SystemWindow
    {

        public EventHandler functionToCallOnSave;
        public string filterTag;
        public string filterLabel;
        public SettingsLayer ActivePresetLayer;        

        public SlicePresetsWindow(EventHandler functionToCallOnSave, string filterLabel, string filterTag)
            : base(420, 560)
        {
            
            Title = LocalizedString.Get("Slice Presets Editor");
            
            this.filterTag = filterTag;
            this.filterLabel = filterLabel;
            this.MinimumSize = new Vector2(420, 560);
            this.functionToCallOnSave = functionToCallOnSave;

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            ChangeToSlicePresetList();
            ShowAsSystemWindow();
        }


        public void ChangeToSlicePresetList()
        {
            this.ActivePresetLayer = null;
            UiThread.RunOnIdle(DoChangeToSlicePresetList);
        }

        private void DoChangeToSlicePresetList(object state)
        {
            GuiWidget slicePresetWidget = new SlicePresetListWidget(this);
            this.RemoveAllChildren();
            this.AddChild(slicePresetWidget);
            this.Invalidate();
        }

        public void ChangeToSlicePresetDetail(SliceSettingsCollection collection = null)
        {
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
            IEnumerable<DataStorage.SliceSetting> settingsList = GetCollectionSettings(collection.Id);
            foreach (DataStorage.SliceSetting s in settingsList)
            {
                settingsDictionary[s.Name] = s;
            }            
            this.ActivePresetLayer = new SettingsLayer(collection, settingsDictionary);
            UiThread.RunOnIdle(DoChangeToSlicePresetDetail);
        }

        IEnumerable<DataStorage.SliceSetting> GetCollectionSettings(int collectionId)
        {
            //Retrieve a list of slice settings from the Datastore
            string query = string.Format("SELECT * FROM SliceSetting WHERE SettingsCollectionID = {0};", collectionId);
            IEnumerable<DataStorage.SliceSetting> result = (IEnumerable<DataStorage.SliceSetting>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSetting>(query);
            return result;
        }

        private void DoChangeToSlicePresetDetail(object state)
        {
            GuiWidget macroDetailWidget = new SlicePresetDetailWidget(this);
            this.RemoveAllChildren();
            this.AddChild(macroDetailWidget);
            this.Invalidate();
        }
    }
}