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
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl
{
    public class EditLevelingSettingsWindow : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        Vector3[] positions = new Vector3[3];

        public EditLevelingSettingsWindow()
            : base(400, 200)
        {
            Title = LocalizedString.Get("Leveling Settings".Localize());

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);

            {
                string movementSpeedsLabel = LocalizedString.Get("Sampled Positions".Localize());
                TextWidget elementHeader = new TextWidget(string.Format("{0}:", movementSpeedsLabel), pointSize: 14);
                elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                elementHeader.HAnchor = HAnchor.ParentLeftRight;
                elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

                headerRow.AddChild(elementHeader);
            }

            topToBottom.AddChild(headerRow);

            FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            //ListBox printerListContainer = new ListBox();
            {
                presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
                presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
                presetsFormContainer.Padding = new BorderDouble(3);
                presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            }

            topToBottom.AddChild(presetsFormContainer);

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            int oldHeight = textImageButtonFactory.FixedHeight;
            textImageButtonFactory.FixedHeight = 30;

            // put in the movement edit controls
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            positions[0] = levelingData.sampledPosition0;
            positions[1] = levelingData.sampledPosition1;
            positions[2] = levelingData.sampledPosition2;
            
            int tab_index = 0;
            for (int row = 0; row < 3; row++ )
            {
                FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
                leftRightEdit.Padding = new BorderDouble(3);
                leftRightEdit.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                TextWidget positionLabel;

                string whichPositionText = LocalizedString.Get("Position");
                positionLabel = new TextWidget("{0} {1,-5}".FormatWith(whichPositionText, row+1), textColor: ActiveTheme.Instance.PrimaryTextColor);

                positionLabel.VAnchor = VAnchor.ParentCenter;
                leftRightEdit.AddChild(positionLabel);

                for (int axis = 0; axis < 3; axis++)
                {
                    GuiWidget hSpacer = new GuiWidget();
                    hSpacer.HAnchor = HAnchor.ParentLeftRight;

                    leftRightEdit.AddChild(hSpacer);

                    string axisName = "x";
                    if (axis == 1) axisName = "y";
                    else if (axis == 2) axisName = "z";

                    TextWidget typeEdit = new TextWidget("  {0}: ".FormatWith(axisName), textColor: ActiveTheme.Instance.PrimaryTextColor);
                    typeEdit.VAnchor = VAnchor.ParentCenter;
                    leftRightEdit.AddChild(typeEdit);

                    int linkCompatibleRow = row;
                    int linkCompatibleAxis = axis;
                    double minValue = double.MinValue;
                    if (axis == 2)
                    {
                        minValue = 0;
                    }
                    MHNumberEdit valueEdit = new MHNumberEdit(positions[linkCompatibleRow][linkCompatibleAxis], allowNegatives: true, allowDecimals: true, minValue: minValue, pixelWidth: 60, tabIndex: tab_index++);
                    valueEdit.ActuallNumberEdit.InternalTextEditWidget.EditComplete += (sender, e) =>
                    {
                        positions[linkCompatibleRow][linkCompatibleAxis] = valueEdit.ActuallNumberEdit.Value;
                    };

                    valueEdit.Margin = new BorderDouble(3);
                    leftRightEdit.AddChild(valueEdit);
                }

                presetsFormContainer.AddChild(leftRightEdit);

                presetsFormContainer.AddChild(new CustomWidgets.HorizontalLine());
            }

            textImageButtonFactory.FixedHeight = oldHeight;


            ShowAsSystemWindow();
			MinimumSize = new Vector2(Width, Height);

            Button savePresetsButton = textImageButtonFactory.Generate("Save".Localize());
            savePresetsButton.Click += new ButtonBase.ButtonEventHandler(save_Click);

            Button cancelPresetsButton = textImageButtonFactory.Generate("Cancel".Localize());
            cancelPresetsButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
                {
                    Close();
                });
            };

            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Padding = new BorderDouble(0, 3);

            GuiWidget hButtonSpacer = new GuiWidget();
            hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

            buttonRow.AddChild(savePresetsButton);
            buttonRow.AddChild(hButtonSpacer);
            buttonRow.AddChild(cancelPresetsButton);

            topToBottom.AddChild(buttonRow);

            AddChild(topToBottom);
        }

        void save_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(DoSave_Click);
        }

        void DoSave_Click(object state)
        {
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);

            levelingData.sampledPosition0 = positions[0];
            levelingData.sampledPosition1 = positions[1];
            levelingData.sampledPosition2 = positions[2];

            PrintLevelingPlane.Instance.SetPrintLevelingEquation(
                levelingData.sampledPosition0,
                levelingData.sampledPosition1,
                levelingData.sampledPosition2,
                ActiveSliceSettings.Instance.PrintCenter);

            Close();
        }
    }
}