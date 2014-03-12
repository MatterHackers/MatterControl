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

﻿using System;
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
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.ToolsPage;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class MainSlidePanel : GuiWidget
    {
        SimpleTextTabWidget aboutTabView;
        static MainSlidePanel globalInstance;
        TabControl advancedControlsTabControl;
        TabControl mainControlsTabControl;
        SliceSettingsWidget sliceSettingsWidget;
        TabControl advancedControls;
        private delegate void ReloadPanel();
        event EventHandler unregisterEvents;
        public RootedObjectEventHandler ReloadPanelTrigger = new RootedObjectEventHandler();
        public RootedObjectEventHandler SetUpdateNotificationTrigger = new RootedObjectEventHandler();
        

        public MainSlidePanel()
        {
            
        }

        public void AddElements()
        {
            //this.AddChild(new MainSlide());
            this.AddChild(new WidescreenPanel());
            this.AnchorAll();
            SetUpdateNotification(this, null);
        }

        public static MainSlidePanel Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new MainSlidePanel();
                    globalInstance.AddElements();
                }
                return globalInstance;
            }
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


        public void SetUpdateNotification(object sender, EventArgs widgetEvent)
        {
            SetUpdateNotificationTrigger.CallEvents(this, null);
        }

        public void ReloadBackPanel()
        {
            ReloadPanelTrigger.CallEvents(this, null);
        }

        void OnReloadBackPanel(EventArgs e)
        {
            ReloadPanelTrigger.CallEvents(this, e);
        }
    }

    public class MainSlide : SlidePanel
    {
        SimpleTextTabWidget aboutTabView;        
        TabControl advancedControlsTabControl;
        TabControl mainControlsTabControl;
        SliceSettingsWidget sliceSettingsWidget;
        TabControl advancedControls;
        private delegate void ReloadPanel();
        public TabPage QueueTabPage;
        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        static MainSlide globalInstance;
        public EventHandler AdvancedControlsLoaded;

        GuiWidget LeftPanel
        {
            get { return GetPannel(0); }
        }

        GuiWidget RightPanel
        {
            get { return GetPannel(1); }
        }

        public MainSlide()
            : base(2)
        {
            AddElements();
        }

        public void AddElements()
        {
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(LoadSettingsOnPrinterChanged, ref unregisterEvents);

            // do the front panel stuff
            {
                // first add the print progress bar
                this.LeftPanel.AddChild(new PrintProgressBar());

                // construct the main controls tab control
                mainControlsTabControl = new TabControl();
                mainControlsTabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
                mainControlsTabControl.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
                mainControlsTabControl.TabBar.Margin = new BorderDouble(0, 0);
                mainControlsTabControl.TabBar.Padding = new BorderDouble(0, 2);

                QueueTabPage = new TabPage(new QueueControlsWidget(), "Queue");
                NumQueueItemsChanged(this, null);

                mainControlsTabControl.AddTab(new SimpleTextTabWidget(QueueTabPage, 18,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

                //mainControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(new GuiWidget(), "History"), 18,
                //        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

                string libraryTabLabel = LocalizedString.Get("Library");

                mainControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(new PrintLibraryWidget(), libraryTabLabel), 18,
                    ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

                //mainControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(new ToolsWidget(), "Tools"), 18,
                //ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

                AboutTabPage = new TabPage(new AboutPage(), LocalizedString.Get("About"));
                aboutTabView = new SimpleTextTabWidget(AboutTabPage, 18,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());
                mainControlsTabControl.AddTab(aboutTabView);


                advancedControlsButtonFactory.normalTextColor = RGBA_Bytes.White;
                advancedControlsButtonFactory.hoverTextColor = RGBA_Bytes.White;
                advancedControlsButtonFactory.pressedTextColor = RGBA_Bytes.White;
                advancedControlsButtonFactory.fontSize = 10;

                advancedControlsButtonFactory.disabledTextColor = RGBA_Bytes.LightGray;
                advancedControlsButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
                advancedControlsButtonFactory.disabledBorderColor = ActiveTheme.Instance.PrimaryBackgroundColor;

                advancedControlsButtonFactory.invertImageLocation = true;
                Button advancedControlsLinkButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Advanced\nControls"), "icon_arrow_right_32x32.png");
                advancedControlsLinkButton.Margin = new BorderDouble(right: 3);
                advancedControlsLinkButton.VAnchor = VAnchor.ParentBottom;
                advancedControlsLinkButton.Cursor = Cursors.Hand;
                advancedControlsLinkButton.Click += new ButtonBase.ButtonEventHandler(AdvancedControlsButton_Click);
                advancedControlsLinkButton.MouseEnterBounds += new EventHandler(onMouseEnterBoundsAdvancedControlsLink);
                advancedControlsLinkButton.MouseLeaveBounds += new EventHandler(onMouseLeaveBoundsAdvancedControlsLink);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                mainControlsTabControl.TabBar.AddChild(hSpacer);
                mainControlsTabControl.TabBar.AddChild(advancedControlsLinkButton);
                // and add it
                this.LeftPanel.AddChild(mainControlsTabControl);

                
            }

            // do the back panel
            {
                advancedControlsTabControl = CreateNewAdvancedControlsTab(new SliceSettingsWidget.UiState());
                this.RightPanel.AddChild(advancedControlsTabControl);
                this.RightPanel.AddChild(new PrintProgressBar());
            }
            AddHandlers();
        }

        void AdvancedControlsButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (this.PannelIndex == 0)
            {
                this.PannelIndex = 1;
            }
            else
            {
                this.PannelIndex = 0;
            }
        }

        void onMouseEnterBoundsAdvancedControlsLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.ShowHoverText(LocalizedString.Get("View Manual Printer Controls and Slicing Settings"));
        }

        void onMouseLeaveBoundsAdvancedControlsLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.HideHoverText();
        }

        void onMouseEnterBoundsPrintQueueLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.ShowHoverText(LocalizedString.Get("View Queue and Library"));
        }

        void onMouseLeaveBoundsPrintQueueLink(Object sender, EventArgs e)
        {
            HelpTextWidget.Instance.HideHoverText();
        }

        public static MainSlide Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new MainSlide();
                }
                return globalInstance;
            }
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

        void OnAdvancedControlsLoaded()
        {
            if (AdvancedControlsLoaded != null)
            {
                AdvancedControlsLoaded(this, null);
            }
        }

        SliceSettingsWidget.UiState sliceSettingsUiState;
        void DoChangePanel(object state)
        {
            // remember which tab we were on
            int topTabIndex = this.advancedControlsTabControl.SelectedTabIndex;

            // remove the advance control and replace it with new ones build for the selected printer
            int advancedControlsWidgetIndex = RightPanel.GetChildIndex(this.advancedControlsTabControl);
            RightPanel.RemoveChild(advancedControlsWidgetIndex);

            this.advancedControlsTabControl = CreateNewAdvancedControlsTab(sliceSettingsUiState);            

            RightPanel.AddChild(this.advancedControlsTabControl, advancedControlsWidgetIndex);

            // set the selected tab back to the one it was before we replace the control
            this.advancedControlsTabControl.SelectTab(topTabIndex);

            // This is a hack to make the panel remain on the screen.  It would be great to debug it and understand
            // why it does not work without this code in here.
            RectangleDouble localBounds = this.LocalBounds;
            this.LocalBounds = new RectangleDouble(0, 0, this.LocalBounds.Width - 1, 10);
            this.LocalBounds = localBounds;
            OnAdvancedControlsLoaded();
            
        }

        TabControl CreateNewAdvancedControlsTab(SliceSettingsWidget.UiState sliceSettingsUiState)
        {
            advancedControls = new TabControl();
            advancedControls.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            advancedControls.TabBar.BorderColor = RGBA_Bytes.White;
            advancedControls.TabBar.Margin = new BorderDouble(0, 0);
            advancedControls.TabBar.Padding = new BorderDouble(0, 2);

            advancedControlsButtonFactory.invertImageLocation = false;
            Button advancedControlsLinkButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Print\nQueue"), "icon_arrow_left_32x32.png");
            advancedControlsLinkButton.Margin = new BorderDouble(right: 3);
            advancedControlsLinkButton.VAnchor = VAnchor.ParentBottom;
            advancedControlsLinkButton.Cursor = Cursors.Hand;
            advancedControlsLinkButton.Click += new ButtonBase.ButtonEventHandler(AdvancedControlsButton_Click);
            advancedControlsLinkButton.MouseEnterBounds += new EventHandler(onMouseEnterBoundsPrintQueueLink);
            advancedControlsLinkButton.MouseLeaveBounds += new EventHandler(onMouseLeaveBoundsPrintQueueLink);

            advancedControls.TabBar.AddChild(advancedControlsLinkButton);

            GuiWidget manualPrinterControls = new ManualPrinterControls();
            ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
            manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            manualPrinterControlsScrollArea.AnchorAll();
            manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

            //Add the tab contents for 'Advanced Controls'
            string printerControlsLabel = LocalizedString.Get("Controls");
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(manualPrinterControlsScrollArea, printerControlsLabel), 18,
            ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string sliceSettingsLabel = LocalizedString.Get("Slice Settings");
            sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(sliceSettingsWidget, sliceSettingsLabel), 18,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string configurationLabel = LocalizedString.Get("Configuration");
            ScrollableWidget configurationControls = new ConfigurationPage();
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(configurationControls, configurationLabel), 18,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));


            
            return advancedControls;
        }

        bool UpdateIsAvailable()
        {
            string currentBuildToken = ApplicationSettings.Instance.get("CurrentBuildToken");
            string applicationBuildToken = VersionInfo.Instance.BuildToken;

            if (applicationBuildToken == currentBuildToken || currentBuildToken == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            PrintQueue.PrintQueueControl.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            PrintQueue.PrintQueueControl.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            MainSlidePanel.Instance.SetUpdateNotificationTrigger.RegisterEvent(OnSetUpdateNotification, ref unregisterEvents);
            MainSlidePanel.Instance.ReloadPanelTrigger.RegisterEvent(ReloadBackPanel, ref unregisterEvents);
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
                //graphics2D.DrawString("1", Width / 2, Height / 2 + 1, 8, Justification.Center, Baseline.BoundsCenter, RGBA_Bytes.White);
                base.OnDraw(graphics2D);
            }
        }

        GuiWidget addedUpdateMark = null;
        public void OnSetUpdateNotification(object sender, EventArgs widgetEvent)
        {
            if (this.UpdateIsAvailable() || UpdateControl.NeedToCheckForUpdateFirstTimeEver)
            {
#if true
                if (addedUpdateMark == null)
                {
                    UpdateControl.NeedToCheckForUpdateFirstTimeEver = false;
                    addedUpdateMark = new NotificationWidget();
                    addedUpdateMark.OriginRelativeParent = new Vector2(63, 10);
                    aboutTabView.AddChild(addedUpdateMark);
                }
#endif
            }
            else
            {
                if (addedUpdateMark != null)
                {
                    addedUpdateMark.Visible = false;
                }
                AboutTabPage.Text = string.Format("About");
            }
        }

        void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
        {
            string queueStringBeg = LocalizedString.Get("Queue");
            string queueString = string.Format("{1} ({0})", PrintQueue.PrintQueueControl.Instance.Count, queueStringBeg);
            QueueTabPage.Text = string.Format(queueString, PrintQueue.PrintQueueControl.Instance.Count);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            this.advancedControls.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
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
            MainSlidePanel.Instance.ReloadBackPanel();
        }
    }
}
