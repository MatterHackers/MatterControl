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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class SavePartsSheetFeedbackWindow : SystemWindow
	{
		private int totalParts;
		private int count = 0;
		private FlowLayoutWidget feedback;

		public SavePartsSheetFeedbackWindow(int totalParts, string firstPartName, Color backgroundColor)
			: base(300, 500)
		{
			this.BackgroundColor = backgroundColor;
			this.Title = ApplicationController.Instance.ProductName + " - " + "Saving to Parts Sheet".Localize();
			this.totalParts = totalParts;

			feedback = new FlowLayoutWidget(FlowDirection.TopToBottom);
			feedback.Padding = 5;
			feedback.AnchorAll();
			this.AddChild(feedback);
		}

		private TextWidget CreateNextLine(string startText)
		{
			return new TextWidget(startText, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 2),
				HAnchor = Agg.UI.HAnchor.Left,
				AutoExpandBoundsToText = true
			};
		}

		public void StartingNextPart(object sender, EventArgs e)
		{
			count++;

			var stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				feedback.AddChild(
					CreateNextLine($"{count}/{totalParts} '{stringEvent.Data}'"));
			}
		}

		public void DoneSaving(object sender, EventArgs e)
		{
			var stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				feedback.AddChild(CreateNextLine(stringEvent.Data));
			}
		}
	}
}