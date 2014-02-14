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

namespace MatterHackers.MatterControl
{
    public class SettingsControlBar : FlowLayoutWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextWidget settingsStatusDescription;
        TextWidget unsavedChangesIndicator;
        Button saveButton;
        Button revertbutton;

        public DropDownMenu SliceOptionsMenuDropList;
        private TupleList<string, Func<bool>> slicerOptionsMenuItems;

        public StyledDropDownList EngineMenuDropList;
        private TupleList<string, Func<bool>> engineOptionsMenuItems = new TupleList<string,Func<bool>>();
        
        public SettingsControlBar()
            : base(FlowDirection.RightToLeft)
        {
            SetDisplayAttributes();
            AddChildElements();
            AddHandlers();
        }        
            
        void SetDisplayAttributes()
        {            
            this.HAnchor |= HAnchor.ParentLeftRight;
            this.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            this.Padding = new BorderDouble(3,3,3,6);
            this.FlowDirection = FlowDirection.LeftToRight;
            this.Height = 50;
        }

        void AddChildElements()
        {
            FlowLayoutWidget settingsStatusLabelContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            settingsStatusLabelContainer.VAnchor |= VAnchor.ParentTop;
            settingsStatusLabelContainer.Margin = new BorderDouble(0);
            {
				string activeSettingsLabelText = new LocalizedString ("Active Settings").Translated;
				string activeSettingsLabelTextFull = string.Format ("{0}:", activeSettingsLabelText);


				TextWidget settingsStatusLabel = new TextWidget(string.Format(activeSettingsLabelTextFull), pointSize: 10);
                settingsStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                settingsStatusDescription = new TextWidget("", pointSize: 14);
                settingsStatusDescription.Margin = new BorderDouble(top: 4);
                settingsStatusDescription.AutoExpandBoundsToText = true;
                settingsStatusDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;

				string unsavedChangesTxtBeg = new  LocalizedString("unsaved changes").Translated;
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

			saveButton = textImageButtonFactory.Generate(new LocalizedString("Save").Translated);
            saveButton.VAnchor = VAnchor.ParentTop;
            saveButton.Visible = false;
            saveButton.Margin = new BorderDouble(0, 0, 0, 10);
            saveButton.Click += new ButtonBase.ButtonEventHandler(saveButton_Click);

			revertbutton = textImageButtonFactory.Generate(new LocalizedString("Revert").Translated);
            revertbutton.VAnchor = VAnchor.ParentTop;
            revertbutton.Visible = false;
            revertbutton.Margin = new BorderDouble(0,0,0,10);
            revertbutton.Click += new ButtonBase.ButtonEventHandler(revertbutton_Click);

			SliceOptionsMenuDropList = new DropDownMenu(new LocalizedString("Options   ").Translated);
            SliceOptionsMenuDropList.Margin = new BorderDouble(top: 11);
            SliceOptionsMenuDropList.VAnchor |= VAnchor.ParentTop;
            SliceOptionsMenuDropList.HoverColor = new RGBA_Bytes(0, 0, 0, 50);
            SliceOptionsMenuDropList.NormalColor = new RGBA_Bytes(0, 0, 0, 0);
            SliceOptionsMenuDropList.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
            SliceOptionsMenuDropList.BackgroundColor = new RGBA_Bytes(0, 0, 0, 0);
            this.SliceOptionsMenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);

            SetMenuItems();

            FlowLayoutWidget sliceEngineContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            sliceEngineContainer.Margin = new BorderDouble(0,0,10,0);
            sliceEngineContainer.VAnchor |= VAnchor.ParentTop;
            {
				string sliceEngineLabelText = new LocalizedString ("Slice Engine").Translated;
				string sliceEngineLabelTextFull = string.Format ("{0}:", sliceEngineLabelText);
				TextWidget sliceEngineLabel = new TextWidget(string.Format(sliceEngineLabelTextFull), pointSize: 10);
                sliceEngineLabel.Margin = new BorderDouble(0);
                sliceEngineLabel.HAnchor = HAnchor.ParentLeft;
                sliceEngineLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                
                EngineMenuDropList = CreateSliceEngineDropdown();                

                sliceEngineContainer.AddChild(sliceEngineLabel);
                sliceEngineContainer.AddChild(EngineMenuDropList);
            }

            this.AddChild(sliceEngineContainer);
            this.AddChild(settingsStatusLabelContainer);

            GuiWidget spacer = new GuiWidget(HAnchor.ParentLeftRight);
            this.AddChild(spacer);

            this.AddChild(saveButton);
            this.AddChild(revertbutton);
            this.AddChild(SliceOptionsMenuDropList);

            SetStatusDisplay();
        }

        StyledDropDownList CreateSliceEngineDropdown()
        {
            StyledDropDownList engineMenuDropList = new StyledDropDownList("Engine   ");
            engineMenuDropList.Margin = new BorderDouble(top: 3, left:0);
            {
                MenuItem slic3rMenuItem = engineMenuDropList.AddItem(PrinterCommunication.SlicingEngine.Slic3r.ToString());
                slic3rMenuItem.Selected += (sender, e) =>
                {
                    PrinterCommunication.Instance.ActiveSliceEngine = PrinterCommunication.SlicingEngine.Slic3r;
                    MainSlidePanel.Instance.ReloadBackPanel();
                };

                MenuItem curaEnginMenuItem = engineMenuDropList.AddItem(PrinterCommunication.SlicingEngine.CuraEngine.ToString());
                curaEnginMenuItem.Selected += (sender, e) =>
                {
                    PrinterCommunication.Instance.ActiveSliceEngine = PrinterCommunication.SlicingEngine.CuraEngine;
                    MainSlidePanel.Instance.ReloadBackPanel();
                };

#if false
                MenuItem matterSliceMenuItem = engineMenuDropList.AddItem(PrinterCommunication.SlicingEngine.MatterSlice.ToString());
                matterSliceMenuItem.Selected += (sender, e) =>
                {
                    PrinterCommunication.Instance.ActiveSliceEngine = PrinterCommunication.SlicingEngine.MatterSlice;
                    MainSlidePanel.Instance.ReloadBackPanel();
                };
#endif

                engineMenuDropList.SelectedValue = PrinterCommunication.Instance.ActiveSliceEngine.ToString();
            }
            engineMenuDropList.MinimumSize = new Vector2(engineMenuDropList.LocalBounds.Width, engineMenuDropList.LocalBounds.Height);
            return engineMenuDropList;
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

        void EngineMenuDropList_SelectionChanged(object sender, EventArgs e)
        {
            string menuSelection = ((DropDownMenu)sender).SelectedValue;
            foreach (Tuple<string, Func<bool>> item in engineOptionsMenuItems)
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
			string importTxt = new LocalizedString ("Import").Translated;
			string importTxtFull = string.Format ("{0}", importTxt);
			string exportTxt = new LocalizedString("Export").Translated;
			string exportTxtFull = string.Format ("{0}", exportTxt);
            //Set the name and callback function of the menu items
            slicerOptionsMenuItems = new TupleList<string, Func<bool>> 
            {
				{importTxtFull, ImportQueueMenu_Click},
				{exportTxtFull, ExportQueueMenu_Click},
            };

            //Add the menu items to the menu itself
            foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
            {
                SliceOptionsMenuDropList.AddItem(item.Item1);
            }
        }

        bool ImportQueueMenu_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                bool goodLoad = ActiveSliceSettings.Instance.LoadSettingsFromIni();
                if (goodLoad)
                {
                    MainSlidePanel.Instance.ReloadBackPanel();
                }
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
            ActiveSliceSettings.Instance.LoadSettingsForPrinter();
            MainSlidePanel.Instance.ReloadBackPanel();
        }
    }
}
