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
//#define SIMULATE_CONNECTION

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.EeProm
{
    public partial class EePromRepetierWidget : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        EePromRepetierStorage currentEePromSettings;
        BindingList<EePromRepetierParameter> data = new BindingList<EePromRepetierParameter>();
        FlowLayoutWidget settingsColmun;

        event EventHandler unregisterEvents;

        Button buttonCancel;
        Button buttonSave;
        
        public EePromRepetierWidget()
            : base(540, 480)
        {
            BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

            currentEePromSettings = new EePromRepetierStorage();

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            topToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			topToBottom.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			topToBottom.Padding = new BorderDouble (3, 0);

            FlowLayoutWidget row = new FlowLayoutWidget();
            row.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            row.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            GuiWidget descriptionWidget = AddDescription(LocalizedString.Get("Description"));
            descriptionWidget.Margin = new BorderDouble(left: 3);
            row.AddChild(descriptionWidget);

            CreateSpacer(row);

            GuiWidget valueText = new TextWidget(LocalizedString.Get("Value"), textColor: ActiveTheme.Instance.PrimaryTextColor);
            valueText.VAnchor = Agg.UI.VAnchor.ParentCenter;
            valueText.Margin = new BorderDouble(left: 5, right: 60);
            row.AddChild(valueText);
            topToBottom.AddChild(row);

            {
                ScrollableWidget settingsAreaScrollBox = new ScrollableWidget(true);
                settingsAreaScrollBox.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
                topToBottom.AddChild(settingsAreaScrollBox);

                settingsColmun = new FlowLayoutWidget(FlowDirection.TopToBottom);
                settingsColmun.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;

                settingsAreaScrollBox.AddChild(settingsColmun);
            }

            FlowLayoutWidget buttonBar = new FlowLayoutWidget();
            buttonBar.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            buttonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            buttonSave = textImageButtonFactory.Generate(LocalizedString.Get("Save To EEPROM"));
			buttonSave.Margin = new BorderDouble(0,3);
            buttonBar.AddChild(buttonSave);

            CreateSpacer(buttonBar);

            buttonCancel = textImageButtonFactory.Generate(LocalizedString.Get("Cancel"));
            buttonCancel.Margin = new BorderDouble(3);
            buttonBar.AddChild(buttonCancel);

            topToBottom.AddChild(buttonBar);

            this.AddChild(topToBottom);

            translate();
            //MatterControlApplication.Instance.LanguageChanged += translate;

            ShowAsSystemWindow();

            currentEePromSettings.Clear();
            PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(currentEePromSettings.Add, ref unregisterEvents); 
            currentEePromSettings.eventAdded += NewSettingReadFromPrinter;
            currentEePromSettings.AskPrinterForSettings();

#if SIMULATE_CONNECTION
            UiThread.RunOnIdle(AddSimulatedItems);
#endif
        }

#if SIMULATE_CONNECTION
        int count;
        void AddSimulatedItems(object state)
        {
            NewSettingReadFromPrinter(this, new EePromRepetierParameter("this is a test line " + count.ToString()));

            count++;
            if (count < 30)
            {
                UiThread.RunOnIdle(AddSimulatedItems);
            }
        }
#endif

        private static void CreateSpacer(FlowLayoutWidget buttonBar)
        {
            GuiWidget spacer = new GuiWidget(1, 1);
            spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            buttonBar.AddChild(spacer);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }
        
        public void translate()
        {
            Title = LocalizedString.Get("Firmware EEPROM Settings");
            buttonCancel.Text = LocalizedString.Get("Close");
            buttonCancel.Click += buttonAbort_Click;

            buttonSave.Text = LocalizedString.Get("Save to EEPROM");
            buttonSave.Click += buttonSave_Click;
        }

        private void NewSettingReadFromPrinter(object sender, EventArgs e)
        {
            EePromRepetierParameter newSetting = e as EePromRepetierParameter;
            if (newSetting != null)
            {
                data.Add(newSetting);

                UiThread.RunOnIdle(AddItemToUi, newSetting);
            }
        }

        void AddItemToUi(object state)
        {
            EePromRepetierParameter newSetting = state as EePromRepetierParameter;
            if (newSetting != null)
            {
                FlowLayoutWidget row = new FlowLayoutWidget();
                row.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                row.AddChild(AddDescription(newSetting.Description));
                row.Padding = new BorderDouble(5, 0);
                if ((settingsColmun.Children.Count % 2) == 1)
                {
                    row.BackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
                }

                CreateSpacer(row);

                double currentValue;
                double.TryParse(newSetting.Value, out currentValue);
                MHNumberEdit valueEdit = new MHNumberEdit(currentValue, pixelWidth: 80, allowNegatives: true, allowDecimals: true);
                valueEdit.VAnchor = Agg.UI.VAnchor.ParentCenter;
                valueEdit.ActuallNumberEdit.EditComplete += (sender, e) =>
                {
                    newSetting.Value = valueEdit.ActuallNumberEdit.Value.ToString();
                };
                row.AddChild(valueEdit);

                settingsColmun.AddChild(row);
            }

            // TODO: fix the flow layout so we don't need this.
            // This is correcting a bug in flow layout widgets not setting sizes correctly.
            double oldWidth = Width;
            Width = Width + 1;
            Width = oldWidth;
        }

        private GuiWidget AddDescription(string description)
        {
            GuiWidget holder = new GuiWidget(340, 40);
            TextWidget textWidget = new TextWidget(description, textColor: ActiveTheme.Instance.PrimaryTextColor);
            textWidget.VAnchor = Agg.UI.VAnchor.ParentCenter;
            holder.AddChild(textWidget);

            return holder;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonSave_Click);
        }

        private void DoButtonSave_Click(object state)
        {
            currentEePromSettings.Save();
            currentEePromSettings.Clear();
            currentEePromSettings.eventAdded -= NewSettingReadFromPrinter;
            Close();
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonAbort_Click);
        }

        private void DoButtonAbort_Click(object state)
        {
            currentEePromSettings.Clear();
            data.Clear();
            currentEePromSettings.eventAdded -= NewSettingReadFromPrinter;
            Close();
        }
    }
}
