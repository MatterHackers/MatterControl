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
        EePromRepatierStorage storage;
        BindingList<EePromRepatierParameter> data = new BindingList<EePromRepatierParameter>();
        FlowLayoutWidget descriptionColmun;
        FlowLayoutWidget valueColmun;

        Button buttonCancel;
        Button buttonSave;
        
        bool reinit = true;
        public EePromRepetierWidget()
            : base(640, 480)
        {
            BackgroundColor = RGBA_Bytes.White;

            storage = new EePromRepatierStorage();

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);

            {
                FlowLayoutWidget columnHolder = new FlowLayoutWidget();
                descriptionColmun = new FlowLayoutWidget(FlowDirection.TopToBottom);
                columnHolder.AddChild(descriptionColmun);

                valueColmun = new FlowLayoutWidget(FlowDirection.TopToBottom);
                columnHolder.AddChild(valueColmun);

                descriptionColmun.AddChild(new TextWidget("Description"));
                valueColmun.AddChild(new TextWidget("Value"));

                topToBottom.AddChild(columnHolder);
            }

            buttonCancel = new Button(new LocalizedString("Cancel").Translated);
            topToBottom.AddChild(buttonCancel);

            buttonSave = new Button(new LocalizedString("Save To EEPROM").Translated);
            topToBottom.AddChild(buttonSave);

            //MatterControlApplication.Instance.LanguageChanged += translate;
            this.AddChild(topToBottom);

            translate();

            ShowAsSystemWindow();
        }
        
        public void translate()
        {
            Title = new LocalizedString("Firmware EEPROM Settings").Translated;
            buttonCancel.Text = new LocalizedString("Cancel").Translated;
            buttonCancel.Click += buttonAbort_Click;
            
            buttonSave.Text = new LocalizedString("Save to EEPROM").Translated;
            buttonSave.Click += buttonSave_Click;
        }

        private void newline(EePromRepatierParameter p)
        {
            data.Add(p);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            storage.Save();
            storage.Clear();
            storage.eventAdded -= newline;
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {
            storage.Clear();
            data.Clear();
            storage.eventAdded -= newline;
        }

        private void EEPROMRepetier_Activated(object sender, EventArgs e)
        {
            if (reinit)
            {
                reinit = false;
                storage.Clear();
                storage.eventAdded += newline;
                storage.AskPrinterForSettings();
            }
        }
    }
}
