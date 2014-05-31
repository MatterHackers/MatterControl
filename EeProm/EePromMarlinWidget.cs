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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.EeProm
{
    public partial class EePromMarlinWidget : SystemWindow
    {
        EePromMarlinSettings currentEePromSettings;

        MHNumberEdit stepsPerMmX;
        MHNumberEdit stepsPerMmY;
        MHNumberEdit stepsPerMmZ;
        MHNumberEdit stepsPerMmE;

        MHNumberEdit maxFeedrateMmPerSX;
        MHNumberEdit maxFeedrateMmPerSY;
        MHNumberEdit maxFeedrateMmPerSZ;
        MHNumberEdit maxFeedrateMmPerSE;

        MHNumberEdit maxAccelerationMmPerSSqrdX;
        MHNumberEdit maxAccelerationMmPerSSqrdY;
        MHNumberEdit maxAccelerationMmPerSSqrdZ;
        MHNumberEdit maxAccelerationMmPerSSqrdE;

        MHNumberEdit acceleration;
        MHNumberEdit retractAcceleration;

        MHNumberEdit pidP;
        MHNumberEdit pidI;
        MHNumberEdit pidD;

        MHNumberEdit homingOffsetX;
        MHNumberEdit homingOffsetY;
        MHNumberEdit homingOffsetZ;

        MHNumberEdit minFeedrate;
        MHNumberEdit minTravelFeedrate;
        MHNumberEdit minSegmentTime;
        
        MHNumberEdit maxXYJerk;
        MHNumberEdit maxZJerk;
        
        Button buttonAbort;
        Button buttonReLoadSettings;
        Button buttonSetActive;
        Button buttonSetToFactorySettings;
        Button buttonSave;

        event EventHandler unregisterEvents;

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        double maxWidthOfLeftStuff = 0;
        List<GuiWidget> leftStuffToSize = new List<GuiWidget>();

        public EePromMarlinWidget()
            : base(700, 480)
        {
            Title = LocalizedString.Get("Marlin Firmware EEPROM Settings");

            currentEePromSettings = new EePromMarlinSettings();
            currentEePromSettings.eventAdded += SetUiToPrinterSettings;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget (FlowDirection.TopToBottom);
			mainContainer.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
			mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			mainContainer.Padding = new BorderDouble (3, 0);

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            topToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			topToBottom.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			topToBottom.Padding = new BorderDouble (top: 3);

            // the top button bar
            {
                FlowLayoutWidget topButtonBar = new FlowLayoutWidget();
                topButtonBar.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                topButtonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

                CreateMainButton(ref buttonReLoadSettings, topButtonBar, "Re-Load Default Settings");
                buttonReLoadSettings.Click += buttonReLoadSettings_Click;

				topButtonBar.Margin = new BorderDouble (0, 3);
                
                CreateMainButton(ref buttonSetToFactorySettings, topButtonBar, "Set Default To Factory Settings");
                buttonSetToFactorySettings.Click += SetToFactorySettings;

				mainContainer.AddChild(topButtonBar);
            }

            topToBottom.AddChild(Create4FieldSet("Steps per mm:",
                "X:", ref stepsPerMmX,
                "Y:", ref stepsPerMmY,
                "Z:", ref stepsPerMmZ,
                "E:", ref stepsPerMmE));

            topToBottom.AddChild(Create4FieldSet("Maximum feedrates [mm/s]:",
                "X:", ref maxFeedrateMmPerSX,
                "Y:", ref maxFeedrateMmPerSY,
                "Z:", ref maxFeedrateMmPerSZ,
                "E:", ref maxFeedrateMmPerSE));

            topToBottom.AddChild(Create4FieldSet("Maximum Acceleration [mm/s²]:",
                "X:", ref maxAccelerationMmPerSSqrdX,
                "Y:", ref maxAccelerationMmPerSSqrdY,
                "Z:", ref maxAccelerationMmPerSSqrdZ,
                "E:", ref maxAccelerationMmPerSSqrdE));

            topToBottom.AddChild(CreateField("Acceleration:", ref acceleration));
            topToBottom.AddChild(CreateField("Retract Acceleration:", ref retractAcceleration));

            topToBottom.AddChild(Create3FieldSet("PID settings:",
                "P:", ref pidP,
                "I:", ref pidI,
                "D:", ref pidD));

            topToBottom.AddChild(Create3FieldSet("Homing Offset:",
                "X:", ref homingOffsetX,
                "Y:", ref homingOffsetY,
                "Z:", ref homingOffsetZ));

            topToBottom.AddChild(CreateField("Min feedrate [mm/s]:", ref minFeedrate));
            topToBottom.AddChild(CreateField("Min travel feedrate [mm/s]:", ref minTravelFeedrate));
            topToBottom.AddChild(CreateField("Minimum segment time [ms]:", ref minSegmentTime));
            topToBottom.AddChild(CreateField("Maximum X-Y jerk [mm/s]:", ref maxXYJerk));
            topToBottom.AddChild(CreateField("Maximum Z jerk [mm/s]:", ref maxZJerk));

            GuiWidget topBottomSpacer = new GuiWidget(1, 1);
            topBottomSpacer.VAnchor = VAnchor.ParentBottomTop;
            topToBottom.AddChild(topBottomSpacer);

			mainContainer.AddChild (topToBottom);

            // the bottom button bar
            {
                FlowLayoutWidget bottomButtonBar = new FlowLayoutWidget();
                bottomButtonBar.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                bottomButtonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				bottomButtonBar.Margin = new BorderDouble (0, 3);

                CreateMainButton(ref buttonSetActive, bottomButtonBar, "Make Settings Active");
                buttonSetActive.Click += buttonSetActive_Click;

                CreateMainButton(ref buttonSave, bottomButtonBar, "Make Settings Active\nAnd Save To Default");
                buttonSave.Click += buttonSave_Click;

                CreateSpacer(bottomButtonBar);

                CreateMainButton(ref buttonAbort, bottomButtonBar, "Close");
                buttonAbort.Click += buttonAbort_Click;

				mainContainer.AddChild(bottomButtonBar);
            }

            PrinterCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents);

            currentEePromSettings.eventAdded += SetUiToPrinterSettings;

			AddChild(mainContainer);

            ShowAsSystemWindow();

            // and ask the printer to send the settings
            currentEePromSettings.Update();

            foreach (GuiWidget widget in leftStuffToSize)
            {
                widget.Width = maxWidthOfLeftStuff;
            }
        }

        private GuiWidget CreateMHNumEdit(ref MHNumberEdit numberEditToCreate)
        {
            numberEditToCreate = new MHNumberEdit(0, pixelWidth: 80, allowNegatives: true, allowDecimals: true);
            numberEditToCreate.VAnchor = Agg.UI.VAnchor.ParentCenter;
            numberEditToCreate.Margin = new BorderDouble(3, 0);
            return numberEditToCreate;
        }

        private GuiWidget CreateField(string label, ref MHNumberEdit field1)
        {
            MHNumberEdit none = null;

            return Create4FieldSet(label,
            "", ref field1,
            null, ref none,
            null, ref none,
            null, ref none);
        }

        private GuiWidget Create3FieldSet(string label,
            string field1Label, ref MHNumberEdit field1,
            string field2Label, ref MHNumberEdit field2,
            string field3Label, ref MHNumberEdit field3)
        {
            MHNumberEdit none = null;

            return Create4FieldSet(label,
            field1Label, ref field1,
            field2Label, ref field2,
            field3Label, ref field3,
            null, ref none);
        }

        GuiWidget CreateTextField(string label)
        {
            GuiWidget textWidget = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor);
            textWidget.VAnchor = VAnchor.ParentCenter;
            textWidget.HAnchor = HAnchor.ParentRight;
            GuiWidget container = new GuiWidget(textWidget.Height, 24);
            container.AddChild(textWidget);
            return container;            
        }

        private GuiWidget Create4FieldSet(string label,
            string field1Label, ref MHNumberEdit field1,
            string field2Label, ref MHNumberEdit field2,
            string field3Label, ref MHNumberEdit field3,
            string field4Label, ref MHNumberEdit field4)
        {
            FlowLayoutWidget row = new FlowLayoutWidget();
            row.Margin = new BorderDouble(3);
            row.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            TextWidget labelWidget = new TextWidget(LocalizedString.Get(label), textColor: ActiveTheme.Instance.PrimaryTextColor);
            labelWidget.VAnchor = VAnchor.ParentCenter;
            maxWidthOfLeftStuff = Math.Max(maxWidthOfLeftStuff, labelWidget.Width);
            GuiWidget holder = new GuiWidget(labelWidget.Width, labelWidget.Height);
            holder.Margin = new BorderDouble(3, 0);
            holder.AddChild(labelWidget);
            leftStuffToSize.Add(holder);
            row.AddChild(holder);

            row.AddChild(CreateTextField(field1Label));
            row.AddChild(CreateMHNumEdit(ref field1));

            if (field2Label != null)
            {
                row.AddChild(CreateTextField(field2Label));
                row.AddChild(CreateMHNumEdit(ref field2));
            }

            if (field3Label != null)
            {
                row.AddChild(CreateTextField(field3Label));
                row.AddChild(CreateMHNumEdit(ref field3));
            }

            if (field4Label != null)
            {
                row.AddChild(CreateTextField(field4Label));
                row.AddChild(CreateMHNumEdit(ref field4));
            }

            return row;
        }

        private static void CreateSpacer(FlowLayoutWidget buttonBar)
        {
            GuiWidget spacer = new GuiWidget(1, 1);
            spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            buttonBar.AddChild(spacer);
        }

        private void CreateMainButton(ref Button button, FlowLayoutWidget buttonBar, string text)
        {
            button = textImageButtonFactory.Generate(LocalizedString.Get(text));
            buttonBar.AddChild(button);
        }

        private void buttonReLoadSettings_Click(object sender, EventArgs e)
        {
            currentEePromSettings.Update();
        }

        private void SetToFactorySettings(object sender, EventArgs e)
        {
            currentEePromSettings.SetPrinterToFactorySettings();
            currentEePromSettings.Update();
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonAbort_Click);
        }

        private void DoButtonAbort_Click(object state)
        {
            Close();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void SetUiToPrinterSettings(object sender, EventArgs e)
        {
            stepsPerMmX.Text = currentEePromSettings.SX;
            stepsPerMmY.Text = currentEePromSettings.SY;
            stepsPerMmZ.Text = currentEePromSettings.SZ;
            stepsPerMmE.Text = currentEePromSettings.SE;
            maxFeedrateMmPerSX.Text = currentEePromSettings.FX;
            maxFeedrateMmPerSY.Text = currentEePromSettings.FY;
            maxFeedrateMmPerSZ.Text = currentEePromSettings.FZ;
            maxFeedrateMmPerSE.Text = currentEePromSettings.FE;
            maxAccelerationMmPerSSqrdX.Text = currentEePromSettings.AX;
            maxAccelerationMmPerSSqrdY.Text = currentEePromSettings.AY;
            maxAccelerationMmPerSSqrdZ.Text = currentEePromSettings.AZ;
            maxAccelerationMmPerSSqrdE.Text = currentEePromSettings.AE;
            acceleration.Text = currentEePromSettings.ACC;
            retractAcceleration.Text = currentEePromSettings.RACC;
            minFeedrate.Text = currentEePromSettings.AVS;
            minTravelFeedrate.Text = currentEePromSettings.AVT;
            minSegmentTime.Text = currentEePromSettings.AVB;
            maxXYJerk.Text = currentEePromSettings.AVX;
            maxZJerk.Text = currentEePromSettings.AVZ;
            pidP.Enabled = pidI.Enabled = pidD.Enabled = currentEePromSettings.hasPID;
            pidP.Text = currentEePromSettings.PPID;
            pidI.Text = currentEePromSettings.IPID;
            pidD.Text = currentEePromSettings.DPID;
            homingOffsetX.Text = currentEePromSettings.hox;
            homingOffsetY.Text = currentEePromSettings.hoy;
            homingOffsetZ.Text = currentEePromSettings.hoz;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonSave_Click);
        }

        private void DoButtonSave_Click(object state)
        {
            SaveSettingsToActive();
            currentEePromSettings.SaveToEeProm();
            Close();
        }

        void buttonSetActive_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonSetActive_Click);
        }

        void DoButtonSetActive_Click(object state)
        {
            SaveSettingsToActive();
            Close();
        }

        void SaveSettingsToActive()
        {
            currentEePromSettings.SX = stepsPerMmX.Text;
            currentEePromSettings.SY = stepsPerMmY.Text;
            currentEePromSettings.SZ = stepsPerMmZ.Text;
            currentEePromSettings.SE = stepsPerMmE.Text;
            currentEePromSettings.FX = maxFeedrateMmPerSX.Text;
            currentEePromSettings.FY = maxFeedrateMmPerSY.Text;
            currentEePromSettings.FZ = maxFeedrateMmPerSZ.Text;
            currentEePromSettings.FE = maxFeedrateMmPerSE.Text;
            currentEePromSettings.AX = maxAccelerationMmPerSSqrdX.Text;
            currentEePromSettings.AY = maxAccelerationMmPerSSqrdY.Text;
            currentEePromSettings.AZ = maxAccelerationMmPerSSqrdZ.Text;
            currentEePromSettings.AE = maxAccelerationMmPerSSqrdE.Text;
            currentEePromSettings.ACC = acceleration.Text;
            currentEePromSettings.RACC = retractAcceleration.Text;
            currentEePromSettings.AVS = minFeedrate.Text;
            currentEePromSettings.AVT = minTravelFeedrate.Text;
            currentEePromSettings.AVB = minSegmentTime.Text;
            currentEePromSettings.AVX = maxXYJerk.Text;
            currentEePromSettings.AVZ = maxZJerk.Text;
            currentEePromSettings.PPID = pidP.Text;
            currentEePromSettings.IPID = pidI.Text;
            currentEePromSettings.DPID = pidD.Text;
            currentEePromSettings.HOX = homingOffsetX.Text;
            currentEePromSettings.HOY = homingOffsetY.Text;
            currentEePromSettings.HOZ = homingOffsetZ.Text;

            currentEePromSettings.Save();
        }
    }
}