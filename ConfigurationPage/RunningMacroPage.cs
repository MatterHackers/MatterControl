/*
Copyright (c) 2016, Lars Brubaker
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

using MatterHackers.Localizations;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Agg;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class RunningMacroPage : WizardPage
	{
		public static void Show(string message, bool showOkButton = false)
		{
			WizardWindow.Show("Macro", "Running Macro", new RunningMacroPage(message, showOkButton));
		}

		public RunningMacroPage(string message, bool showOkButton)
			: base("Cancel", "Macro Feedback")
		{
			TextWidget syncingText = new TextWidget(message, textColor: ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(syncingText);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			if(showOkButton)
			{
				Button okButton = textImageButtonFactory.Generate("OK".Localize());
				okButton.Margin = new BorderDouble(5);
				okButton.HAnchor = HAnchor.ParentCenter;
				
				okButton.Click += (s, e) => PrinterConnectionAndCommunication.Instance.MacroContinue();

				contentRow.AddChild(okButton);
			}

			cancelButton.Click += (s, e) =>
			{
				PrinterConnectionAndCommunication.Instance.MacroCancel();
			};
		}

		public override void OnClosed(EventArgs e)
		{
			PrinterConnectionAndCommunication.Instance.MacroContinue();
			base.OnClosed(e);
		}
	}
}
