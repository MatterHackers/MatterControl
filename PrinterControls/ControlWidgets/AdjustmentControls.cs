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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class AdjustmentControls : ControlWidgetBase
	{
		private NumberEdit feedRateValue;
		private SolidSlider feedRateRatioSlider;
		private SolidSlider extrusionRatioSlider;
		private NumberEdit extrusionValue;

		private readonly double minExtrutionRatio = .5;
		private readonly double maxExtrusionRatio = 3;
		private readonly double minFeedRateRatio = .5;
		private readonly double maxFeedRateRatio = 2;

		private event EventHandler unregisterEvents;

		protected override void AddChildElements()
		{
			AltGroupBox adjustmentControlsGroupBox = new AltGroupBox(new TextWidget("Tuning Adjustment".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
			adjustmentControlsGroupBox.Margin = new BorderDouble(0);
			adjustmentControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			adjustmentControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

			{
				FlowLayoutWidget tuningRatiosLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
				tuningRatiosLayout.Margin = new BorderDouble(0, 0, 0, 0);
				tuningRatiosLayout.HAnchor = HAnchor.ParentLeftRight;
				tuningRatiosLayout.Padding = new BorderDouble(3, 0, 3, 0);

				double sliderWidth = 300 * GuiWidget.DeviceScale;
				double sliderThumbWidth = 10 * GuiWidget.DeviceScale;
				if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
				{
					sliderThumbWidth = 15 * GuiWidget.DeviceScale;
				}

				TextWidget subheader = new TextWidget("", pointSize: 4, textColor: ActiveTheme.Instance.PrimaryTextColor);
				subheader.Margin = new BorderDouble(bottom: 6);
				tuningRatiosLayout.AddChild(subheader);
				TextWidget feedRateDescription;
				{
					FlowLayoutWidget feedRateLeftToRight;
					{
						feedRateValue = new NumberEdit(0, allowDecimals: true, minValue: minFeedRateRatio, maxValue: maxFeedRateRatio, pixelWidth: 40 * GuiWidget.DeviceScale);
						feedRateValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.FeedRateRatio * 100 + .5)) / 100.0;

						feedRateLeftToRight = new FlowLayoutWidget();
						feedRateLeftToRight.HAnchor = HAnchor.ParentLeftRight;

						feedRateDescription = new TextWidget(LocalizedString.Get("Speed Multiplier"));
						feedRateDescription.MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale;
						feedRateDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						feedRateDescription.VAnchor = VAnchor.ParentCenter;
						feedRateLeftToRight.AddChild(feedRateDescription);
						feedRateRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minFeedRateRatio, maxFeedRateRatio);
						feedRateRatioSlider.Margin = new BorderDouble(5, 0);
						feedRateRatioSlider.Value = PrinterConnectionAndCommunication.Instance.FeedRateRatio;
						feedRateRatioSlider.TotalWidthInPixels = sliderWidth;
						feedRateRatioSlider.View.BackgroundColor = new RGBA_Bytes();
						feedRateRatioSlider.ValueChanged += (sender, e) =>
						{
							PrinterConnectionAndCommunication.Instance.FeedRateRatio = feedRateRatioSlider.Value;
						};
						PrinterConnectionAndCommunication.Instance.FeedRateRatioChanged.RegisterEvent(FeedRateRatioChanged_Event, ref unregisterEvents);
						feedRateValue.EditComplete += (sender, e) =>
						{
							feedRateRatioSlider.Value = feedRateValue.Value;
						};
						feedRateLeftToRight.AddChild(feedRateRatioSlider);
						tuningRatiosLayout.AddChild(feedRateLeftToRight);

						feedRateLeftToRight.AddChild(feedRateValue);
						feedRateValue.Margin = new BorderDouble(0, 0, 5, 0);
						feedRateValue.VAnchor = VAnchor.ParentCenter;
						textImageButtonFactory.FixedHeight = (int)feedRateValue.Height + 1;
						textImageButtonFactory.borderWidth = 1;
						textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
						textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

						Button setFeedRateButton = textImageButtonFactory.Generate(LocalizedString.Get("Set"));
						setFeedRateButton.VAnchor = VAnchor.ParentCenter;

						feedRateLeftToRight.AddChild(setFeedRateButton);
					}

					TextWidget extrusionDescription;
					{
						extrusionValue = new NumberEdit(0, allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40 * GuiWidget.DeviceScale);
						extrusionValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.ExtrusionRatio * 100 + .5)) / 100.0;

						FlowLayoutWidget leftToRight = new FlowLayoutWidget();
						leftToRight.HAnchor = HAnchor.ParentLeftRight;
						leftToRight.Margin = new BorderDouble(top: 10);

						extrusionDescription = new TextWidget(LocalizedString.Get("Extrusion Multiplier"));
						extrusionDescription.MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale;
						extrusionDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						extrusionDescription.VAnchor = VAnchor.ParentCenter;
						leftToRight.AddChild(extrusionDescription);
						extrusionRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minExtrutionRatio, maxExtrusionRatio, Orientation.Horizontal);
						extrusionRatioSlider.TotalWidthInPixels = sliderWidth;
						extrusionRatioSlider.Margin = new BorderDouble(5, 0);
						extrusionRatioSlider.Value = PrinterConnectionAndCommunication.Instance.ExtrusionRatio;
						extrusionRatioSlider.View.BackgroundColor = new RGBA_Bytes();
						extrusionRatioSlider.ValueChanged += (sender, e) =>
						{
							PrinterConnectionAndCommunication.Instance.ExtrusionRatio = extrusionRatioSlider.Value;
						};
						PrinterConnectionAndCommunication.Instance.ExtrusionRatioChanged.RegisterEvent(ExtrusionRatioChanged_Event, ref unregisterEvents);
						extrusionValue.EditComplete += (sender, e) =>
						{
							extrusionRatioSlider.Value = extrusionValue.Value;
						};
						leftToRight.AddChild(extrusionRatioSlider);
						tuningRatiosLayout.AddChild(leftToRight);
						leftToRight.AddChild(extrusionValue);
						extrusionValue.Margin = new BorderDouble(0, 0, 5, 0);
						extrusionValue.VAnchor = VAnchor.ParentCenter;
						textImageButtonFactory.FixedHeight = (int)extrusionValue.Height + 1;
						Button setExtrusionButton = textImageButtonFactory.Generate(LocalizedString.Get("Set"));
						setExtrusionButton.VAnchor = VAnchor.ParentCenter;
						leftToRight.AddChild(setExtrusionButton);
					}
					feedRateLeftToRight.VAnchor = VAnchor.FitToChildren;
				}

				adjustmentControlsGroupBox.AddChild(tuningRatiosLayout);
			}

			this.AddChild(adjustmentControlsGroupBox);
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void ExtrusionRatioChanged_Event(object sender, EventArgs e)
		{
			extrusionRatioSlider.Value = PrinterConnectionAndCommunication.Instance.ExtrusionRatio;
			extrusionValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.ExtrusionRatio * 100 + .5)) / 100.0;
		}

		private void FeedRateRatioChanged_Event(object sender, EventArgs e)
		{
			feedRateRatioSlider.Value = PrinterConnectionAndCommunication.Instance.FeedRateRatio;
			feedRateValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.FeedRateRatio * 100 + .5)) / 100.0;
		}
	}
}