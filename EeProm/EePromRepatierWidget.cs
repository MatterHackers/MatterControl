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
using System.ComponentModel;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.EeProm
{
    public partial class EePromRepetierWidget : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        EePromRepatierStorage storage;
        BindingList<EePromRepatierParameter> data = new BindingList<EePromRepatierParameter>();
        FlowLayoutWidget settingsColmun;

        event EventHandler unregisterEvents;

        Button buttonCancel;
        Button buttonSave;
        
        public EePromRepetierWidget()
            : base(540, 480)
        {
            BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

            storage = new EePromRepatierStorage();

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            topToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            FlowLayoutWidget row = new FlowLayoutWidget();
            row.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            row.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            GuiWidget descriptionWidget = AddDescription(new LocalizedString("Description").Translated);
            descriptionWidget.Margin = new BorderDouble(left: 3);
            row.AddChild(descriptionWidget);

            GuiWidget topSpacer = new GuiWidget(1, 1);
            topSpacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            row.AddChild(topSpacer);

            GuiWidget valueText = new TextWidget(new LocalizedString("Value").Translated, textColor: ActiveTheme.Instance.PrimaryTextColor);
            valueText.VAnchor = Agg.UI.VAnchor.ParentCenter;
            valueText.Margin = new BorderDouble(left: 5, right: 60);
            row.AddChild(valueText);
            topToBottom.AddChild(row);

            {
                ScrollableWidget settingsAreaScrollBox = new ScrollableWidget(true);
                settingsAreaScrollBox.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                settingsAreaScrollBox.AnchorAll();
                topToBottom.AddChild(settingsAreaScrollBox);

                settingsColmun = new FlowLayoutWidget(FlowDirection.TopToBottom);
                settingsColmun.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;

                settingsAreaScrollBox.AddChild(settingsColmun);
            }

            FlowLayoutWidget buttonBar = new FlowLayoutWidget();
            buttonBar.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            buttonBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            buttonSave = textImageButtonFactory.Generate(new LocalizedString("Save To EEPROM").Translated);
            buttonSave.Margin = new BorderDouble(3);
            buttonBar.AddChild(buttonSave);

            GuiWidget spacer = new GuiWidget(1, 1);
            spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            buttonBar.AddChild(spacer);

            buttonCancel = textImageButtonFactory.Generate(new LocalizedString("Cancel").Translated);
            buttonCancel.Margin = new BorderDouble(3);
            buttonBar.AddChild(buttonCancel);

            topToBottom.AddChild(buttonBar);

            //MatterControlApplication.Instance.LanguageChanged += translate;
            this.AddChild(topToBottom);

            translate();

            ShowAsSystemWindow();

            storage.Clear();
            PrinterCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(storage.Add, ref unregisterEvents); 
            storage.eventAdded += NewSettingReadFromPrinter;
            storage.AskPrinterForSettings();
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
            Title = new LocalizedString("Firmware EEPROM Settings").Translated;
            buttonCancel.Text = new LocalizedString("Cancel").Translated;
            buttonCancel.Click += buttonAbort_Click;
            
            buttonSave.Text = new LocalizedString("Save to EEPROM").Translated;
            buttonSave.Click += buttonSave_Click;
        }

        private void NewSettingReadFromPrinter(object sender, EventArgs e)
        {
            EePromRepatierParameter newSetting = e as EePromRepatierParameter;
            if (newSetting != null)
            {
                data.Add(newSetting);

                UiThread.RunOnIdle(AddItemToUi, newSetting);
            }
        }

        void AddItemToUi(object state)
        {
            EePromRepatierParameter newSetting = state as EePromRepatierParameter;
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

                GuiWidget spacer = new GuiWidget(1, 1);
                spacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                row.AddChild(spacer);

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
            storage.Save();
            storage.Clear();
            storage.eventAdded -= NewSettingReadFromPrinter;
            Close();
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoButtonAbort_Click);
        }

        private void DoButtonAbort_Click(object state)
        {
            storage.Clear();
            data.Clear();
            storage.eventAdded -= NewSettingReadFromPrinter;
            Close();
        }
    }
}
