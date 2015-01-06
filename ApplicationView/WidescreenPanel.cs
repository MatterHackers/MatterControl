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
        static readonly int ColumnOneFixedWidth = 590;
        static int lastNumberOfVisiblePanels;

        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

        FlowLayoutWidget ColumnOne;
        FlowLayoutWidget ColumnTwo;
        double Force1PanelWidth = 990 * TextWidget.GlobalPointSizeScaleRatio;
        double Force2PanelWidth = 1590 * TextWidget.GlobalPointSizeScaleRatio;

        GuiWidget leftBorderLine;

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

            UiThread.RunOnIdle((state2) =>
            {
                PartPreviewContent partViewContent = new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, View3DWidget.WindowMode.Embeded, View3DWidget.AutoRotate.Enabled);
                partViewContent.AnchorAll();

                ColumnTwo.AddChild(partViewContent);
            });

            ColumnTwo.AnchorAll();
        }

        int NumberOfVisiblePanels()
        {
            if (this.Width < Force1PanelWidth)
            {
                return 1;
            }

            return 2;
        }

        public void RecreateAllPanels(object state = null)
        {
            if (Width == 0)
            {
                return;
            }

            int numberOfPanels = NumberOfVisiblePanels();

            PreChangePanels.CallEvents(this, null);
            RemovePanelsAndCreateEmpties();

            switch (numberOfPanels)
            {
                case 1:
                    ApplicationController.Instance.WidescreenMode = false;
                    LoadCompactView();
                    break;

                case 2:
                        ApplicationController.Instance.WidescreenMode = false;
                        LoadCompactView();
                        LoadColumnTwo();
                    break;
            }

            SetColumnVisibility();

            lastNumberOfVisiblePanels = numberOfPanels;
        }

        void SetColumnVisibility(object state = null)
        {
            int numberOfPanels = NumberOfVisiblePanels();

            switch (numberOfPanels)
            {
                case 1:
                    {
                        ColumnTwo.Visible = false;
                        ColumnOne.Visible = true;

                        Padding = new BorderDouble(0);

                        leftBorderLine.Visible = false;
                    }
                    break;

                case 2:
                    Padding = new BorderDouble(4);
                    ColumnOne.Visible = true;
                    ColumnTwo.Visible = true;
                    ColumnOne.HAnchor = Agg.UI.HAnchor.None;
                    ColumnOne.Width = ColumnOneFixedWidth; // it can hold the slice settings so it needs to be bigger.
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

            AddChild(ColumnOne);
            leftBorderLine = new GuiWidget(vAnchor: VAnchor.ParentBottomTop);
            leftBorderLine.Width = 15;
            leftBorderLine.DrawBefore += (widget, graphics2D) =>
            {
                RectangleDouble bounds = widget.LocalBounds;
                bounds.Left += 3;
                bounds.Right -= 8;
                graphics2D.graphics2D.FillRectangle(bounds, new RGBA_Bytes(160, 160, 160));
            };
            AddChild(leftBorderLine);
            AddChild(ColumnTwo);
        }

        public void ReloadAdvancedControlsPanel(object state)
        {
            PreChangePanels.CallEvents(this, null);
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
