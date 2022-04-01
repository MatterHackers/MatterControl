/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
    public class AligningZAxisPageInstructions : WizardPage
    {
        private bool calibrationComplete;

        public AligningZAxisPageInstructions(ISetupWizard setupWizard, string instructionsText)
            : base(setupWizard, "Aligning Z Axis".Localize(), instructionsText)
        {
        }

        public override void OnClosed(EventArgs e)
        {
            calibrationComplete = false;

            // Unregister listeners
            printer.Connection.LineReceived -= Connection_LineRecieved;

            base.OnClosed(e);
        }

        public override void OnLoad(EventArgs args)
        {
            // Send the G34 Z Stepper Auto Align (https://reprap.org/wiki/G-code#G34:_Z_Stepper_Auto-Align)
            // 7 iterations .1 accuracy for early exit
            printer.Connection.QueueLine("G34 I7 T.1");
            NextButton.Enabled = false;

            // Register listeners
            printer.Connection.LineReceived += Connection_LineRecieved;

            // Always enable the advance button after 15 seconds
            UiThread.RunOnIdle(() =>
            {
                // Wait 30 seconds then ensure that if we miss the ok event, the user can still continue.
                if (!this.HasBeenClosed)
                {
                    NextButton.Enabled = true;
                }
            }, 30);

            base.OnLoad(args);
        }

        private void Connection_LineRecieved(object sender, string reciviedString)
        {
            if (reciviedString == "ok")
            {
                calibrationComplete = true;
                printer.Connection.LineReceived -= Connection_LineRecieved;
            }

            if (calibrationComplete)
            {
                NextButton.Enabled = true;
                UiThread.RunOnIdle(() => NextButton.InvokeClick());
            }
        }
    }
}