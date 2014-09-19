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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
    public class MacroControls : FlowLayoutWidget
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        protected FlowLayoutWidget presetButtonsContainer;

        protected string label;
        protected string editWindowLabel;


        public MacroControls()
            : base(FlowDirection.TopToBottom)
        {
            SetDisplayAttributes();
            AddChildElements();
        }

        void SetDisplayAttributes()
        {
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;            
			this.textImageButtonFactory.FixedHeight = 24;
			this.textImageButtonFactory.fontSize = 12;
            this.textImageButtonFactory.borderWidth = 1;
            this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
            this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.HAnchor = HAnchor.ParentLeftRight;
        }

        public override RectangleDouble LocalBounds
        {
            get
            {
                return base.LocalBounds;
            }
            set
            {
                base.LocalBounds = value;
            }
        }

        protected void ReloadMacros(object sender, EventArgs e)
        {
            ApplicationWidget.Instance.ReloadAdvancedControlsPanel();
        }

        
        EditMacrosWindow editSettingsWindow;
        void AddChildElements()
        {
            Button editButton;
			AltGroupBox groupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(LocalizedString.Get("Macros"), out editButton));
            editButton.Click += (sender, e) =>
            {
                if (editSettingsWindow == null)
                {
                    editSettingsWindow = new EditMacrosWindow(ReloadMacros);
                    editSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editSettingsWindow = null; };
                }
                else
                {
                    editSettingsWindow.BringToFront();
                }
            };

            groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            groupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            // make sure the client area will get smaller when the contents get smaller
            groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget controlRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
            controlRow.Margin = new BorderDouble(top: 5);
            controlRow.HAnchor |= HAnchor.ParentLeftRight;
            {
                {
                    this.presetButtonsContainer = GetMacroButtonContainer();
                    controlRow.AddChild(this.presetButtonsContainer);
                }
            }

            groupBox.AddChild(controlRow);
            this.AddChild(groupBox);
        }

        private FlowLayoutWidget GetMacroButtonContainer()
        {
            FlowLayoutWidget macroButtonContainer = new FlowLayoutWidget();
            macroButtonContainer.Margin = new BorderDouble(3, 0);
            macroButtonContainer.Padding = new BorderDouble(3);

            IEnumerable<DataStorage.CustomCommands> macroList = GetMacros();
            int buttonCount = 0;
            foreach (DataStorage.CustomCommands m in macroList)
            {
                buttonCount++;
                Button macroButton = textImageButtonFactory.Generate(m.Name);
                macroButton.Text = m.Value;
                macroButton.Margin = new BorderDouble(right: 5);
                macroButton.Click += (sender, e) =>
                {
                    SentCommandToPrinter(macroButton.Text);
                };
                macroButtonContainer.AddChild(macroButton);
            }
            if (buttonCount == 0)
            {
				TextWidget noMacrosFound = new TextWidget(LocalizedString.Get("No macros are currently setup for this printer."), pointSize:10);
                noMacrosFound.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                macroButtonContainer.AddChild(noMacrosFound);
            }
            return macroButtonContainer;
        }

        IEnumerable<DataStorage.CustomCommands> GetMacros()
        {
            IEnumerable<DataStorage.CustomCommands> results = Enumerable.Empty<DataStorage.CustomCommands>();
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                //Retrieve a list of saved printers from the Datastore
                string query = string.Format("SELECT * FROM CustomCommands WHERE PrinterId = {0};", ActivePrinterProfile.Instance.ActivePrinter.Id);
                results = (IEnumerable<DataStorage.CustomCommands>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.CustomCommands>(query);
                return results;
            }
            return results;            
        }

        protected void SentCommandToPrinter(string command)
        {
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow(command);
        }
    }
}