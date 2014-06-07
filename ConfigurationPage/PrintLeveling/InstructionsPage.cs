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
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    public class InstructionsPage : WizardPage
    {
        protected FlowLayoutWidget topToBottomControls;

        public InstructionsPage(string pageDescription, string instructionsText)
            : base(pageDescription)
        {
            topToBottomControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomControls.Padding = new BorderDouble(3);
            topToBottomControls.HAnchor |= Agg.UI.HAnchor.ParentLeft;
            topToBottomControls.VAnchor |= Agg.UI.VAnchor.ParentTop;

            AddTextField(instructionsText, 10);

            AddChild(topToBottomControls);

            AnchorAll();
        }

        public static Vector3 ManualControlsFeedRate()
        {
            Vector3 feedRate = new Vector3(3000, 3000, 315);
            string savedSettings = ActivePrinterProfile.Instance.ActivePrinter.ManualMovementSpeeds;
            if (savedSettings != null && savedSettings != "")
            {
                feedRate.x = double.Parse(savedSettings.Split(',')[1]);
                feedRate.y = double.Parse(savedSettings.Split(',')[3]);
                feedRate.z = double.Parse(savedSettings.Split(',')[5]);
            }

            return feedRate;
        }

        public void AddTextField(string instructionsText, int pixelsFromLast)
        {
            GuiWidget spacer = new GuiWidget(10, pixelsFromLast);
            topToBottomControls.AddChild(spacer);

            EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
            string wrappedInstructions = wrapper.InsertCRs(instructionsText, 400);
            string wrappedInstructionsTabsToSpaces = wrappedInstructions.Replace("\t", "    ");
            TextWidget instructionsWidget = new TextWidget(wrappedInstructionsTabsToSpaces, textColor: ActiveTheme.Instance.PrimaryTextColor);
            instructionsWidget.HAnchor = Agg.UI.HAnchor.ParentLeft;
            topToBottomControls.AddChild(instructionsWidget);
        }
    }
}
