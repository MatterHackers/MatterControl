/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PlusTabPage : FlowLayoutWidget
	{
		public PlusTabPage()
			: base(FlowDirection.BottomToTop)
		{
			Name = "+";

			HAnchor = HAnchor.ParentLeftRight;
			VAnchor = VAnchor.ParentBottomTop;

			var leftRight = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			AddChild(leftRight);

			// put in the add new design stuff
			var createItems = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
				Margin = 15,
			};
			leftRight.AddChild(createItems);

			var label = new TextWidget("Create New".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor);
			createItems.AddChild(label);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(top: 15, bottom: 15),
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.FitToChildren
			};
			createItems.AddChild(container);

			var createPart = ApplicationController.Instance.Theme.BreadCrumbButtonFactory.Generate("Create Part");
			createPart.HAnchor = HAnchor.ParentLeft;
			container.AddChild(createPart);

			var createPrinter = ApplicationController.Instance.Theme.BreadCrumbButtonFactory.Generate("Create Printer");
			createPrinter.HAnchor = HAnchor.ParentLeft;
			container.AddChild(createPrinter);
			createPrinter.Click += (s, e) =>
			{
				if (PrinterConnection.Instance.PrinterIsPrinting
					|| PrinterConnection.Instance.PrinterIsPaused)
				{
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize())
					);
				}
				else
				{
					UiThread.RunOnIdle(() =>
					{
						//WizardPage.WizardWindow.ChangeToSetupPrinterForm(true);
						WizardWindow.ShowPrinterSetup(true);

					});
				}
			};

			var existingLabel = new TextWidget("Open Existing".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(top: 15, bottom: 15)
			};
			createItems.AddChild(existingLabel);

			var printerSelector = new PrinterSelectEditDropdown()
			{
				Margin = new BorderDouble(left: 15)
			};
			createItems.AddChild(printerSelector);
		}
	}
}
