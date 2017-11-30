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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SliceLayerSelector : GuiWidget
	{
		public static int SliderWidth { get; } = (UserSettings.Instance.IsTouchScreen) ? 20 : 10;

		private InlineEditControl currentLayerInfo;

		private LayerScrollbar layerScrollbar;
		private BedConfig sceneContext;

		private SolidSlider layerSlider;

		public SliceLayerSelector(PrinterConfig printer, BedConfig sceneContext)
		{
			this.sceneContext = sceneContext;

			this.AddChild(layerScrollbar = new LayerScrollbar(printer, sceneContext)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right
			});

			layerSlider = layerScrollbar.layerSlider;

			currentLayerInfo = new InlineEditControl("1000")
			{
				GetDisplayString = (value) => $"{value + 1}",
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
			};
			currentLayerInfo.EditComplete += (s, e) =>
			{
				layerScrollbar.Value = currentLayerInfo.Value - 1;
			};
			this.AddChild(currentLayerInfo);

			sceneContext.ActiveLayerChanged += SetPositionAndValue;
			layerScrollbar.MouseEnter += SetPositionAndValue;
		}

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

		public override void OnClosed(ClosedEventArgs e)
		{
			sceneContext.ActiveLayerChanged -= SetPositionAndValue;
			layerSlider.MouseEnter -= SetPositionAndValue;

			base.OnClosed(e);
		}

		private void SetPositionAndValue(object sender, EventArgs e)
		{
			UiThread.RunOnIdle((Action)(() =>
			{
				currentLayerInfo.Value = sceneContext.ActiveLayerIndex;
				currentLayerInfo.Position = new Vector2(0, (double)(layerSlider.Position.Y + layerSlider.PositionPixelsFromFirstValue - 3));
				currentLayerInfo.Visible = true;
			}));
		}

		private class LayerScrollbar : FlowLayoutWidget
		{
			internal SolidSlider layerSlider;

			private TextWidget layerCountText;
			private TextWidget layerStartText;
			private BedConfig sceneContext;

			public LayerScrollbar(PrinterConfig printer, BedConfig sceneContext)
				: base(FlowDirection.TopToBottom)
			{
				this.sceneContext = sceneContext;

				layerCountText = new TextWidget("", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					MinimumSize = new Vector2(20, 20),
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.Center
				};
				this.AddChild(layerCountText);

				layerSlider = new SolidSlider(new Vector2(), SliderWidth, 0, 1, Orientation.Vertical)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Stretch,
					Margin = new BorderDouble(0, 5),
					Minimum = 0,
					Maximum = sceneContext.LoadedGCode?.LayerCount ?? 1,
					Value = sceneContext.ActiveLayerIndex
				};
				layerSlider.ValueChanged += (s, e) =>
				{
					if (printer?.Bed?.RenderInfo != null)
					{
						sceneContext.ActiveLayerIndex = (int)(layerSlider.Value + .5);
					}

					// show the layer info next to the slider
					this.Invalidate();
				};
				this.AddChild(layerSlider);

				layerStartText = new TextWidget("1", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					AutoExpandBoundsToText = true,
					HAnchor = HAnchor.Center
				};
				this.AddChild(layerStartText);

				sceneContext.ActiveLayerChanged += ActiveLayer_Changed;
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
					//layerSlider.OriginRelativeParent = new Vector2(this.Width - 20, 78);
					layerSlider.TotalWidthInPixels = layerSlider.Height;
				}
			}

			private void ActiveLayer_Changed(object sender, EventArgs e)
			{
				if (layerSlider != null
					&& sceneContext.ActiveLayerIndex != (int)(layerSlider.Value + .5))
				{
					layerSlider.Value = sceneContext.ActiveLayerIndex;
				}
			}
		}
	}
}
