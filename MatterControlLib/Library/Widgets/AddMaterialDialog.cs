/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.Library.Widgets
{
    public class AddMaterialDialog : DialogPage
    {
        private readonly RadioButton createPrinterRadioButton = null;
        private readonly AddMaterialWidget materialPanel;
        private readonly TextButton nextButton;

        public AddMaterialDialog(Action<PrinterSettingsLayer> addMaterial)
        {
            bool userIsLoggedIn = !ApplicationController.GuestUserActive?.Invoke() ?? false;

            this.HeaderText = this.WindowTitle = "Printer Setup".Localize();
            this.WindowSize = new VectorMath.Vector2(800 * GuiWidget.DeviceScale, 600 * GuiWidget.DeviceScale);

            contentRow.BackgroundColor = theme.SectionBackgroundColor;
            nextButton = theme.CreateDialogButton("Next".Localize());

            materialPanel = new AddMaterialWidget(nextButton, theme, (enabled) =>
            {
                nextButton.Enabled = enabled;
            })
            {
                HAnchor = HAnchor.Stretch,
                VAnchor = VAnchor.Stretch
            };

            if (userIsLoggedIn)
            {
                contentRow.AddChild(materialPanel);
                contentRow.Padding = 0;
            }
            else
            {
                contentRow.Padding = theme.DefaultContainerPadding;
                materialPanel.Margin = new BorderDouble(left: 15, top: theme.DefaultContainerPadding);

                var commonMargin = new BorderDouble(4, 2);

                // Create export button for each plugin
                createPrinterRadioButton = new RadioButton(new RadioButtonViewText("Create a new printer", theme.TextColor))
                {
                    HAnchor = HAnchor.Left,
                    Margin = commonMargin,
                    Cursor = Cursors.Hand,
                    Name = "Create Printer Radio Button",
                    Checked = true
                };
                contentRow.AddChild(createPrinterRadioButton);

                createPrinterRadioButton.CheckedStateChanged += (s, e) =>
                {
                    materialPanel.Enabled = createPrinterRadioButton.Checked;
                    this.SetElementVisibility();
                };

                contentRow.AddChild(materialPanel);
            }

            nextButton.Name = "Next Button";
            nextButton.Click += (s, e) => UiThread.RunOnIdle(async () =>
            {
                bool controlsValid = materialPanel.ValidateControls();
                if (controlsValid
                    && materialPanel.SelectedPrinter is AddPrinterWidget.MakeModelInfo selectedPrinter)
                {
                    var printer = await ProfileManager.CreatePrinterAsync(selectedPrinter.Make, selectedPrinter.Model, materialPanel.NewPrinterName);
                    if (printer == null)
                    {
                        materialPanel.SetError("Error creating profile".Localize());
                        return;
                    }

                    UiThread.RunOnIdle(() =>
                    {
                        DialogWindow.ChangeToPage(AppContext.Platform.GetConnectDevicePage(printer) as DialogPage);
                    });
                }
            });

            var printerNotListedButton = theme.CreateDialogButton("Define New".Localize());
            printerNotListedButton.ToolTipText = "Select this option only if your printer does not appear in the list".Localize();

            printerNotListedButton.Click += async (s, e) =>
            {
                var printer = await ProfileManager.CreatePrinterAsync("Other", "Other", "Custom Printer");
                if (printer == null)
                {
                    materialPanel.SetError("Error creating profile".Localize());
                    return;
                }

                UiThread.RunOnIdle(() =>
                {
                    DialogWindow.ChangeToPage(new SetupCustomPrinter(printer) as DialogPage);
                });
            };

            this.AddPageAction(printerNotListedButton, false);
            this.AddPageAction(nextButton);

            SetElementVisibility();
        }

        private void SetElementVisibility()
        {
            nextButton.Enabled = materialPanel.SelectedPrinter != null;
        }
    }
}