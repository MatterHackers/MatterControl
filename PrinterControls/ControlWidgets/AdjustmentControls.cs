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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class AdjustmentControls : ControlWidgetBase
	{
		private MHNumberEdit feedRateValue;
		private MHNumberEdit extrusionValue;

		private SolidSlider feedRateRatioSlider;
		private SolidSlider extrusionRatioSlider;

		private readonly double minExtrutionRatio = .5;
		private readonly double maxExtrusionRatio = 3;
		private readonly double minFeedRateRatio = .25;
		private readonly double maxFeedRateRatio = 3;

		private EventHandler unregisterEvents;

		public AdjustmentControls()
		{
			var adjustmentControlsGroupBox = new AltGroupBox(new TextWidget("Tuning Adjustment".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor))
			{
				Margin = 0,
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.ParentLeftRight
			};

			var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 0, 0, 0),
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(3, 0, 3, 0)
			};

			double sliderWidth = 300 * GuiWidget.DeviceScale;
			double sliderThumbWidth = 10 * GuiWidget.DeviceScale;
			if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
			{
				sliderThumbWidth = 15 * GuiWidget.DeviceScale;
			}

			var subheader = new TextWidget("", pointSize: 4, textColor: ActiveTheme.Instance.PrimaryTextColor);
			subheader.Margin = new BorderDouble(bottom: 6);
			topToBottom.AddChild(subheader);

			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
					Margin = 0,
					VAnchor = VAnchor.FitToChildren
				};

				var feedRateDescription = new TextWidget("Speed Multiplier".Localize())
				{
					MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale,
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					VAnchor = VAnchor.ParentCenter,
				};
				row.AddChild(feedRateDescription);

				double feedrateRatio = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.feedrate_ratio);
				feedRateRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minFeedRateRatio, maxFeedRateRatio)
				{
					Name = "Feed Rate Slider",
					Margin = new BorderDouble(5, 0),
					Value = feedrateRatio,
					HAnchor = HAnchor.ParentLeftRight,
					TotalWidthInPixels = sliderWidth,
				};
				feedRateRatioSlider.View.BackgroundColor = new RGBA_Bytes();
				feedRateRatioSlider.ValueChanged += (sender, e) =>
				{
					feedRateValue.ActuallNumberEdit.Value = Math.Round(feedRateRatioSlider.Value, 2);
				};
				feedRateRatioSlider.SliderReleased += (s, e) =>
				{
					ActiveSliceSettings.Instance.SetValue(SettingsKey.feedrate_ratio, Math.Round(feedRateRatioSlider.Value, 2).ToString());
				};
				row.AddChild(feedRateRatioSlider);

				feedRateValue = new MHNumberEdit(Math.Round(feedrateRatio, 2), allowDecimals: true, minValue: minFeedRateRatio, maxValue: maxFeedRateRatio, pixelWidth: 40 * GuiWidget.DeviceScale)
				{
					Name = "Feed Rate NumberEdit",
					SelectAllOnFocus = true,
					Margin = new BorderDouble(0, 0, 5, 0),
					VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
					Padding = 0
				};
				feedRateValue.ActuallNumberEdit.EditComplete += (sender, e) =>
				{
					feedRateRatioSlider.Value = feedRateValue.ActuallNumberEdit.Value;
					ActiveSliceSettings.Instance.SetValue(SettingsKey.feedrate_ratio, Math.Round(feedRateRatioSlider.Value, 2).ToString());
				};
				row.AddChild(feedRateValue);

				topToBottom.AddChild(row);

				textImageButtonFactory.FixedHeight = (int)feedRateValue.Height + 1;
				textImageButtonFactory.borderWidth = 1;
				textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
				textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

				Button setButton = textImageButtonFactory.Generate("Set".Localize());
				setButton.VAnchor = VAnchor.ParentCenter;
				row.AddChild(setButton);
			}

			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
					Margin = new BorderDouble(top: 10),
					VAnchor = VAnchor.FitToChildren
				};

				var extrusionDescription = new TextWidget("Extrusion Multiplier".Localize())
				{
					MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale,
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					VAnchor = VAnchor.ParentCenter
				};
				row.AddChild(extrusionDescription);

				double extrusionRatio = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.extrusion_ratio);
				extrusionRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minExtrutionRatio, maxExtrusionRatio, Orientation.Horizontal)
				{
					Name = "Extrusion Multiplier Slider",
					TotalWidthInPixels = sliderWidth,
					HAnchor = HAnchor.ParentLeftRight,
					Margin = new BorderDouble(5, 0),
					Value = extrusionRatio
				};
				extrusionRatioSlider.View.BackgroundColor = new RGBA_Bytes();
				extrusionRatioSlider.ValueChanged += (sender, e) =>
				{
					extrusionValue.ActuallNumberEdit.Value = Math.Round(extrusionRatioSlider.Value, 2);
				};
				extrusionRatioSlider.SliderReleased += (s, e) =>
				{
					ActiveSliceSettings.Instance.SetValue(SettingsKey.extrusion_ratio, Math.Round(extrusionRatioSlider.Value, 2).ToString());
				};

				extrusionValue = new MHNumberEdit(Math.Round(extrusionRatio, 2), allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40 * GuiWidget.DeviceScale)
				{
					Name = "Extrusion Multiplier NumberEdit",
					SelectAllOnFocus = true,
					Margin = new BorderDouble(0, 0, 5, 0),
					VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
					Padding = 0
				};
				extrusionValue.ActuallNumberEdit.EditComplete += (sender, e) =>
				{
					extrusionRatioSlider.Value = extrusionValue.ActuallNumberEdit.Value;
					ActiveSliceSettings.Instance.SetValue(SettingsKey.extrusion_ratio, Math.Round(extrusionRatioSlider.Value, 2).ToString());
				};
				row.AddChild(extrusionRatioSlider);
				row.AddChild(extrusionValue);

				topToBottom.AddChild(row);
				
				textImageButtonFactory.FixedHeight = (int)extrusionValue.Height + 1;

				Button setButton = textImageButtonFactory.Generate("Set".Localize());
				setButton.VAnchor = VAnchor.ParentCenter;
				row.AddChild(setButton);
			}

			adjustmentControlsGroupBox.AddChild(topToBottom);

			this.AddChild(adjustmentControlsGroupBox);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				var eventArgs = e as StringEventArgs;
				if (eventArgs?.Data == SettingsKey.extrusion_ratio)
				{
					double extrusionRatio = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.extrusion_ratio);
					extrusionRatioSlider.Value = extrusionRatio;
					extrusionValue.ActuallNumberEdit.Value = Math.Round(extrusionRatio, 2);
				}
				else if (eventArgs?.Data == SettingsKey.feedrate_ratio)
				{
					double feedrateRatio = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.feedrate_ratio);
					feedRateRatioSlider.Value = feedrateRatio;
					feedRateValue.ActuallNumberEdit.Value = Math.Round(feedrateRatio, 2);
				}
			}, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}