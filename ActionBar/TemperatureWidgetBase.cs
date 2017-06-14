/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class TemperatureWidgetBase : GuiWidget
	{
		private TextWidget currentTempIndicator;
		protected TextWidget temperatureTypeName;
		protected Button preheatButton;

		protected TextImageButtonFactory whiteButtonFactory = new TextImageButtonFactory()
		{
			FixedHeight = 18 * GuiWidget.DeviceScale,
			fontSize = 7,
			normalFillColor = RGBA_Bytes.White,
			normalTextColor = RGBA_Bytes.DarkGray,
		};

		private static RGBA_Bytes borderColor = new RGBA_Bytes(255, 255, 255);
		private int borderWidth = 2;

		public string IndicatorValue
		{
			get
			{
				return currentTempIndicator.Text;
			}
			set
			{
				if (currentTempIndicator.Text != value)
				{
					currentTempIndicator.Text = value;
				}
			}
		}

		public TemperatureWidgetBase(string textValue)
			: base(52 * GuiWidget.DeviceScale, 52 * GuiWidget.DeviceScale)
		{
			this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
			this.Margin = new BorderDouble(0, 2) * GuiWidget.DeviceScale;

			temperatureTypeName = new TextWidget("", pointSize: 8)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.ParentCenter,
				VAnchor = VAnchor.ParentTop,
				Margin = new BorderDouble(0, 3),
				TextColor = ActiveTheme.Instance.SecondaryAccentColor,
				Visible = false
			};
			this.AddChild(temperatureTypeName);

			currentTempIndicator = new TextWidget(textValue, pointSize: 11)
			{
				TextColor = ActiveTheme.Instance.PrimaryAccentColor,
				HAnchor = HAnchor.ParentCenter,
				VAnchor = VAnchor.ParentCenter,
				AutoExpandBoundsToText = true
			};
			this.AddChild(currentTempIndicator);

			var buttonContainer = new GuiWidget()
			{
				HAnchor = Agg.UI.HAnchor.ParentLeftRight,
				Height = 18 * GuiWidget.DeviceScale
			};
			this.AddChild(buttonContainer);

			preheatButton = whiteButtonFactory.Generate("Preheat".Localize().ToUpper());
			preheatButton.Cursor = Cursors.Hand;
			preheatButton.Visible = false;
			preheatButton.Click += (s, e) => SetTargetTemperature();
			buttonContainer.AddChild(preheatButton);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			temperatureTypeName.Visible = true;
			if (PrinterConnection.Instance.PrinterIsConnected && !PrinterConnection.Instance.PrinterIsPrinting)
			{
				preheatButton.Visible = true;
			}

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			temperatureTypeName.Visible = false;
			preheatButton.Visible = false;

			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var borderRect = new RoundedRect(this.LocalBounds, 0);
			graphics2D.Render(new Stroke(borderRect, borderWidth), borderColor);
		}

		protected virtual void SetTargetTemperature() { }
	}
}