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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class TemperatureWidgetBase : PopupButton
	{
		protected TextWidget CurrentTempIndicator;
		private TextWidget goalTempIndicator;
		protected TextWidget DirectionIndicator;

		protected ImageWidget ImageWidget = new ImageWidget(AggContext.StaticData.LoadIcon("hotend.png"))
		{
			VAnchor = VAnchor.Center,
			Margin = new BorderDouble(right: 5)
		};

		protected EventHandler unregisterEvents;
		protected PrinterConfig printer;

		protected virtual int ActualTemperature { get; }
		protected virtual int TargetTemperature { get; }

		public TemperatureWidgetBase(PrinterConfig printer, string textValue)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.Cursor = Cursors.Hand;

			this.AlignToRightEdge = true;

			var container = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(5)
			};
			this.AddChild(container);

			if (ActiveTheme.Instance.IsDarkTheme)
			{
				this.ImageWidget.Image = this.ImageWidget.Image.InvertLightness();
			}

			container.AddChild(this.ImageWidget);

			CurrentTempIndicator = new TextWidget(textValue, pointSize: 11)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true
			};
			container.AddChild(CurrentTempIndicator);

			container.AddChild(new TextWidget("/") { TextColor = ActiveTheme.Instance.PrimaryTextColor });

			goalTempIndicator = new TextWidget(textValue, pointSize: 11)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true
			};
			container.AddChild(goalTempIndicator);

			DirectionIndicator = new TextWidget(textValue, pointSize: 11)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(left: 5)
			};
			container.AddChild(DirectionIndicator);
		}

		protected void DisplayCurrentTemperature()
		{
			int actualTemperature =  this.ActualTemperature;
			int targetTemperature = this.TargetTemperature;

			if (targetTemperature > 0)
			{
				int targetTemp = (int)(targetTemperature + 0.5);
				int actualTemp = (int)(actualTemperature + 0.5);

				this.goalTempIndicator.Text = $"{targetTemperature:0.#}°";
				this.DirectionIndicator.Text = (targetTemp < actualTemp) ? "↓" : "↑";
			}
			else
			{
				this.DirectionIndicator.Text = "";
			}

			this.CurrentTempIndicator.Text = $"{actualTemperature:0.#}";
			this.goalTempIndicator.Text = $"{targetTemperature:0.#}";
		}

		protected virtual void SetTargetTemperature(double targetTemp) { }

		protected virtual GuiWidget GetPopupContent() { return null; }

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
