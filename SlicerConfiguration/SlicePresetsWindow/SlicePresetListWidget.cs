﻿/*
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

    public class SlicePresetListWidget : GuiWidget
    {
        SlicePresetsWindow windowController;
        TextImageButtonFactory buttonFactory;
        LinkButtonFactory linkButtonFactory;
        PresetListControl presetListControl;
        Button importPresetButton;

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
            AddHandlers();
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

        void AddHandlers()
        {
            importPresetButton.Click += new EventHandler(importPreset_Click);
        }

        FlowLayoutWidget GetTopRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.Padding = new BorderDouble(0, 6);
            TextWidget labelText = new TextWidget("{0} Presets:".FormatWith(windowController.filterLabel.Localize()), pointSize: 14);
            labelText.TextColor = ActiveTheme.Instance.PrimaryTextColor;

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
            container.Margin = new BorderDouble(0, 3, 0, 0);

            presetListControl = new PresetListControl();

            IEnumerable<DataStorage.SliceSettingsCollection> collections = GetCollections();
            foreach (DataStorage.SliceSettingsCollection collection in collections)
            {
                presetListControl.AddChild(new PresetListItem(this.windowController, collection));
            }
            container.AddChild(presetListControl);

            return container;
        }

        FlowLayoutWidget GetBottomRow()
        {
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;

            Button addPresetButton = buttonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
            addPresetButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    windowController.ChangeToSlicePresetDetail();
                });
            };

            importPresetButton = buttonFactory.Generate(LocalizedString.Get("Import"));

            Button closeButton = buttonFactory.Generate(LocalizedString.Get("Close"));
            closeButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    windowController.Close();
                });
            };

            container.AddChild(addPresetButton);
            container.AddChild(importPresetButton);
            container.AddChild(new HorizontalSpacer());
            container.AddChild(closeButton);

            return container;
        }


        void importPreset_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(importPresetDo);
        }

        void importPresetDo(object state)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Load Slice Preset|*.slice;*.ini");
            openParams.ActionButtonLabel = "Load Slice Preset";
            openParams.Title = "MatterControl: Select A File"; 

			FileDialog.OpenFileDialog(openParams, onPresetLoad);
        }

		void onPresetLoad(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{                
				DataStorage.SliceSettingsCollection settingsCollection;
				try
				{
					if (File.Exists(openParams.FileName))
					{
						//Create collection to hold preset settings
						settingsCollection = new SliceSettingsCollection();
						settingsCollection.Tag = windowController.filterTag;
						settingsCollection.PrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;
						settingsCollection.Name = System.IO.Path.GetFileNameWithoutExtension(openParams.FileName);
						settingsCollection.Commit();

						string[] lines = System.IO.File.ReadAllLines(openParams.FileName);
						foreach (string line in lines)
						{
							//Ignore commented lines
							if (!line.StartsWith("#"))
							{
								string[] settingLine = line.Split('=');
								string keyName = settingLine[0].Trim();
								string settingDefaultValue = settingLine[1].Trim();

								//To do - validate imported settings as valid (KP)
								DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
								sliceSetting.Name = keyName;
								sliceSetting.Value = settingDefaultValue;                                
								sliceSetting.SettingsCollectionId = settingsCollection.Id;
								sliceSetting.Commit();
							}
						}
						windowController.ChangeToSlicePresetList();
					}
				}
				catch (Exception)
				{                    
					// Error loading configuration
				}
			}
		}

        IEnumerable<DataStorage.SliceSettingsCollection> GetCollections()
        {
            IEnumerable<DataStorage.SliceSettingsCollection> results = Enumerable.Empty<DataStorage.SliceSettingsCollection>();
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                //Retrieve a list of collections matching from the Datastore
                string query = string.Format("SELECT * FROM SliceSettingsCollection WHERE Tag = '{0}' AND PrinterId = {1}  ORDER BY Name;", windowController.filterTag, ActivePrinterProfile.Instance.ActivePrinter.Id);
                results = (IEnumerable<DataStorage.SliceSettingsCollection>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSettingsCollection>(query);
            }
            return results;
        }

        class PresetListItem : FlowLayoutWidget
        {
            DataStorage.SliceSettingsCollection preset;
            DataStorage.SliceSettingsCollection Preset { get { return preset; } }

            public PresetListItem(SlicePresetsWindow windowController, DataStorage.SliceSettingsCollection preset)
            {
                this.preset = preset;
                this.BackgroundColor = RGBA_Bytes.White;
                this.HAnchor = HAnchor.ParentLeftRight;
                this.Margin = new BorderDouble(6,0,6,3);
                this.Padding = new BorderDouble(3);

                LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
                linkButtonFactory.fontSize = 10;

                int maxLabelWidth = 300;
                TextWidget materialLabel = new TextWidget(preset.Name, pointSize:14);
                materialLabel.EllipsisIfClipped = true;
                materialLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                materialLabel.MinimumSize = new Vector2(maxLabelWidth, materialLabel.Height);
                materialLabel.Width = maxLabelWidth;

                Button materialEditLink = linkButtonFactory.Generate("edit");
                materialEditLink.VAnchor = Agg.UI.VAnchor.ParentCenter;
                materialEditLink.Click += (sender, e) =>
                {
                    UiThread.RunOnIdle((state) =>
                    {
                        windowController.ChangeToSlicePresetDetail(preset);
                    });
                };


                Button materialRemoveLink = linkButtonFactory.Generate("remove");
                materialRemoveLink.Margin = new BorderDouble(left: 4);
                materialRemoveLink.VAnchor = Agg.UI.VAnchor.ParentCenter;
                materialRemoveLink.Click += (sender, e) =>
                {
                    UiThread.RunOnIdle((state) =>
                    {
                        //Unwind this setting if it is currently active
                        if (ActivePrinterProfile.Instance.ActivePrinter != null)
                        {
                            if (preset.Id == ActivePrinterProfile.Instance.ActiveQualitySettingsID)
                            {
                                ActivePrinterProfile.Instance.ActiveQualitySettingsID = 0;
                            }
                            
                            string[] activeMaterialPresets = ActivePrinterProfile.Instance.ActivePrinter.MaterialCollectionIds.Split(',');
                            for (int i = 0; i < activeMaterialPresets.Count(); i++)
                            {
                                int index = 0;
                                Int32.TryParse(activeMaterialPresets[i],out index);
                                if (preset.Id == index)
                                {
                                    ActivePrinterProfile.Instance.SetMaterialSetting(i + 1, 0);
                                }
                            }
                        }
                        preset.Delete();
                        windowController.ChangeToSlicePresetList();
                        ActiveSliceSettings.Instance.LoadAllSettings();
                        ApplicationController.Instance.ReloadAdvancedControlsPanel();
                    });
                };

                this.AddChild(materialLabel);
                this.AddChild(new HorizontalSpacer());
                this.AddChild(materialEditLink);
                this.AddChild(materialRemoveLink);
                
                this.Height = 35;

            }
        }

        class PresetListControl : ScrollableWidget
        {
            FlowLayoutWidget topToBottomItemList;
            
            public PresetListControl()
            {                
                this.AnchorAll();
                this.AutoScroll = true;
                this.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                              
                topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
                topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                topToBottomItemList.Margin = new BorderDouble(top: 3);
                
                base.AddChild(topToBottomItemList);
            }

            public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
            {
                FlowLayoutWidget itemHolder = new FlowLayoutWidget();
                itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
                itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                itemHolder.AddChild(child);
                itemHolder.VAnchor = VAnchor.FitToChildren;

                topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
            }
        }


    }



    
}