/*
Copyright (c) 2014, Kevin Pope
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
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class SliceSelectorWidget : FlowLayoutWidget
    {
        Button editButton;
        ImageButtonFactory imageButtonFactory = new ImageButtonFactory();        

        string filterTag;
        string filterLabel;
        public AnchoredDropDownList DropDownList;
        private TupleList<string, Func<bool>> DropDownMenuItems = new TupleList<string, Func<bool>>();
        
        public SliceSelectorWidget(string label, RGBA_Bytes accentColor, string tag=null)
            : base(FlowDirection.TopToBottom)
        {
            this.filterLabel = label;
            if (tag == null)
            {
                this.filterTag = label.ToLower();
            }
            else
            {
                this.filterTag = tag;
            }
            
            this.HAnchor = HAnchor.ParentLeftRight;
            this.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            GuiWidget accentBar = new GuiWidget(1, 5);
            accentBar.BackgroundColor = accentColor;
            accentBar.HAnchor = HAnchor.ParentLeftRight;

            TextWidget labelText = new TextWidget(LocalizedString.Get(label).ToUpper());
            labelText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            labelText.HAnchor = Agg.UI.HAnchor.ParentCenter;
            labelText.Margin = new BorderDouble(0, 3, 0, 6);

            this.AddChild(labelText);
            this.AddChild(GetPulldownContainer());
            this.AddChild(new VerticalSpacer());
            this.AddChild(accentBar);
        }

        public virtual FlowLayoutWidget GetPulldownContainer()
        {
            DropDownList = CreateDropdown();

            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.Padding = new BorderDouble(6, 0);

            editButton = imageButtonFactory.Generate("icon_edit_white.png", "icon_edit_gray.png");
            
            editButton.VAnchor = VAnchor.ParentCenter;
            editButton.Margin = new BorderDouble(right:6);
            editButton.Click += (sender, e) =>
            {
                if (ApplicationWidget.Instance.EditSlicePresetsWindow == null)
                {
                    ApplicationWidget.Instance.EditSlicePresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag);
                    ApplicationWidget.Instance.EditSlicePresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationWidget.Instance.EditSlicePresetsWindow = null; };
                }
                else
                {
                    ApplicationWidget.Instance.EditSlicePresetsWindow.BringToFront();
                }
            };

            container.AddChild(editButton);
            container.AddChild(DropDownList);
            return container;
        }

        protected void ReloadOptions(object sender, EventArgs e)
        {
            ApplicationWidget.Instance.ReloadBackPanel();
        }

        IEnumerable<DataStorage.SliceSettingsCollection> GetCollections()
        {
            IEnumerable<DataStorage.SliceSettingsCollection> results = Enumerable.Empty<DataStorage.SliceSettingsCollection>();

            //Retrieve a list of collections matching from the Datastore
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                string query = string.Format("SELECT * FROM SliceSettingsCollection WHERE Tag = '{0}' AND PrinterId = {1} ORDER BY Name;", filterTag, ActivePrinterProfile.Instance.ActivePrinter.Id);
                results = (IEnumerable<DataStorage.SliceSettingsCollection>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSettingsCollection>(query);
            }
            return results;
        }

        void onItemSelect(object sender, EventArgs e)
        {
            
            MenuItem item = (MenuItem)sender;
            if (filterTag == "material")
            {
                if (ActivePrinterProfile.Instance.GetMaterialSetting(1) != Int32.Parse(item.Value))
                {
                    ActivePrinterProfile.Instance.SetMaterialSetting(1, Int32.Parse(item.Value));
                }
            }
            else if (filterTag == "quality")
            {
                if (ActivePrinterProfile.Instance.ActiveQualitySettingsID != Int32.Parse(item.Value))
                {
                    ActivePrinterProfile.Instance.ActiveQualitySettingsID = Int32.Parse(item.Value);
                }
            }
            UiThread.RunOnIdle((state) =>
            {
                ActiveSliceSettings.Instance.LoadAllSettings();
                ApplicationWidget.Instance.ReloadBackPanel();
            });
        }

        void onNewItemSelect(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                ActiveSliceSettings.Instance.LoadAllSettings();
                ApplicationWidget.Instance.ReloadBackPanel();
                if (ApplicationWidget.Instance.EditSlicePresetsWindow == null)
                {
                    ApplicationWidget.Instance.EditSlicePresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag, false, 0);
                    ApplicationWidget.Instance.EditSlicePresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationWidget.Instance.EditSlicePresetsWindow = null; };
                }
                else
                {
                    ApplicationWidget.Instance.EditSlicePresetsWindow.ChangeToSlicePresetFromID(0);
                    ApplicationWidget.Instance.EditSlicePresetsWindow.BringToFront();
                }                
            });
        }


        AnchoredDropDownList CreateDropdown()
        {
            AnchoredDropDownList dropDownList = new AnchoredDropDownList("- default -");
            dropDownList.Margin = new BorderDouble(0, 3);
            dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);
            MenuItem defaultMenuItem = dropDownList.AddItem("- default -", "0");
            defaultMenuItem.Selected += new EventHandler(onItemSelect);

            IEnumerable<DataStorage.SliceSettingsCollection> collections = GetCollections();
            foreach (DataStorage.SliceSettingsCollection collection in collections)
            {
                MenuItem menuItem = dropDownList.AddItem(collection.Name, collection.Id.ToString());
                menuItem.Selected += new EventHandler(onItemSelect);
            }

            MenuItem addNewPreset = dropDownList.AddItem("<< Add >>", "new");
            addNewPreset.Selected += new EventHandler(onNewItemSelect);

            if (filterTag == "material")
            {
                try
                {
                    dropDownList.SelectedValue = ActivePrinterProfile.Instance.GetMaterialSetting(1).ToString();
                }
                catch
                {
                    //Unable to set selected value
                }
            }
            else if (filterTag == "quality")
            {
                try
                {
                    dropDownList.SelectedValue = ActivePrinterProfile.Instance.ActiveQualitySettingsID.ToString();
                }
                catch
                {
                    //Unable to set selected value
                }
            }

            return dropDownList;
        }

    }

    public class SliceEngineSelector : SliceSelectorWidget
    {
        public AnchoredDropDownList EngineMenuDropList;
        private TupleList<string, Func<bool>> engineOptionsMenuItems = new TupleList<string, Func<bool>>();
        
         public SliceEngineSelector(string label, RGBA_Bytes accentColor)
             :base(label, accentColor)
         {

         }

        public override FlowLayoutWidget GetPulldownContainer()
        {
            EngineMenuDropList = CreateSliceEngineDropdown();
            
            FlowLayoutWidget container = new FlowLayoutWidget();
            container.HAnchor = HAnchor.ParentLeftRight;
            container.Padding = new BorderDouble(6, 0);
            
            container.AddChild(EngineMenuDropList);
            return container;
        }

        AnchoredDropDownList CreateSliceEngineDropdown()
        {
            AnchoredDropDownList engineMenuDropList = new AnchoredDropDownList("Engine   ");
            engineMenuDropList.Margin = new BorderDouble(0,3);
            {
                MenuItem slic3rMenuItem = engineMenuDropList.AddItem(ActivePrinterProfile.SlicingEngineTypes.Slic3r.ToString());
                slic3rMenuItem.Selected += (sender, e) =>
                {
                    ActivePrinterProfile.Instance.ActiveSliceEngineType = ActivePrinterProfile.SlicingEngineTypes.Slic3r;
                    ApplicationWidget.Instance.ReloadBackPanel();
                };

                MenuItem curaEnginMenuItem = engineMenuDropList.AddItem(ActivePrinterProfile.SlicingEngineTypes.CuraEngine.ToString());
                curaEnginMenuItem.Selected += (sender, e) =>
                {
                    ActivePrinterProfile.Instance.ActiveSliceEngineType = ActivePrinterProfile.SlicingEngineTypes.CuraEngine;
                    ApplicationWidget.Instance.ReloadBackPanel();
                };

                MenuItem matterSliceMenuItem = engineMenuDropList.AddItem(ActivePrinterProfile.SlicingEngineTypes.MatterSlice.ToString());
                matterSliceMenuItem.Selected += (sender, e) =>
                {
                    ActivePrinterProfile.Instance.ActiveSliceEngineType = ActivePrinterProfile.SlicingEngineTypes.MatterSlice;
                    ApplicationWidget.Instance.ReloadBackPanel();
                };

                engineMenuDropList.SelectedLabel = ActivePrinterProfile.Instance.ActiveSliceEngineType.ToString();
            }
            engineMenuDropList.MinimumSize = new Vector2(engineMenuDropList.LocalBounds.Width, engineMenuDropList.LocalBounds.Height);
            return engineMenuDropList;
        }
    }

    public class AnchoredDropDownList : DropDownList
    {

        static RGBA_Bytes whiteSemiTransparent = new RGBA_Bytes(255, 255, 255, 100);
        static RGBA_Bytes whiteTransparent = new RGBA_Bytes(255, 255, 255, 0);

        public AnchoredDropDownList(string noSelectionString, Direction direction = Direction.Down)
            : base(noSelectionString, whiteTransparent, whiteSemiTransparent, direction)
        {
            this.HAnchor = HAnchor.ParentLeftRight;
            this.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            
            this.MenuItemsBorderWidth = 1;            
            this.MenuItemsBackgroundColor = RGBA_Bytes.White;
            this.MenuItemsBorderColor = ActiveTheme.Instance.SecondaryTextColor;
            this.MenuItemsPadding = new BorderDouble(10, 4, 10, 6);
            this.MenuItemsBackgroundHoverColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.MenuItemsTextHoverColor = ActiveTheme.Instance.PrimaryTextColor;
            this.BorderWidth = 1;
            this.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
            this.HoverColor = whiteSemiTransparent;
            this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 0);
        }
    }
}
