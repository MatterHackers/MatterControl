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

namespace MatterHackers.MatterControl
{
    public class EditLevelingSettingsWindow : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        List<GuiWidget> listWithValues = new List<GuiWidget>();

        Vector3[] positions = new Vector3[3];

        public EditLevelingSettingsWindow()
            : base(360, 360)
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
                string movementSpeedsLbl = LocalizedString.Get("Sampled Positions".Localize());
                TextWidget elementHeader = new TextWidget(string.Format("{0}:", movementSpeedsLbl), pointSize: 14);
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

            TextWidget tempTypeLabel = new TextWidget(Title, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
            tempTypeLabel.Margin = new BorderDouble(3);
            tempTypeLabel.HAnchor = HAnchor.ParentLeft;
            presetsFormContainer.AddChild(tempTypeLabel);

            FlowLayoutWidget leftRightLabels = new FlowLayoutWidget();
            leftRightLabels.Padding = new BorderDouble(3, 6);
            leftRightLabels.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            GuiWidget hLabelSpacer = new GuiWidget();
            hLabelSpacer.HAnchor = HAnchor.ParentLeftRight;

            GuiWidget tempLabelContainer = new GuiWidget();
            tempLabelContainer.Width = 76;
            tempLabelContainer.Height = 16;
            tempLabelContainer.Margin = new BorderDouble(3, 0);

            leftRightLabels.AddChild(hLabelSpacer);
            leftRightLabels.AddChild(tempLabelContainer);

            presetsFormContainer.AddChild(leftRightLabels);

            // put in the movement edit controls
            Vector2 probeBackCenter = ActiveSliceSettings.Instance.GetPrintLevelPositionToSample(0);

            positions[0] = ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(0);
            positions[1] = ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(1);
            positions[2] = ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(2);
            
            string[] settingsArray = "{0},{1},{2},{3},{4},{5},{6},{7},{8}".FormatWith(
                positions[0].x, positions[0].y, positions[0].z,
                positions[1].x, positions[1].y, positions[1].z,
                positions[2].x, positions[2].y, positions[2].z).Split(',');
            int preset_count = 1;
            int tab_index = 0;
            for (int i = 0; i < settingsArray.Count() - 1; i += 3)
            {
                FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
                leftRightEdit.Padding = new BorderDouble(3);
                leftRightEdit.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                TextWidget positionLabel;

                string whichPositionText = LocalizedString.Get("Position");
                positionLabel = new TextWidget("{0} {1,-14} x: {2,-10:0.00} y:{3,-10:0.00} z:".FormatWith(whichPositionText, preset_count, settingsArray[i], settingsArray[i + 1]), textColor: ActiveTheme.Instance.PrimaryTextColor);
                
                positionLabel.VAnchor = VAnchor.ParentCenter;
                leftRightEdit.AddChild(positionLabel);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                leftRightEdit.AddChild(hSpacer);

                // we add this to the listWithValues to make sure we build the string correctly on save.
                TextWidget typeEdit = new TextWidget(settingsArray[i]);
                listWithValues.Add(typeEdit);

                double zPosition = 0;
                double.TryParse(settingsArray[i + 2], out zPosition);
                MHNumberEdit valueEdit = new MHNumberEdit(zPosition, allowNegatives:true, allowDecimals: true, minValue: 0, pixelWidth: 60, tabIndex: tab_index++);
                int insideValue = preset_count-1;
                valueEdit.ActuallNumberEdit.InternalTextEditWidget.EditComplete += (sender, e) =>
                {
                    positions[insideValue].z = valueEdit.ActuallNumberEdit.Value;
                };
                
                valueEdit.Margin = new BorderDouble(3);
                leftRightEdit.AddChild(valueEdit);
                listWithValues.Add(valueEdit);

                presetsFormContainer.AddChild(leftRightEdit);
                preset_count += 1;

                presetsFormContainer.AddChild(new CustomWidgets.HorizontalLine());
            }

            textImageButtonFactory.FixedHeight = oldHeight;


            ShowAsSystemWindow();
			MinimumSize = new Vector2(360, 360);

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
            double[] printLevelPositions3x3 =  
            {
                positions[0].x, positions[0].y, positions[0].z, 
                positions[1].x, positions[1].y, positions[1].z, 
                positions[2].x, positions[2].y, positions[2].z, 
            };

            ActivePrinterProfile.Instance.SetPrintLevelingMeasuredPositions(printLevelPositions3x3);

            PrintLeveling.Instance.SetPrintLevelingEquation(
                ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(0),
                ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(1),
                ActivePrinterProfile.Instance.GetPrintLevelingMeasuredPosition(2),
                ActiveSliceSettings.Instance.PrintCenter);

            Close();
        }
    }
}