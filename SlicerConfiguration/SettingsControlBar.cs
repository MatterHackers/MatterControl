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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class EnhancedSettingsControlBar : FlowLayoutWidget
    {   
        public EnhancedSettingsControlBar()
        {                     
            this.HAnchor = HAnchor.ParentLeftRight;
            //this.AddChild(GetSliceEngineContainer());

            int numberOfHeatedExtruders = 1;
            if (ActiveSliceSettings.Instance.GetActiveValue("extruders_share_temperature") == "0")
            {
                numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
            }


            if (numberOfHeatedExtruders == 1)
            {
                this.AddChild(new SliceEngineSelector("Slice Engine".Localize(), RGBA_Bytes.YellowGreen));
                this.AddChild(new GuiWidget(8, 0));
            }
            this.AddChild(new SliceSelectorWidget("Quality".Localize(), RGBA_Bytes.Yellow, "quality"));
            this.AddChild(new GuiWidget(8, 0));

            if (numberOfHeatedExtruders > 1)
            {
                List<RGBA_Bytes> colorList = new List<RGBA_Bytes>() { RGBA_Bytes.Orange, RGBA_Bytes.Violet, RGBA_Bytes.YellowGreen };
                
                for (int i = 0; i < numberOfHeatedExtruders; i++)
                {
                    if (i > 0)
                    {
                        this.AddChild(new GuiWidget(8, 0));
                    }
                    int colorIndex = i % colorList.Count;
                    RGBA_Bytes color = colorList[colorIndex];
                    this.AddChild(new SliceSelectorWidget(string.Format("{0} {1}", "Material".Localize(), i + 1), color, "material", i+1));
                    
                }
            }
            else
            {
                this.AddChild(new SliceSelectorWidget("Material".Localize(), RGBA_Bytes.Orange, "material"));
            }

            //this.AddChild(new GuiWidget(6, 0));
            //this.AddChild(new SliceSelectorWidget("Item", RGBA_Bytes.Violet)); 
            this.Height = 70;
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            //
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }
    }
    
    public class SettingsControlBar : FlowLayoutWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextWidget settingsStatusDescription;
        TextWidget unsavedChangesIndicator;
        Button saveButton;
        Button revertbutton;

        public DropDownMenu sliceOptionsMenuDropList;
        private TupleList<string, Func<bool>> slicerOptionsMenuItems;
        
        public SettingsControlBar()
            : base(FlowDirection.TopToBottom)
        {
            SetDisplayAttributes();
            AddChildElements();
            AddHandlers();
        }        
            
        void SetDisplayAttributes()
        {            
            this.HAnchor |= HAnchor.ParentLeftRight;
            this.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            this.Padding = new BorderDouble(6,10,6,5);
        }

        void AddChildElements()
        {
            EnhancedSettingsControlBar topRow = new EnhancedSettingsControlBar();
            FlowLayoutWidget bottomRow = new FlowLayoutWidget();

            bottomRow.HAnchor = HAnchor.ParentLeftRight;
            bottomRow.Margin = new BorderDouble(bottom:4);

            FlowLayoutWidget settingsStatusLabelContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            settingsStatusLabelContainer.VAnchor |= VAnchor.ParentTop;
            settingsStatusLabelContainer.Margin = new BorderDouble(0);
            {   
                string activeSettingsLabelText = LocalizedString.Get ("Active Settings").ToUpper();
				string activeSettingsLabelTextFull = string.Format ("{0}:", activeSettingsLabelText);

				TextWidget settingsStatusLabel = new TextWidget(string.Format(activeSettingsLabelTextFull), pointSize: 10);
                settingsStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                settingsStatusDescription = new TextWidget("", pointSize: 14);
                settingsStatusDescription.Margin = new BorderDouble(top: 4);
                settingsStatusDescription.AutoExpandBoundsToText = true;
                settingsStatusDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                string unsavedChangesTxtBeg = LocalizedString.Get("unsaved changes");
				string unsavedChangesTxtFull = string.Format ("({0})", unsavedChangesTxtBeg);
				unsavedChangesIndicator = new TextWidget(unsavedChangesTxtFull, pointSize: 10);
                unsavedChangesIndicator.AutoExpandBoundsToText = true;
                unsavedChangesIndicator.Visible = false;
                unsavedChangesIndicator.Margin = new BorderDouble(left: 4);
                unsavedChangesIndicator.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                settingsStatusLabelContainer.AddChild(settingsStatusLabel);
                settingsStatusLabelContainer.AddChild(settingsStatusDescription);
                settingsStatusLabelContainer.AddChild(unsavedChangesIndicator);
            }

			saveButton = textImageButtonFactory.Generate(LocalizedString.Get("Save"));
            saveButton.VAnchor = VAnchor.ParentCenter;
            saveButton.Visible = false;
            saveButton.Margin = new BorderDouble(0, 0, 0, 10);
			saveButton.Click += new ButtonBase.ButtonEventHandler(saveButton_Click);

			revertbutton = textImageButtonFactory.Generate(LocalizedString.Get("Revert"));
            revertbutton.VAnchor = VAnchor.ParentCenter;
            revertbutton.Visible = false;
            revertbutton.Margin = new BorderDouble(0,0,0,10);
            revertbutton.Click += new ButtonBase.ButtonEventHandler(revertbutton_Click);		
            
            bottomRow.AddChild(settingsStatusLabelContainer);

            GuiWidget spacer = new GuiWidget(HAnchor.ParentLeftRight);
            bottomRow.AddChild(spacer);

            bottomRow.AddChild(saveButton);
            bottomRow.AddChild(revertbutton);
            bottomRow.AddChild(GetSliceOptionsMenuDropList());

            this.AddChild(bottomRow);
            this.AddChild(topRow);

            SetStatusDisplay();
        }

        DropDownMenu GetSliceOptionsMenuDropList()
        {
            if (sliceOptionsMenuDropList == null)
            {
                sliceOptionsMenuDropList = new DropDownMenu(LocalizedString.Get("Options   "));
                sliceOptionsMenuDropList.HoverColor = new RGBA_Bytes(0, 0, 0, 50);
                sliceOptionsMenuDropList.NormalColor = new RGBA_Bytes(0, 0, 0, 0);
                sliceOptionsMenuDropList.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
                sliceOptionsMenuDropList.BackgroundColor = new RGBA_Bytes(0, 0, 0, 0);
                sliceOptionsMenuDropList.BorderWidth = 1;
                sliceOptionsMenuDropList.VAnchor |= VAnchor.ParentCenter;
                sliceOptionsMenuDropList.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
                sliceOptionsMenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);

                SetMenuItems();
            }

            return sliceOptionsMenuDropList;
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveSliceSettings.Instance.CommitStatusChanged.RegisterEvent(onCommitStatusChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void onCommitStatusChanged(object sender, EventArgs e)
        {
            SetStatusDisplay();
        }


        void SetStatusDisplay()
        {            
            string settingsLayerDescription;
            if (ActivePrinterProfile.Instance.ActivePrinter == null)
            {
                settingsLayerDescription = "Default Settings";
            }
            else
            {
                settingsLayerDescription = ActivePrinterProfile.Instance.ActivePrinter.Name;
            }
            settingsStatusDescription.Text = string.Format("{0}", settingsLayerDescription);
            
            if (ActiveSliceSettings.Instance.HasUncommittedChanges)
            {   
                this.saveButton.Visible = true;
                this.revertbutton.Visible = true;                
                unsavedChangesIndicator.Visible = true;
            }
            else
            {
                this.saveButton.Visible = false;
                this.revertbutton.Visible = false;                
                unsavedChangesIndicator.Visible = false;
            }         
        }
			
        void saveButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            ActiveSliceSettings.Instance.CommitChanges();
        }

        void revertbutton_Click(object sender, MouseEventArgs mouseEvent)
        {
            ActiveSliceSettings.Instance.LoadAllSettings();
            ApplicationController.Instance.ReloadAdvancedControlsPanel();
        }

        void MenuDropList_SelectionChanged(object sender, EventArgs e)
        {
            string menuSelection = ((DropDownMenu)sender).SelectedValue;
            foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
            {
                // if the menu we selecti is this one
                if (item.Item1 == menuSelection)
                {
                    // call its function
                    item.Item2();
                }
            }
        }

        void SetMenuItems()
        {
            string importTxt = LocalizedString.Get("Import");
            string importTxtFull = string.Format("{0}", importTxt);
            string exportTxt = LocalizedString.Get("Export");
            string exportTxtFull = string.Format("{0}", exportTxt);
            //Set the name and callback function of the menu items
            slicerOptionsMenuItems = new TupleList<string, Func<bool>> 
            {
				{importTxtFull, ImportQueueMenu_Click},
				{exportTxtFull, ExportQueueMenu_Click},
            };

            //Add the menu items to the menu itself
            foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
            {
                sliceOptionsMenuDropList.AddItem(item.Item1);
            }
        }

        bool ImportQueueMenu_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                ActiveSliceSettings.Instance.LoadSettingsFromIni(state);
            });
            return true;
        }

        bool ExportQueueMenu_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                ActiveSliceSettings.Instance.SaveAs();
            });
            return true;
        }
    }
}
