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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl
{
    public class WidescreenPanel : FlowLayoutWidget
    {
        SliceSettingsWidget sliceSettingsWidget;
        TabControl advancedControls;
        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        SliceSettingsWidget.UiState sliceSettingsUiState;

        FlowLayoutWidget ColumnOne;
        FlowLayoutWidget ColumnTwo;
        FlowLayoutWidget ColumnThree;
        int Max1ColumnWidth = 990;
        int Max2ColumnWidth = 1590;

        View3DTransformPart part3DView;
        GcodeViewBasic partGcodeView;

        PanelSeparator RightBorderLine;
        PanelSeparator LeftBorderLine;

        event EventHandler unregisterEvents;

        QueueDataView queueDataView = null;
        
        public WidescreenPanel()
            : base(FlowDirection.LeftToRight)
        {
            Name = "WidescreenPanel";
            AnchorAll();
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            Padding = new BorderDouble(4);

            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(LoadSettingsOnPrinterChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
            ApplicationWidget.Instance.ReloadPanelTrigger.RegisterEvent(ReloadBackPanel, ref unregisterEvents);
            this.BoundsChanged += new EventHandler(onBoundsChanges);
        }

        public override void OnParentChanged(EventArgs e)
        {
            lastNumberVisible = 0;
            RecreateAllPannels();
            base.OnParentChanged(e);
        }

        static int lastNumberVisible;
        void onBoundsChanges(Object sender, EventArgs e)
        {
            if (NumberOfVisiblePannels() != lastNumberVisible)
            {
                RecreateAllPannels();
            }
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

        TabControl CreateNewAdvancedControlsTab(SliceSettingsWidget.UiState sliceSettingsUiState)
        {
            StoreUiState();

            advancedControls = new TabControl();
            advancedControls.AnchorAll();
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

            RestoreUiState();

            return advancedControls;
        }

        void onRightBorderClick(object sender, EventArgs e)
        {
            RightBorderLine.Hidden = !RightBorderLine.Hidden;
            UiThread.RunOnIdle(RecreateAllPannels);
        }

        void onLeftBorderClick(object sender, EventArgs e)
        {
            LeftBorderLine.Hidden = !LeftBorderLine.Hidden;
            UiThread.RunOnIdle(RecreateAllPannels);
        }

        void onActivePrintItemChanged(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(LoadColumnTwo);
        }

        public void StoreUiState()
        {
            if (queueDataView != null && queueDataView.SelectedIndex != -1)
            {
                MainScreenUiState.lastSelectedIndex = queueDataView.SelectedIndex;
            }
            if (advancedControls != null && advancedControls.SelectedTabIndex != -1)
            {
                MainScreenUiState.lastAdvancedControlsTab = advancedControls.SelectedTabIndex;
            }
        }

        void RestoreUiState()
        {
            if (MainScreenUiState.lastSelectedIndex > -1 && queueDataView != null)
            {
                queueDataView.SelectedIndex = MainScreenUiState.lastSelectedIndex;
            }
            if (MainScreenUiState.lastAdvancedControlsTab > -1 && advancedControls != null)
            {
                advancedControls.SelectedTabIndex = MainScreenUiState.lastAdvancedControlsTab;
            }
        }

        void LoadCompactView()
        {
            queueDataView = new QueueDataView();
            
            ColumnOne.RemoveAllChildren();
            ColumnOne.AddChild(new ActionBarPlus(queueDataView));
            ColumnOne.AddChild(new CompactSlidePanel(queueDataView));
            ColumnOne.AnchorAll();
        }

        void LoadColumnOne()
        {
            queueDataView = new QueueDataView();

            ColumnOne.VAnchor = VAnchor.ParentBottomTop;
            ColumnOne.AddChild(new ActionBarPlus(queueDataView));
            ColumnOne.AddChild(new PrintProgressBar());
            ColumnOne.AddChild(new MainScreenTabView(queueDataView));
            ColumnOne.Width = 500; //Ordering here matters - must go after children are added                      
        }

        void LoadColumnTwo(object state = null)
        {
            ColumnTwo.RemoveAllChildren();

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;
            part3DView = new View3DTransformPart(PrinterCommunication.Instance.ActivePrintItem, new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight), ActiveSliceSettings.Instance.BedShape, false);
            part3DView.Margin = new BorderDouble(bottom: 4);
            part3DView.AnchorAll();

            partGcodeView = new GcodeViewBasic(PrinterCommunication.Instance.ActivePrintItem, ActiveSliceSettings.Instance.GetBedSize, ActiveSliceSettings.Instance.GetBedCenter, false);
            partGcodeView.AnchorAll();

            ColumnTwo.AddChild(part3DView);
            ColumnTwo.AddChild(partGcodeView);
            ColumnTwo.AnchorAll();
        }

        void LoadColumnThree()
        {
            ColumnThree.AddChild(CreateNewAdvancedControlsTab(new SliceSettingsWidget.UiState()));
            ColumnThree.Width = 590; //Ordering here matters - must go after children are added  
        }

        int NumberOfVisiblePannels()
        {
            if (this.Width < Max1ColumnWidth)
            {
                return 1;
            }
            else if (this.Width < Max2ColumnWidth)
            {
                return 2;
            }
            else
            {
                return 3;
            }
        }

        void RecreateAllPannels(object state = null)
        {
            if (Width == 0)
            {
                return;
            }
            StoreUiState();
            RemovePannelsAndCreateEmpties();

            int numberOfPannels = NumberOfVisiblePannels();

            switch (numberOfPannels)
            {
                case 1:
                    {
                        ApplicationWidget.Instance.WidescreenMode = false;

                        LoadCompactView();

                        ColumnThree.Visible = false;
                        ColumnTwo.Visible = false;
                        ColumnOne.Visible = true;

                        Padding = new BorderDouble(0);

                        LeftBorderLine.Visible = false;
                        RightBorderLine.Visible = false;
                    }
                    break;

                case 2:
                case 3:
                    {
                        ApplicationWidget.Instance.WidescreenMode = true;

                        LoadColumnOne();
                        LoadColumnTwo();
                        LoadColumnThree();
                    }

                    if (numberOfPannels == 2)
                    {
                        if (this.Width < Max2ColumnWidth && !RightBorderLine.Hidden)
                        {
                            //Queue column and advanced controls columns show

                            ColumnTwo.Visible = LeftBorderLine.Hidden;
                            ColumnThree.Visible = !RightBorderLine.Hidden;
                            ColumnOne.Visible = true;
                            ColumnOne.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                            Padding = new BorderDouble(4);

                            LeftBorderLine.Visible = false;
                            RightBorderLine.Visible = true;
                        }
                        else if (LeftBorderLine.Hidden)
                        {
                            //Queue column and preview column shown                
                            ApplicationWidget.Instance.WidescreenMode = true;

                            ColumnThree.Visible = !RightBorderLine.Hidden;
                            ColumnTwo.Visible = !LeftBorderLine.Hidden;
                            ColumnOne.AnchorAll();

                            Padding = new BorderDouble(4);

                            ColumnOne.Visible = true;

                            LeftBorderLine.Visible = true;
                            RightBorderLine.Visible = true;
                        }
                    }
                    else // number of pannels == 3
                    {
                        //All three columns shown
                        ApplicationWidget.Instance.WidescreenMode = true;

                        ColumnThree.Visible = !RightBorderLine.Hidden;
                        ColumnTwo.Visible = !LeftBorderLine.Hidden;

                        ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                        ColumnOne.Width = 500;
                        Padding = new BorderDouble(4);

                        ColumnOne.Visible = true;

                        LeftBorderLine.Visible = true;
                        RightBorderLine.Visible = true;
                    }
                    break;
            }

            RestoreUiState();
            lastNumberVisible = numberOfPannels;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
        }

        private void RemovePannelsAndCreateEmpties()
        {
            RemoveAllChildren();

            ColumnOne = new FlowLayoutWidget(FlowDirection.TopToBottom);
            ColumnTwo = new FlowLayoutWidget(FlowDirection.TopToBottom);
            ColumnThree = new FlowLayoutWidget(FlowDirection.TopToBottom);
            ColumnThree.VAnchor = VAnchor.ParentBottomTop;

            LeftBorderLine = new PanelSeparator();
            RightBorderLine = new PanelSeparator();

            AddChild(ColumnOne);
            AddChild(LeftBorderLine);
            AddChild(ColumnTwo);
            AddChild(RightBorderLine);
            AddChild(ColumnThree);

            RightBorderLine.Click += new ClickWidget.ButtonEventHandler(onRightBorderClick);
            LeftBorderLine.Click += new ClickWidget.ButtonEventHandler(onLeftBorderClick);
        }

        public void ReloadBackPanel(object sender, EventArgs widgetEvent)
        {
            lastNumberVisible = 0;
            sliceSettingsUiState = new SliceSettingsWidget.UiState(sliceSettingsWidget);
            UiThread.RunOnIdle(RecreateAllPannels);
        }

        public void LoadSettingsOnPrinterChanged(object sender, EventArgs e)
        {
            ActiveSliceSettings.Instance.LoadAllSettings();
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
