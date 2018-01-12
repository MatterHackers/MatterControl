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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using static MatterHackers.MatterControl.JogControls;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ZAxisControls : FlowLayoutWidget
	{
		private MoveButtonFactory buttonFactory = new MoveButtonFactory()
		{
			FontSize = ApplicationController.Instance.Theme.DefaultFontSize,
		};

		public ZAxisControls(PrinterConfig printer, ThemeConfig theme, bool smallScreen) :
			base(FlowDirection.TopToBottom)
		{
			buttonFactory.Colors.Fill.Normal = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.Colors.Fill.Hover = ActiveTheme.Instance.PrimaryAccentColor;
			buttonFactory.BorderWidth = 0;
			buttonFactory.Colors.Text.Normal = Color.White;

			this.AddChild(new TextWidget("Z+", pointSize: smallScreen ? 12 : 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(bottom: 8)
			});

			this.AddChild(CreateZMoveButton(printer, .1, smallScreen));

			this.AddChild(CreateZMoveButton(printer, .02, smallScreen));

			this.AddChild(new ZTuningWidget(printer.Settings, theme)
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = 10
			});

			this.AddChild(CreateZMoveButton(printer, -.02, smallScreen));

			this.AddChild(CreateZMoveButton(printer, -.1, smallScreen));

			this.AddChild(new TextWidget("Z-", pointSize: smallScreen ? 12 : 15, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(top: 9),
			});

			//this.BackgroundColor = new Color(200, 0, 0, 30);

			this.Margin = new BorderDouble(0);
			this.Margin = 0;
			this.Padding = 3;
			this.VAnchor = VAnchor.Fit | VAnchor.Top;
		}

		private Button CreateZMoveButton(PrinterConfig printer, double moveAmount, bool smallScreen)
		{
			var button = buttonFactory.GenerateMoveButton(printer, $"{Math.Abs(moveAmount):0.00} mm", PrinterConnection.Axis.Z, printer.Settings.ZSpeed());
			button.MoveAmount = moveAmount;
			button.HAnchor = HAnchor.MaxFitOrStretch;
			button.VAnchor = VAnchor.Fit;
			button.Margin = new BorderDouble(0, 1);
			button.Padding = new BorderDouble(15, 7);
			if (smallScreen) button.Height = 45; else button.Height = 55;
			button.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			return button;
		}
	}
}