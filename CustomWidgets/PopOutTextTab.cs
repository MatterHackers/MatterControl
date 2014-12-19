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

using System;
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl;

namespace MatterHackers.Agg.UI
{
    public class PopOutTextTabWidget : Tab
    {
        public PopOutTextTabWidget(TabPage tabPageControledByTab, string internalTabName)
            : this(tabPageControledByTab, internalTabName, 12)
        {
        }

        public PopOutTextTabWidget(TabPage tabPageControledByTab, string internalTabName, double pointSize)
            : base(internalTabName, new GuiWidget(), new GuiWidget(), new GuiWidget(), tabPageControledByTab)
        {
            RGBA_Bytes selectedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            RGBA_Bytes selectedBackgroundColor = new RGBA_Bytes();
            RGBA_Bytes normalTextColor = ActiveTheme.Instance.TabLabelUnselected;
            RGBA_Bytes normalBackgroundColor = new RGBA_Bytes();

            AddText(tabPageControledByTab.Text, selectedWidget, selectedTextColor, selectedBackgroundColor, pointSize);
            AddText(tabPageControledByTab.Text, normalWidget, normalTextColor, normalBackgroundColor, pointSize);

            tabPageControledByTab.TextChanged += new EventHandler(tabPageControledByTab_TextChanged);

            SetBoundsToEncloseChildren();
        }

        void tabPageControledByTab_TextChanged(object sender, EventArgs e)
        {
            normalWidget.Children[0].Text = ((GuiWidget)sender).Text;
            normalWidget.SetBoundsToEncloseChildren();
            selectedWidget.Children[0].Text = ((GuiWidget)sender).Text;
            selectedWidget.SetBoundsToEncloseChildren();
            SetBoundsToEncloseChildren();
        }

        public TextWidget tabTitle;
        private void AddText(string tabText, GuiWidget widgetState, RGBA_Bytes textColor, RGBA_Bytes backgroundColor, double pointSize)
        {
            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            tabTitle = new TextWidget(tabText, pointSize: pointSize, textColor: textColor);
            tabTitle.AutoExpandBoundsToText = true;
            leftToRight.AddChild(tabTitle);
            widgetState.AddChild(leftToRight);
            widgetState.Selectable = false;
            widgetState.SetBoundsToEncloseChildren();
            widgetState.BackgroundColor = backgroundColor;
        }
    }
}
