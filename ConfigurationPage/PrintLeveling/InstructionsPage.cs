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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class InstructionsPage : WizardControlPage
	{
		double extraTextScaling = 1;
		protected FlowLayoutWidget topToBottomControls;

		protected PrinterConfig printer { get; }

		public InstructionsPage(PrinterConfig printer, string pageDescription, string instructionsText)
			: base(pageDescription)
		{
			this.printer = printer;

			if (UserSettings.Instance.IsTouchScreen)
			{
				extraTextScaling = 1.33333;
			}

			topToBottomControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomControls.Padding = new BorderDouble(3);
			topToBottomControls.HAnchor |= Agg.UI.HAnchor.Left;
			topToBottomControls.VAnchor |= Agg.UI.VAnchor.Top;

			AddTextField(instructionsText, 10);

			AddChild(topToBottomControls);

			AnchorAll();
		}

		public void AddTextField(string instructionsText, int pixelsFromLast)
		{
			GuiWidget spacer = new GuiWidget(10, pixelsFromLast);
			topToBottomControls.AddChild(spacer);

			EnglishTextWrapping wrapper = new EnglishTextWrapping(12);
			string wrappedInstructions = wrapper.InsertCRs(instructionsText, 400);
			string wrappedInstructionsTabsToSpaces = wrappedInstructions.Replace("\t", "    ");
			TextWidget instructionsWidget = new TextWidget(wrappedInstructionsTabsToSpaces, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 12 * extraTextScaling);
			instructionsWidget.HAnchor = Agg.UI.HAnchor.Left;
			topToBottomControls.AddChild(instructionsWidget);
		}
	}
}