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
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
    public class WidescreenPanel : FlowLayoutWidget
    {
        static readonly int ColumnOneFixedWidth = 500;
        static readonly int ColumnTheeFixedWidth = 590;
        static bool leftBorderLineHiden;
        static bool rightBorderLineHiden;
        static int lastNumberOfVisiblePanels;

        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

        FlowLayoutWidget ColumnOne;
        FlowLayoutWidget ColumnTwo;
        FlowLayoutWidget ColumnThree;
        double Force1PanelWidth = 990 * TextWidget.GlobalPointSizeScaleRatio;
        double Force2PanelWidth = 1590 * TextWidget.GlobalPointSizeScaleRatio;

        View3DWidget part3DView;
        ViewGcodeBasic partGcodeView;

        PanelSeparator RightBorderLine;
        PanelSeparator LeftBorderLine;

        event EventHandler unregisterEvents;

        public static RootedObjectEventHandler PreChangePanels = new RootedObjectEventHandler();

        QueueDataView queueDataView = null;
        
        public WidescreenPanel()
            : base(FlowDirection.LeftToRight)
        {
            Name = "WidescreenPanel";
            AnchorAll();
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            Padding = new BorderDouble(4);

            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(LoadSettingsOnPrinterChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
            ApplicationController.Instance.ReloadAdvancedControlsPanelTrigger.RegisterEvent(ReloadAdvancedControlsPanelTrigger, ref unregisterEvents);
            this.BoundsChanged += new EventHandler(onBoundsChanges);
        }

        public void ReloadAdvancedControlsPanelTrigger(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(ReloadAdvancedControlsPanel);
        }

        public override void OnParentChanged(EventArgs e)
        {
            lastNumberOfVisiblePanels = 0;
            RecreateAllPanels();
            base.OnParentChanged(e);
        }

        void onBoundsChanges(Object sender, EventArgs e)
        {
            if (NumberOfVisiblePanels() != lastNumberOfVisiblePanels)
            {
                RecreateAllPanels();
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

        void onRightBorderClick(object sender, EventArgs e)
        {
            RightBorderLine.Hidden = !RightBorderLine.Hidden;
            UiThread.RunOnIdle(SetColumnVisibility);
            UiThread.RunOnIdle(RightBorderLine.SetDisplayState);
        }

        void onLeftBorderClick(object sender, EventArgs e)
        {
            LeftBorderLine.Hidden = !LeftBorderLine.Hidden;
            UiThread.RunOnIdle(SetColumnVisibility);
            UiThread.RunOnIdle(LeftBorderLine.SetDisplayState);
        }

        void onActivePrintItemChanged(object sender, EventArgs e)
        {
            if (NumberOfVisiblePanels() > 1)
            {
                UiThread.RunOnIdle(LoadColumnTwo);
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
            ColumnOne.AddChild(new FirstPanelTabView(queueDataView));
            ColumnOne.Width = ColumnOneFixedWidth; //Ordering here matters - must go after children are added
        }

        void LoadColumnTwo(object state = null)
        {
            ColumnTwo.CloseAndRemoveAllChildren();

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

            if (OemSettings.Instance.UseLiteInterface)
            {
                PartPreviewContent partViewContent = new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, true, View3DWidget.AutoRotate.Enabled, false);
                partViewContent.AnchorAll();

                ColumnTwo.AddChild(partViewContent);
            }
            else
            {
                part3DView = new View3DWidget(PrinterConnectionAndCommunication.Instance.ActivePrintItem,
                    new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                    ActiveSliceSettings.Instance.BedCenter,
                    ActiveSliceSettings.Instance.BedShape,
                    View3DWidget.WindowType.Embeded,
                    View3DWidget.AutoRotate.Enabled);
                part3DView.Margin = new BorderDouble(bottom: 4);
                part3DView.AnchorAll();

                partGcodeView = new ViewGcodeBasic(PrinterConnectionAndCommunication.Instance.ActivePrintItem,
                    new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                    ActiveSliceSettings.Instance.BedCenter,
                    ActiveSliceSettings.Instance.BedShape,
                    false);
                partGcodeView.AnchorAll();

                ColumnTwo.AddChild(part3DView);
                ColumnTwo.AddChild(partGcodeView);
            }

            ColumnTwo.AnchorAll();
        }

        static int ColumnThreeCount = 0;
        void LoadColumnThree(object state = null)
        {
            ColumnThree.CloseAndRemoveAllChildren();
            ThirdPanelTabView thirdPanelTabView = new ThirdPanelTabView();
            thirdPanelTabView.Name = "For - WideScreenPanel {0}".FormatWith(ColumnThreeCount++);
            ColumnThree.AddChild(thirdPanelTabView);
            ColumnThree.Width = ColumnTheeFixedWidth; //Ordering here matters - must go after children are added  
        }

        int NumberOfVisiblePanels()
        {
            if (this.Width < Force1PanelWidth)
            {
                return 1;
            }
            else if (this.Width < Force2PanelWidth)
            {
                return 2;
            }
            else
            {
                if (OemSettings.Instance.UseLiteInterface)
                {
                    return 2;
                }
                return 3;
            }
        }

        public void RecreateAllPanels(object state = null)
        {
            if (Width == 0)
            {
                return;
            }

            int numberOfPanels = NumberOfVisiblePanels();

            if (LeftBorderLine != null)
            {
                leftBorderLineHiden = LeftBorderLine.Hidden;
                rightBorderLineHiden = RightBorderLine.Hidden;
            }
            PreChangePanels.CallEvents(this, null);
            RemovePanelsAndCreateEmpties();

            switch (numberOfPanels)
            {
                case 1:
                    ApplicationController.Instance.WidescreenMode = false;
                    LoadCompactView();
                    break;

                case 2:
                    if (OemSettings.Instance.UseLiteInterface)
                    {
                        ApplicationController.Instance.WidescreenMode = false;
                        LoadCompactView();
                        LoadColumnTwo();
                        LoadColumnThree();
                    }
                    else
                    {
                        ApplicationController.Instance.WidescreenMode = true;

                        LoadColumnOne();
                        // make sure we restore the state of column one because LoadColumnThree is going to save it.
                        LoadColumnTwo();
                        LoadColumnThree();
                    }
                    break;

                case 3:
                    ApplicationController.Instance.WidescreenMode = true;

                    LoadColumnOne();
                    // make sure we restore the state of column one because LoadColumnThree is going to save it.
                    LoadColumnTwo();
                    LoadColumnThree();
                    break;
            }

            LeftBorderLine.Hidden = leftBorderLineHiden;
            RightBorderLine.Hidden = rightBorderLineHiden;
            SetColumnVisibility();
            RightBorderLine.SetDisplayState();
            LeftBorderLine.SetDisplayState();

            lastNumberOfVisiblePanels = numberOfPanels;
        }

        void SetColumnVisibility(object state = null)
        {
            int numberOfPanels = NumberOfVisiblePanels();

            switch (numberOfPanels)
            {
                case 1:
                    {
                        ColumnThree.Visible = false;
                        ColumnTwo.Visible = false;
                        ColumnOne.Visible = true;

                        Padding = new BorderDouble(0);

                        LeftBorderLine.Visible = false;
                        RightBorderLine.Visible = false;
                    }
                    break;

                case 2:
                    Padding = new BorderDouble(4);
                    ColumnOne.Visible = true;
                    if (OemSettings.Instance.UseLiteInterface)
                    {
                        LeftBorderLine.Visible = true;
                        RightBorderLine.Visible = false;
                        ColumnTwo.Visible = true;
                        ColumnThree.Visible = false;
                        ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                        ColumnOne.Width = ColumnTheeFixedWidth; // it can hold the slice settings so it needs to be bigger.
                    }
                    else
                    {
                        RightBorderLine.Visible = true;
                        if (RightBorderLine.Hidden)
                        {
                            LeftBorderLine.Visible = true;
                            if (LeftBorderLine.Hidden)
                            {
                                ColumnThree.Visible = false;
                                ColumnTwo.Visible = false;
                                ColumnOne.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                            }
                            else
                            {
                                ColumnThree.Visible = false;
                                ColumnTwo.Visible = true;
                                ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                            }
                        }
                        else
                        {
                            LeftBorderLine.Visible = false;
                            ColumnThree.Visible = true;
                            ColumnTwo.Visible = false;
                            ColumnOne.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                        }
                    }
                    break;

                case 3:                    
                    //All three columns shown
                    Padding = new BorderDouble(4);                    

                    //If the middle column is hidden, left/right anchor the left column
                    if (LeftBorderLine.Hidden)
                    {
                        ColumnOne.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                    }
                    else
                    {
                        ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                        ColumnOne.Width = ColumnOneFixedWidth;
                    }

                    ColumnOne.Visible = true;
                    LeftBorderLine.Visible = true;
                    RightBorderLine.Visible = true;
                    ColumnThree.Visible = !RightBorderLine.Hidden;
                    ColumnTwo.Visible = !LeftBorderLine.Hidden;

                    break;
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
        }

        private void RemovePanelsAndCreateEmpties()
        {
            CloseAndRemoveAllChildren();

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

            RightBorderLine.Click += new EventHandler(onRightBorderClick);
            LeftBorderLine.Click += new EventHandler(onLeftBorderClick);
        }

        public void ReloadAdvancedControlsPanel(object state)
        {
            PreChangePanels.CallEvents(this, null);
            if (NumberOfVisiblePanels() > 1)
            {
                UiThread.RunOnIdle(LoadColumnThree);
            }
        }

        public void LoadSettingsOnPrinterChanged(object sender, EventArgs e)
        {
            ActiveSliceSettings.Instance.LoadAllSettings();
            ApplicationController.Instance.ReloadAll(null, null); 
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
