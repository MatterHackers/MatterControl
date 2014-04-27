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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class CompactSlidePanel : SlidePanel
    {
        TabControl advancedControlsTabControl;
        TabControl mainControlsTabControl;
        SliceSettingsWidget sliceSettingsWidget;
        private delegate void ReloadPanel();
        public TabPage QueueTabPage;
        public TabPage AboutTabPage;
        TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        public EventHandler AdvancedControlsLoaded;

        QueueDataView queueDataView;

        GuiWidget LeftPanel
        {
            get { return GetPannel(0); }
        }

        GuiWidget RightPanel
        {
            get { return GetPannel(1); }
        }

        static int lastPannelIndexOnClose = 0;
        static int lastAdvanceControlsIndex = 0;
        public CompactSlidePanel(QueueDataView queueDataView, SliceSettingsWidget.UiState sliceSettingsUiState)
            : base(2)
        {
            this.queueDataView = queueDataView;

            // do the front panel stuff
            {
                // first add the print progress bar
                this.LeftPanel.AddChild(new PrintProgressBar());

                // construct the main controls tab control
                mainControlsTabControl = new MainScreenTabView(queueDataView);

                advancedControlsButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
                advancedControlsButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
                advancedControlsButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
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

                this.LeftPanel.AddChild(mainControlsTabControl);
            }

            // do the back panel
            {
                CreateNewAdvancedControlsTab(sliceSettingsUiState);
                
                this.RightPanel.AddChild(new PrintProgressBar());
                this.RightPanel.AddChild(advancedControlsTabControl);
                
            }
            AddHandlers();

            SetPannelIndexImediate(lastPannelIndexOnClose);
            advancedControlsTabControl.SelectedTabIndex = lastAdvanceControlsIndex;
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

        public override void OnClosed(EventArgs e)
        {
            lastPannelIndexOnClose = PannelIndex;
            lastAdvanceControlsIndex = advancedControlsTabControl.SelectedTabIndex;
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

            CreateNewAdvancedControlsTab(sliceSettingsUiState);

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

        void CreateNewAdvancedControlsTab(SliceSettingsWidget.UiState sliceSettingsUiState)
        {
            advancedControlsTabControl = new TabControl();
            advancedControlsTabControl.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            advancedControlsTabControl.TabBar.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
            advancedControlsTabControl.TabBar.Margin = new BorderDouble(0, 0);
            advancedControlsTabControl.TabBar.Padding = new BorderDouble(0, 2);

            advancedControlsButtonFactory.invertImageLocation = false;
            Button advancedControlsLinkButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Print\nQueue"), "icon_arrow_left_32x32.png");
            advancedControlsLinkButton.Margin = new BorderDouble(right: 3);
            advancedControlsLinkButton.VAnchor = VAnchor.ParentBottom;
            advancedControlsLinkButton.Cursor = Cursors.Hand;
            advancedControlsLinkButton.Click += new ButtonBase.ButtonEventHandler(AdvancedControlsButton_Click);
            advancedControlsLinkButton.MouseEnterBounds += new EventHandler(onMouseEnterBoundsPrintQueueLink);
            advancedControlsLinkButton.MouseLeaveBounds += new EventHandler(onMouseLeaveBoundsPrintQueueLink);

            advancedControlsTabControl.TabBar.AddChild(advancedControlsLinkButton);

            GuiWidget manualPrinterControls = new ManualPrinterControls();
            ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
            manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            manualPrinterControlsScrollArea.AnchorAll();
            manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

            //Add the tab contents for 'Advanced Controls'
            string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
            advancedControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(manualPrinterControlsScrollArea, printerControlsLabel), 14,
            ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string sliceSettingsLabel = LocalizedString.Get("Slice Settings").ToUpper();
            sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
            advancedControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(sliceSettingsWidget, sliceSettingsLabel), 14,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string configurationLabel = LocalizedString.Get("Configuration").ToUpper();
            ScrollableWidget configurationControls = new ConfigurationPage();
            advancedControlsTabControl.AddTab(new SimpleTextTabWidget(new TabPage(configurationControls, configurationLabel), 14,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
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
            ApplicationWidget.Instance.ReloadPanelTrigger.RegisterEvent(ReloadBackPanel, ref unregisterEvents);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            this.advancedControlsTabControl.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.advancedControlsTabControl.Invalidate();
        }

        public void ReloadBackPanel(object sender, EventArgs widgetEvent)
        {
            sliceSettingsUiState = new SliceSettingsWidget.UiState(sliceSettingsWidget);
            UiThread.RunOnIdle(DoChangePanel);
        }
    }
}
