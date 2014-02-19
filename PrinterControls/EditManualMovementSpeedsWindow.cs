﻿/*
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

namespace MatterHackers.MatterControl
{
    public class EditManualMovementSpeedsWindow : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        EventHandler functionToCallOnSave;
        List<GuiWidget> listWithValues = new List<GuiWidget>();

        public EditManualMovementSpeedsWindow(string windowTitle, string movementSpeedsString, EventHandler functionToCallOnSave)
            : base(260, 300)
        {
			Title = new LocalizedString(windowTitle).Translated;

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);

            {
				string movementSpeedsLbl = new LocalizedString("Movement Speeds Presets").Translated;
				TextWidget elementHeader = new TextWidget(string.Format("{0}:",movementSpeedsLbl), pointSize: 14);
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

            this.functionToCallOnSave = functionToCallOnSave;
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            int oldHeight = textImageButtonFactory.FixedHeight;
            textImageButtonFactory.FixedHeight = 30;

            TextWidget tempTypeLabel = new TextWidget(windowTitle, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
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

            TextWidget tempLabel = new TextWidget(string.Format("mm / minute"), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
            tempLabel.HAnchor = HAnchor.ParentLeft;
            tempLabel.VAnchor = VAnchor.ParentCenter;

            tempLabelContainer.AddChild(tempLabel);

            leftRightLabels.AddChild(hLabelSpacer);
            leftRightLabels.AddChild(tempLabelContainer);

            presetsFormContainer.AddChild(leftRightLabels);

            // put in the movement edit controls
            string[] settingsArray = movementSpeedsString.Split(',');
            int preset_count = 1;
            int tab_index = 0;
            for (int i = 0; i < settingsArray.Count() - 1; i += 2)
            {
                FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
                leftRightEdit.Padding = new BorderDouble(3);
                leftRightEdit.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                TextWidget axisLabel;
                if (settingsArray[i].StartsWith("e"))
                {
                    int extruderIndex = (int)double.Parse(settingsArray[i].Substring(1)) + 1;
					string extruderLblTxt = new LocalizedString ("Extruder").Translated;
					axisLabel = new TextWidget(string.Format("{0} {1}",extruderLblTxt ,extruderIndex), textColor: ActiveTheme.Instance.PrimaryTextColor);
                }
                else
                {
					string axisLblText = new LocalizedString("Axis").Translated;
					axisLabel = new TextWidget(string.Format("{0} {1}",axisLblText, settingsArray[i]), textColor: ActiveTheme.Instance.PrimaryTextColor);
                }
                axisLabel.VAnchor = VAnchor.ParentCenter;
                leftRightEdit.AddChild(axisLabel);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                leftRightEdit.AddChild(hSpacer);

                // we add this to the listWithValues to make sure we build the string correctly on save.
                TextWidget typeEdit = new TextWidget(settingsArray[i]);
                listWithValues.Add(typeEdit);

                double movementSpeed = 0;
                double.TryParse(settingsArray[i + 1], out movementSpeed);
                MHNumberEdit valueEdit = new MHNumberEdit(movementSpeed, minValue: 0, pixelWidth: 60, tabIndex: tab_index++);
                valueEdit.Margin = new BorderDouble(3);
                leftRightEdit.AddChild(valueEdit);
                listWithValues.Add(valueEdit);

                //leftRightEdit.AddChild(textImageButtonFactory.Generate("Delete"));
                presetsFormContainer.AddChild(leftRightEdit);
                preset_count += 1;
            }

            textImageButtonFactory.FixedHeight = oldHeight;

            ShowAsSystemWindow();

			Button savePresetsButton = textImageButtonFactory.Generate(new LocalizedString("Save").Translated);
            savePresetsButton.Click += new ButtonBase.ButtonEventHandler(save_Click);

			Button cancelPresetsButton = textImageButtonFactory.Generate(new LocalizedString("Cancel").Translated);
            cancelPresetsButton.Click += (sender, e) => { Close(); };

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
            bool first = true;
            StringBuilder settingString = new StringBuilder();
            foreach (GuiWidget valueToAdd in listWithValues)
            {
                if (!first)
                {
                    settingString.Append(",");
                }

                settingString.Append(valueToAdd.Text);
                first = false;
            }
            functionToCallOnSave(this, new StringEventArgs(settingString.ToString()));
            Close();
        }
    }
}