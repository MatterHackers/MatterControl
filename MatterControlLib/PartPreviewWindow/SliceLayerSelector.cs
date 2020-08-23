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
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SliceLayerSelector : GuiWidget
	{
		public static double SliderWidth { get; } = 10 * GuiWidget.DeviceScale;

		private readonly InlineEditControl currentLayerInfo;

		private readonly LayerScrollbar layerScrollbar;
		private readonly double layerInfoHalfHeight;
		private readonly PrinterConfig printer;

		public SliceLayerSelector(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.AddChild(layerScrollbar = new LayerScrollbar(printer, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right
			});

			SolidSlider = layerScrollbar.layerSlider;
			theme.ApplySliderStyle(SolidSlider);

			var tagContainer = new HorizontalTag()
			{
				HAnchor = HAnchor.Fit | HAnchor.Right,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(6, 4, 10, 4),
				Margin = new BorderDouble(right: layerScrollbar.Width + layerScrollbar.Margin.Width),
				TagColor = theme.IsDarkTheme ? theme.Shade : theme.SlightShade
			};

			currentLayerInfo = new InlineEditControl("1000")
			{
				Name = "currentLayerInfo",
				GetDisplayString = (value) => $"{value}",
				HAnchor = HAnchor.Right | HAnchor.Fit,
				VAnchor = VAnchor.Absolute | VAnchor.Fit,
			};
			currentLayerInfo.EditComplete += (s, e) =>
			{
				layerScrollbar.Value = currentLayerInfo.Value - 1;
			};

			tagContainer.AddChild(currentLayerInfo);
			this.AddChild(tagContainer);

			currentLayerInfo.Visible = true;
			layerInfoHalfHeight = currentLayerInfo.Height / 2;
			currentLayerInfo.Visible = false;

			SolidSlider.ValueChanged += (s, e) =>
			{
				currentLayerInfo.StopEditing();
				currentLayerInfo.Position = new Vector2(0, (double)(SolidSlider.Position.Y + SolidSlider.PositionPixelsFromFirstValue - layerInfoHalfHeight));
			};

			// Set initial position
			currentLayerInfo.Position = new Vector2(0, (double)(SolidSlider.Position.Y + SolidSlider.PositionPixelsFromFirstValue - layerInfoHalfHeight));

			printer.Bed.ActiveLayerChanged += SetPositionAndValue;
			layerScrollbar.MouseEnter += SetPositionAndValue;
		}

		public SolidSlider SolidSlider { get; }

		public double Maximum
		{
			get => layerScrollbar.Maximum;
			set => layerScrollbar.Maximum = value;
		}

		public double Value
		{
			get => layerScrollbar.Value;
			set => layerScrollbar.Value = value;
		}

		public override void OnClosed(EventArgs e)
		{
			printer.Bed.ActiveLayerChanged -= SetPositionAndValue;
			SolidSlider.MouseEnter -= SetPositionAndValue;

			base.OnClosed(e);
		}

		private void SetPositionAndValue(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				currentLayerInfo.Value = printer.Bed.ActiveLayerIndex + 1;
				currentLayerInfo.Visible = true;
			});
		}

		private class LayerScrollbar : FlowLayoutWidget
		{
			internal SolidSlider layerSlider;
			private readonly PrinterConfig printer;
			private readonly TextWidget layerCountText;
			private readonly TextWidget layerStartText;

			public LayerScrollbar(PrinterConfig printer, ThemeConfig theme)
				: base(FlowDirection.TopToBottom)
			{
				this.printer = printer;
				layerCountText = new TextWidget("", pointSize: 9, textColor: theme.TextColor)
				{
					MinimumSize = new Vector2(20, 20),
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.Center
				};
				this.AddChild(layerCountText);

				layerSlider = new SolidSlider(default(Vector2), SliderWidth, theme, 0, 1, Orientation.Vertical)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Stretch,
					Margin = new BorderDouble(0, 5),
					Minimum = 0,
					Maximum = printer.Bed.LoadedGCode?.LayerCount ?? 1,
					Value = printer.Bed.ActiveLayerIndex
				};
				layerSlider.ValueChanged += (s, e) =>
				{
					if (printer?.Bed?.RenderInfo != null)
					{
						printer.Bed.ActiveLayerIndex = (int)(layerSlider.Value + .5);
					}

					// show the layer info next to the slider
					this.Invalidate();
				};
				this.AddChild(layerSlider);

				layerStartText = new TextWidget("1", pointSize: 9, textColor: theme.TextColor)
				{
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.Center
				};
				this.AddChild(layerStartText);

				printer.Bed.ActiveLayerChanged += ActiveLayer_Changed;
			}

			public double Maximum
			{
				get => layerSlider.Maximum;
				set
				{
					layerSlider.Maximum = value - 1;
					layerCountText.Text = value.ToString();
				}
			}

			public double Value
			{
				get => layerSlider.Value;
				set => layerSlider.Value = value;
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				base.OnBoundsChanged(e);

				if (layerSlider != null)
				{
					// layerSlider.OriginRelativeParent = new Vector2(this.Width - 20, 78);
					layerSlider.TotalWidthInPixels = layerSlider.Height;
				}
			}

			public override void OnClosed(EventArgs e)
			{
				base.OnClosed(e);

				printer.Bed.ActiveLayerChanged -= ActiveLayer_Changed;
			}

			private void ActiveLayer_Changed(object sender, EventArgs e)
			{
				if (layerSlider != null
					&& printer.Bed.ActiveLayerIndex != (int)(layerSlider.Value + .5))
				{
					layerSlider.Value = printer.Bed.ActiveLayerIndex;
				}
			}
		}
	}
}
