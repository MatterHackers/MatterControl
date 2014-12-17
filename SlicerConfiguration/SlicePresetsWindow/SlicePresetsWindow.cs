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
    public class SlicePresetsWindow : SystemWindow
    {

        public EventHandler functionToCallOnSave;
        public string filterTag;
        public string filterLabel;
        public SettingsLayer ActivePresetLayer;        

        public SlicePresetsWindow(EventHandler functionToCallOnSave, string filterLabel, string filterTag, bool showList=true, int collectionID=0)
            : base(640, 480)
        {
            AlwaysOnTopOfMain = true;
            Title = LocalizedString.Get("Slice Presets Editor");
            
            this.filterTag = filterTag;
            this.filterLabel = filterLabel;
            
            this.functionToCallOnSave = functionToCallOnSave;

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            if (showList)
            {
                ChangeToSlicePresetList();
            }
            else
            {
                if (collectionID == 0)
                {
                    ChangeToSlicePresetDetail();
                }
                else
                {
                    ChangeToSlicePresetDetail(GetCollection(collectionID));
                }
            }
            ShowAsSystemWindow();
            this.MinimumSize = new Vector2(640, 480);
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

        public void ChangeToSlicePresetFromID(int collectionId)
        {
            ChangeToSlicePresetDetail(GetCollection(collectionId));
        }

        public void ChangeToSlicePresetDetail(SliceSettingsCollection collection = null)
        {
            if (collection != null)
            {
                Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
                IEnumerable<DataStorage.SliceSetting> settingsList = GetCollectionSettings(collection.Id);
                foreach (DataStorage.SliceSetting s in settingsList)
                {
                    settingsDictionary[s.Name] = s;
                }
                this.ActivePresetLayer = new SettingsLayer(collection, settingsDictionary);
            }
           UiThread.RunOnIdle(DoChangeToSlicePresetDetail);
        }

        DataStorage.SliceSettingsCollection GetCollection(int collectionId)
        {
            return DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == collectionId).Take(1).FirstOrDefault();
        }

        IEnumerable<DataStorage.SliceSettingsCollection> GetPresets(string filterTag)
        {
            //Retrieve a list of presets from the Datastore
            string query = string.Format("SELECT * FROM SliceSettingsCollection WHERE Tag = {0};", filterTag);
            IEnumerable<DataStorage.SliceSettingsCollection> result = (IEnumerable<DataStorage.SliceSettingsCollection>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSettingsCollection>(query);
            return result;
        }

        public IEnumerable<DataStorage.SliceSetting> GetCollectionSettings(int collectionId)
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