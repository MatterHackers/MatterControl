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

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl
{   
    public class WidescreenPanel : FlowLayoutWidget
    {        
        TabControl advancedControlsTabControl;
        SliceSettingsWidget sliceSettingsWidget;
        TabControl advancedControls;
        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        SliceSettingsWidget.UiState sliceSettingsUiState;

        FlowLayoutWidget ColumnOne;
        FlowLayoutWidget ColumnTwo;
        int ColumnTwoMinWidth = 1390;
        FlowLayoutWidget ColumnThree;
        int ColumnThreeMinWidth = 990;

        View3DTransformPart part3DView;
        GcodeViewBasic partGcodeView;

        ClickWidget RightBorderLine;
        ClickWidget LeftBorderLine;

        bool ColTwoIsHidden = false;
        bool ColThreeIsHidden = false;

        public WidescreenPanel()
            : base(FlowDirection.LeftToRight)
        {
            Name = "WidescreenPanel";
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(LoadSettingsOnPrinterChanged, ref unregisterEvents);
            {
                //PrintQueueControl.Instance.Initialize();
                BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
                Padding = new BorderDouble(4);

                ColumnOne = new FlowLayoutWidget(FlowDirection.TopToBottom);
                ColumnTwo = new FlowLayoutWidget(FlowDirection.TopToBottom);
                ColumnThree = new FlowLayoutWidget(FlowDirection.TopToBottom);


                
                //ColumnOne.Padding = new BorderDouble(4);
                //ColumnTwo.Padding = new BorderDouble(4, 0);
                //ColumnThree.Padding = new BorderDouble(4);

                LeftBorderLine = CreateBorderLine();
                RightBorderLine = CreateBorderLine();
                
                LoadColumnTwo();                
                
                ColumnThree.VAnchor = VAnchor.ParentBottomTop;
                
                LoadColumnThree();

                AddChild(ColumnOne);
                AddChild(LeftBorderLine);
                AddChild(ColumnTwo);
                AddChild(RightBorderLine);
                AddChild(ColumnThree);                
            }

            AnchorAll();
            AddHandlers();
            SetVisibleStatus();
            
        }

        private static ClickWidget CreateBorderLine()
        {
            ClickWidget topLine = new ClickWidget(3, 1);
            topLine.BackgroundColor = new RGBA_Bytes(200,200,200);
            topLine.VAnchor = VAnchor.ParentBottomTop;
            topLine.Margin = new BorderDouble(8, 0);
            topLine.Cursor = Cursors.Hand;
            return topLine;
        }

        void onBoundsChanges(Object sender, EventArgs e)
        {
            SetVisibleStatus();
        }

        void onMouseEnterBoundsAdvancedControlsLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.ShowHoverText("View Manual Printer Controls and Slicing Settings");
        }

        void onMouseLeaveBoundsAdvancedControlsLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.HideHoverText();
        }

        void onMouseEnterBoundsPrintQueueLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.ShowHoverText("View Queue and Library");
        }

        void onMouseLeaveBoundsPrintQueueLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.HideHoverText();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void DoNotChangePanel()
        {
            //Empty function used as placeholder
        }

        void DoChangePanel(object state)
        {
            // remember which tab we were on
            int topTabIndex = this.advancedControlsTabControl.SelectedTabIndex;

            // remove the advance control and replace it with new ones build for the selected printer
            int advancedControlsWidgetIndex = ColumnThree.GetChildIndex(this.advancedControlsTabControl);
            ColumnThree.RemoveChild(advancedControlsWidgetIndex);
            this.advancedControlsTabControl = CreateNewAdvancedControlsTab(sliceSettingsUiState);
            ColumnThree.AddChild(this.advancedControlsTabControl, advancedControlsWidgetIndex);
            ColumnThree.Width = 590;

            // set the selected tab back to the one it was before we replace the control
            this.advancedControlsTabControl.SelectTab(topTabIndex);
        }

        TabControl CreateNewAdvancedControlsTab(SliceSettingsWidget.UiState sliceSettingsUiState)
        {
            advancedControls = new TabControl();
            advancedControls.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            advancedControls.TabBar.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
            advancedControls.TabBar.Margin = new BorderDouble(0, 0);
            advancedControls.TabBar.Padding = new BorderDouble(0, 2);

            advancedControlsButtonFactory.invertImageLocation = false;

            GuiWidget manualPrinterControls = new ManualPrinterControls();
            ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
            manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            manualPrinterControlsScrollArea.AnchorAll();
            manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

            //Add the tab contents for 'Advanced Controls'
            string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(manualPrinterControlsScrollArea, printerControlsLabel), 16,
            ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string sliceSettingsLabel = LocalizedString.Get("Slice Settings").ToUpper();
            sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(sliceSettingsWidget, sliceSettingsLabel), 16,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string configurationLabel = LocalizedString.Get("Configuration").ToUpper();
            ScrollableWidget configurationControls = new ConfigurationPage();
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(configurationControls, configurationLabel), 16,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            return advancedControls;
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
            ApplicationWidget.Instance.ReloadPanelTrigger.RegisterEvent(ReloadBackPanel, ref unregisterEvents);
            this.BoundsChanged += new EventHandler(onBoundsChanges);
            RightBorderLine.Click += new ClickWidget.ButtonEventHandler(onRightBorderClick);
            LeftBorderLine.Click += new ClickWidget.ButtonEventHandler(onLeftBorderClick);
            
        }

        void onRightBorderClick(object sender, EventArgs e)
        {            
            ColThreeIsHidden = ColumnThree.Visible;
            SetVisibleStatus(true);
        }

        void onLeftBorderClick(object sender, EventArgs e)
        {            
            ColTwoIsHidden = ColumnTwo.Visible;
            SetVisibleStatus(true);
        }

        void onActivePrintItemChanged(object sender, EventArgs e)
        {
            LoadColumnTwo();
        }

        void LoadColumnTwo()
        {            
            ColumnTwo.RemoveAllChildren();

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;
            part3DView = new View3DTransformPart(PrinterCommunication.Instance.ActivePrintItem, new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight), ActiveSliceSettings.Instance.BedShape);
            part3DView.Margin = new BorderDouble(bottom: 4);
            part3DView.AnchorAll();

            partGcodeView = new GcodeViewBasic(PrinterCommunication.Instance.ActivePrintItem, ActiveSliceSettings.Instance.GetBedSize, ActiveSliceSettings.Instance.GetBedCenter);
            partGcodeView.AnchorAll();

            ColumnTwo.AddChild(part3DView);
            ColumnTwo.AddChild(partGcodeView);
            ColumnTwo.AnchorAll();
            SetVisibleStatus();
        }

        void LoadColumnZero()
        {
            ColumnOne.RemoveAllChildren();
            ColumnOne.AddChild(new ActionBarPlus());
            ColumnOne.AddChild(new CompactSlidePanel());
            ColumnOne.AnchorAll();
        }

        void LoadColumnOne()
        {
            ColumnOne.RemoveAllChildren();
            ColumnOne.VAnchor = VAnchor.ParentBottomTop;
            ColumnOne.AddChild(new ActionBarPlus());
            ColumnOne.AddChild(new PrintProgressBar());
            ColumnOne.AddChild(new QueueTab());
            ColumnOne.Width = 500; //Ordering here matters - must go after children are added                      
        }

        void LoadColumnThree()
        {
            advancedControlsTabControl = CreateNewAdvancedControlsTab(new SliceSettingsWidget.UiState());
            ColumnThree.AddChild(advancedControlsTabControl);
            ColumnThree.Width = 590; //Ordering here matters - must go after children are added  
            
        }

        int UiState = -1;
        void SetVisibleStatus(bool forceReset = false)
        {            
            if (forceReset)
            {
                UiState = -1;
            }

            if (this.Width < ColumnThreeMinWidth)
            {
                if (UiState != 0)
                {
                    UiState = 0;
                    ApplicationWidget.Instance.WidescreenMode = false;

                    LoadColumnZero();                   

                    ColumnThree.Visible = false;
                    ColumnTwo.Visible = false;
                    ColumnOne.Visible = true;
                    
                    
                    Padding = new BorderDouble(0);

                    LeftBorderLine.Visible = false;
                    RightBorderLine.Visible = false;
                }

            }
            else if (this.Width < ColumnTwoMinWidth && !ColThreeIsHidden)
            {
                if (UiState != 1)
                {
                    UiState = 1;
                    ApplicationWidget.Instance.WidescreenMode = true;

                    LoadColumnOne();
                    
                    ColumnTwo.Visible = !!ColTwoIsHidden;
                    ColumnThree.Visible = !ColThreeIsHidden;
                    ColumnOne.Visible = true;
                    Padding = new BorderDouble(4);

                    LeftBorderLine.Visible = false;
                    RightBorderLine.Visible = true;
                }
            }
            else if (ColTwoIsHidden)
            {
                if (UiState != 2)
                {
                    UiState = 2;
                    ApplicationWidget.Instance.WidescreenMode = true;

                    LoadColumnOne();                    
                    ColumnThree.Visible = !ColThreeIsHidden;
                    ColumnTwo.Visible = !ColTwoIsHidden;
                    ColumnOne.AnchorAll();

                    Padding = new BorderDouble(4);

                    ColumnOne.Visible = true;

                    LeftBorderLine.Visible = true;
                    RightBorderLine.Visible = true;
                }
            }
            else
            {
                if (UiState != 3)
                {
                    UiState = 3;
                    ApplicationWidget.Instance.WidescreenMode = true;

                    LoadColumnOne();
                    
                    ColumnThree.Visible = !ColThreeIsHidden;
                    ColumnTwo.Visible = !ColTwoIsHidden;

                    ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                    ColumnOne.Width = 500;
                    Padding = new BorderDouble(4);

                    ColumnOne.Visible = true;

                    LeftBorderLine.Visible = true;
                    RightBorderLine.Visible = true;
                }
            }
        }


        private void onThemeChanged(object sender, EventArgs e)
        {
            //this.advancedControls.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.advancedControls.Invalidate();
        }

        public void ReloadBackPanel(object sender, EventArgs widgetEvent)
        {
            sliceSettingsUiState = new SliceSettingsWidget.UiState(sliceSettingsWidget);
            UiThread.RunOnIdle(DoChangePanel);
        }

        public void LoadSettingsOnPrinterChanged(object sender, EventArgs e)
        {
            ActiveSliceSettings.Instance.LoadSettingsForPrinter();
            ApplicationWidget.Instance.ReloadBackPanel();
        }
    }

    

    class NotificationWidget : GuiWidget
    {
        public NotificationWidget()
            : base(12, 12)
        {
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            graphics2D.Circle(Width / 2, Height / 2, Width / 2, RGBA_Bytes.White);
            graphics2D.Circle(Width / 2, Height / 2, Width / 2 - 1, RGBA_Bytes.Red);
            graphics2D.FillRectangle(Width / 2 - 1, Height / 2 - 3, Width / 2 + 1, Height / 2 + 3, RGBA_Bytes.White);
            base.OnDraw(graphics2D);
        }
    }
}
