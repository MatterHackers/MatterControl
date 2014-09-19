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
        event EventHandler unregisterEvents;

        TabControl mainControlsTabControl;
        public TabPage QueueTabPage;
        public TabPage AboutTabPage;

        QueueDataView queueDataView;

        GuiWidget LeftPanel
        {
            get { return GetPanel(0); }
        }

        GuiWidget RightPanel
        {
            get { return GetPanel(1); }
        }

        static int lastPanelIndexBeforeReload = 0;
        public CompactSlidePanel(QueueDataView queueDataView)
            : base(2)
        {
            this.queueDataView = queueDataView;

            // do the front panel stuff
            {
                // first add the print progress bar
                this.LeftPanel.AddChild(new PrintProgressBar());

                // construct the main controls tab control
                mainControlsTabControl = new FirstPanelTabView(queueDataView);

                TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
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

            // do the right panel
            {
                this.RightPanel.AddChild(new PrintProgressBar());
                ThirdPanelTabView thirdPanelTabView = new ThirdPanelTabView(AdvancedControlsButton_Click, onMouseEnterBoundsPrintQueueLink, onMouseLeaveBoundsPrintQueueLink);
                thirdPanelTabView.Name = "For - CompactSlidePanel";
                this.RightPanel.AddChild(thirdPanelTabView);
            }

            WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentPanelIndex, ref unregisterEvents);

            SetPanelIndexImediate(lastPanelIndexBeforeReload);
        }

        void SaveCurrentPanelIndex(object sender, EventArgs e)
        {
            lastPanelIndexBeforeReload = PanelIndex;
        }

        void AdvancedControlsButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (this.PanelIndex == 0)
            {
                this.PanelIndex = 1;
            }
            else
            {
                this.PanelIndex = 0;
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
    }
}
