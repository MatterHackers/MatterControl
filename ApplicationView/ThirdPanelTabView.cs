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
using MatterHackers.MatterControl.PrintHistory;

namespace MatterHackers.MatterControl
{
	public class ThirdPanelTabView : GuiWidget
    {
        event EventHandler unregisterEvents;
        static int lastAdvanceControlsIndex = 0;

        Button advancedControlsLinkButton;
        SliceSettingsWidget sliceSettingsWidget;
        EventHandler AdvancedControlsButton_Click;
        EventHandler onMouseEnterBoundsPrintQueueLink;
        EventHandler onMouseLeaveBoundsPrintQueueLink;

        TabControl advancedControls2;

        public ThirdPanelTabView(EventHandler AdvancedControlsButton_Click = null,
            EventHandler onMouseEnterBoundsPrintQueueLink = null,
            EventHandler onMouseLeaveBoundsPrintQueueLink = null)
        {
            this.AdvancedControlsButton_Click = AdvancedControlsButton_Click;
            this.onMouseEnterBoundsPrintQueueLink = onMouseEnterBoundsPrintQueueLink;
            this.onMouseLeaveBoundsPrintQueueLink = onMouseLeaveBoundsPrintQueueLink;

            advancedControls2 = CreateNewAdvancedControls(AdvancedControlsButton_Click, onMouseEnterBoundsPrintQueueLink, onMouseLeaveBoundsPrintQueueLink);

            AddChild(advancedControls2);

            WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentPanelIndex, ref unregisterEvents);
			ApplicationController.Instance.ReloadAdvancedControlsPanelTrigger.RegisterEvent(ReloadAdvancedControlsPanelTrigger, ref unregisterEvents);

            AnchorAll();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public void ReloadAdvancedControlsPanelTrigger(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(ReloadSliceSettings);
        }

        static SliceSettingsWidgetUiState sliceSettingsUiState = new SliceSettingsWidgetUiState();
        void SaveCurrentPanelIndex(object sender, EventArgs e)
        {
            sliceSettingsUiState = new SliceSettingsWidgetUiState(sliceSettingsWidget);

            if (advancedControls2.Children.Count > 0)
            {
                lastAdvanceControlsIndex = advancedControls2.SelectedTabIndex;
            }
        }

        private TabControl CreateNewAdvancedControls(EventHandler AdvancedControlsButton_Click, EventHandler onMouseEnterBoundsPrintQueueLink, EventHandler onMouseLeaveBoundsPrintQueueLink)
        {
            TabControl advancedControls = new TabControl();

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            advancedControls.TabBar.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
            advancedControls.TabBar.Margin = new BorderDouble(0, 0);
            advancedControls.TabBar.Padding = new BorderDouble(0, 2);

            int textSize = 16;

            if (AdvancedControlsButton_Click != null)
            {
                // this means we are in compact view and so we will make the tabs text a bit smaller
                textSize = 14;
                TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
                advancedControlsButtonFactory.invertImageLocation = false;
                advancedControlsLinkButton = advancedControlsButtonFactory.Generate(LocalizedString.Get("Print\nQueue"), "icon_arrow_left_32x32.png");
                advancedControlsLinkButton.Margin = new BorderDouble(right: 3);
                advancedControlsLinkButton.VAnchor = VAnchor.ParentBottom;
                advancedControlsLinkButton.Cursor = Cursors.Hand;
                advancedControlsLinkButton.Click += new EventHandler(AdvancedControlsButton_Click);
                advancedControlsLinkButton.MouseEnterBounds += new EventHandler(onMouseEnterBoundsPrintQueueLink);
                advancedControlsLinkButton.MouseLeaveBounds += new EventHandler(onMouseLeaveBoundsPrintQueueLink);

                advancedControls.TabBar.AddChild(advancedControlsLinkButton);
            }

            GuiWidget hSpacer = new GuiWidget();
            hSpacer.HAnchor = HAnchor.ParentLeftRight;

            advancedControls.TabBar.AddChild(hSpacer);

            GuiWidget manualPrinterControls = new ManualPrinterControls();
            ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
            manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            manualPrinterControlsScrollArea.AnchorAll();
            manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

            RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

            //Add the tab contents for 'Advanced Controls'
            string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(manualPrinterControlsScrollArea, printerControlsLabel), "Controls Tab", textSize,
            ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string sliceSettingsLabel = LocalizedString.Get("Slice Settings").ToUpper();
            sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(sliceSettingsWidget, sliceSettingsLabel), "Slice Settings Tab", textSize,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string configurationLabel = LocalizedString.Get("Configuration").ToUpper();
            ScrollableWidget configurationControls = new PrinterConfigurationScrollWidget();
            advancedControls.AddTab(new SimpleTextTabWidget(new TabPage(configurationControls, configurationLabel), "Configuration Tab", textSize,
                        ActiveTheme.Instance.PrimaryTextColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            advancedControls.SelectedTabIndex = lastAdvanceControlsIndex;

            return advancedControls;
        }

        public void ReloadSliceSettings(object state)
        {
            lastAdvanceControlsIndex = advancedControls2.SelectedTabIndex;

            WidescreenPanel.PreChangePanels.CallEvents(null, null);

            // remove the advance control and replace it with new ones built for the selected printer
            int advancedControlsIndex = GetChildIndex(advancedControls2);
            RemoveChild(advancedControlsIndex);
            advancedControls2.Close();

            if (advancedControlsLinkButton != null)
            {
                advancedControlsLinkButton.Click -= new EventHandler(AdvancedControlsButton_Click);
                advancedControlsLinkButton.MouseEnterBounds -= new EventHandler(onMouseEnterBoundsPrintQueueLink);
                advancedControlsLinkButton.MouseLeaveBounds -= new EventHandler(onMouseLeaveBoundsPrintQueueLink);
            }

            advancedControls2 = CreateNewAdvancedControls(AdvancedControlsButton_Click,
                onMouseEnterBoundsPrintQueueLink,
                onMouseLeaveBoundsPrintQueueLink);
            AddChild(advancedControls2, advancedControlsIndex);

            // This is a hack to make the panel remain on the screen.  It would be great to debug it and understand
            // why it does not work without this code in here.
            //currentParent.Parent.Width = currentParent.Parent.Width + 1;
        }
    }
}
