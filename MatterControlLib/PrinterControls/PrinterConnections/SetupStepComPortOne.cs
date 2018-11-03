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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System.IO;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortOne : DialogPage
	{
		public SetupStepComPortOne(PrinterConfig printer)
		{
			this.WindowTitle = "Setup Wizard".Localize();

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(5),
				HAnchor = HAnchor.Stretch
			};

			BorderDouble elementMargin = new BorderDouble(top: 5);

			var printerMessageOne = new TextWidget("MatterControl will now attempt to auto-detect printer.".Localize(), 0, 0, 10)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerMessageOne);

			var printerMessageTwo = new TextWidget(string.Format("1.) {0} ({1}).", "Disconnect printer".Localize(), "if currently connected".Localize()), 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerMessageTwo);

			var printerMessageThree = new TextWidget(string.Format("2.) {0} '{1}'.", "Press".Localize(), "Continue".Localize()), 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerMessageThree);

			var removeImage = AggContext.StaticData.LoadImage(Path.Combine("Images", "remove usb.png"));
			removeImage.SetRecieveBlender(new BlenderPreMultBGRA());
			container.AddChild(new ImageWidget(removeImage)
			{
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(0, 10),
			});

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.Stretch;
			container.AddChild(vSpacer);

			var setupManualConfigurationOrSkipConnectionWidget = new TextWidget("You can also".Localize() + ":", 0, 0, 10)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(setupManualConfigurationOrSkipConnectionWidget);

			var manualLink = new LinkLabel("Manually Configure Connection".Localize(), theme)
			{
				Margin = new BorderDouble(0, 5),
				TextColor = theme.TextColor
			};
			manualLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				DialogWindow.ChangeToPage(new SetupStepComPortManual(printer));
			});
			container.AddChild(manualLink);

			var printerMessageFour = new TextWidget("or".Localize(), 0, 0, 10)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerMessageFour);

			var skipConnectionLink = new LinkLabel("Skip Connection Setup".Localize(), theme)
			{
				Margin = new BorderDouble(0, 8),
				TextColor = theme.TextColor
			};
			skipConnectionLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				printer.Connection.HaltConnectionThread();
				Parent.Close();
			});
			container.AddChild(skipConnectionLink);

			contentRow.AddChild(container);

			//Construct buttons
			var nextButton = theme.CreateDialogButton("Continue".Localize());
			nextButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				DialogWindow.ChangeToPage(new SetupStepComPortTwo(printer));
			});

			this.AddPageAction(nextButton);
		}
	}
}