/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using System;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class TemperatureWidgetBase : GuiWidget
	{
		protected TextWidget currentTempIndicator;
		protected TextWidget temperatureTypeName;
		protected Button preheatButton;

		protected TextImageButtonFactory whiteButtonFactory = new TextImageButtonFactory();

		private RGBA_Bytes borderColor = new RGBA_Bytes(255, 255, 255);
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

		private event EventHandler unregisterEvents;

		public TemperatureWidgetBase(string textValue)
			: base(52 * GuiWidget.DeviceScale, 52 * GuiWidget.DeviceScale)
		{
			whiteButtonFactory.FixedHeight = 18 * GuiWidget.DeviceScale;
			whiteButtonFactory.fontSize = 7;
			whiteButtonFactory.normalFillColor = RGBA_Bytes.White;
			whiteButtonFactory.normalTextColor = RGBA_Bytes.DarkGray;

			this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
			this.Margin = new BorderDouble(0, 2) * GuiWidget.DeviceScale;

			temperatureTypeName = new TextWidget("", pointSize: 8);
			temperatureTypeName.AutoExpandBoundsToText = true;
			temperatureTypeName.HAnchor = HAnchor.ParentCenter;
			temperatureTypeName.VAnchor = VAnchor.ParentTop;
			temperatureTypeName.Margin = new BorderDouble(0, 3);
			temperatureTypeName.TextColor = ActiveTheme.Instance.SecondaryAccentColor;
			temperatureTypeName.Visible = false;

			currentTempIndicator = new TextWidget(textValue, pointSize: 11);
			currentTempIndicator.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			currentTempIndicator.HAnchor = HAnchor.ParentCenter;
			currentTempIndicator.VAnchor = VAnchor.ParentCenter;
			currentTempIndicator.AutoExpandBoundsToText = true;

			GuiWidget buttonContainer = new GuiWidget();
			buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			buttonContainer.Height = 18 * GuiWidget.DeviceScale;

			preheatButton = whiteButtonFactory.Generate("Preheat".Localize().ToUpper());
			preheatButton.Cursor = Cursors.Hand;
			preheatButton.Visible = false;

			buttonContainer.AddChild(preheatButton);

			this.AddChild(temperatureTypeName);
			this.AddChild(currentTempIndicator);
			this.AddChild(buttonContainer);

			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

			this.MouseEnterBounds += onEnterBounds;
			this.MouseLeaveBounds += onLeaveBounds;
			this.preheatButton.Click += onPreheatButtonClick;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.currentTempIndicator.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.Invalidate();
		}

		private void onEnterBounds(Object sender, EventArgs e)
		{
			temperatureTypeName.Visible = true;
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected && !PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				preheatButton.Visible = true;
			}
		}

		private void onLeaveBounds(Object sender, EventArgs e)
		{
			temperatureTypeName.Visible = false;
			preheatButton.Visible = false;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			RectangleDouble Bounds = LocalBounds;
			RoundedRect borderRect = new RoundedRect(this.LocalBounds, 0);
			Stroke strokeRect = new Stroke(borderRect, borderWidth);
			graphics2D.Render(strokeRect, borderColor);
		}

		private void onPreheatButtonClick(object sender, EventArgs e)
		{
			SetTargetTemperature();
		}

		protected virtual void SetTargetTemperature()
		{
		}
	}
}