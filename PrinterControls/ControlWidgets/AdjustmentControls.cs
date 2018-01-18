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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class AdjustmentControls : FlowLayoutWidget
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

		private AdjustmentControls(PrinterConfig printer, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			double sliderWidth = 300 * GuiWidget.DeviceScale;
			double sliderThumbWidth = 10 * GuiWidget.DeviceScale;

			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Stretch,
					Margin = 0,
					VAnchor = VAnchor.Fit
				};

				var feedRateDescription = new TextWidget("Speed Multiplier".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor)
				{
					MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale,
					VAnchor = VAnchor.Center,
				};
				row.AddChild(feedRateDescription);

				feedRateRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minFeedRateRatio, maxFeedRateRatio)
				{
					Name = "Feed Rate Slider",
					Margin = new BorderDouble(5, 0),
					Value = FeedRateMultiplyerStream.FeedRateRatio,
					HAnchor = HAnchor.Stretch,
					TotalWidthInPixels = sliderWidth,
				};
				feedRateRatioSlider.ValueChanged += (sender, e) =>
				{
					feedRateValue.ActuallNumberEdit.Value = Math.Round(feedRateRatioSlider.Value, 2);
				};
				feedRateRatioSlider.SliderReleased += (s, e) =>
				{
					// Update state for runtime use
					FeedRateMultiplyerStream.FeedRateRatio = Math.Round(feedRateRatioSlider.Value, 2);

					// Persist data for future use
					printer.Settings.SetValue(
						SettingsKey.feedrate_ratio,
						FeedRateMultiplyerStream.FeedRateRatio.ToString());
				};
				row.AddChild(feedRateRatioSlider);

				feedRateValue = new MHNumberEdit(Math.Round(FeedRateMultiplyerStream.FeedRateRatio, 2), allowDecimals: true, minValue: minFeedRateRatio, maxValue: maxFeedRateRatio, pixelWidth: 40 * GuiWidget.DeviceScale)
				{
					Name = "Feed Rate NumberEdit",
					SelectAllOnFocus = true,
					Margin = new BorderDouble(0, 0, 5, 0),
					VAnchor = VAnchor.Center | VAnchor.Fit,
					Padding = 0
				};
				feedRateValue.ActuallNumberEdit.EditComplete += (sender, e) =>
				{
					feedRateRatioSlider.Value = feedRateValue.ActuallNumberEdit.Value;

					// Update state for runtime use
					FeedRateMultiplyerStream.FeedRateRatio = Math.Round(feedRateRatioSlider.Value, 2);

					// Persist data for future use
					printer.Settings.SetValue(
						SettingsKey.feedrate_ratio,
						FeedRateMultiplyerStream.FeedRateRatio.ToString());
				};
				row.AddChild(feedRateValue);

				this.AddChild(row);
			}

			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(top: 10),
					VAnchor = VAnchor.Fit
				};

				var extrusionDescription = new TextWidget("Extrusion Multiplier".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor)
				{
					MinimumSize = new Vector2(140, 0) * GuiWidget.DeviceScale,
					VAnchor = VAnchor.Center
				};
				row.AddChild(extrusionDescription);

				extrusionRatioSlider = new SolidSlider(new Vector2(), sliderThumbWidth, minExtrutionRatio, maxExtrusionRatio, Orientation.Horizontal)
				{
					Name = "Extrusion Multiplier Slider",
					TotalWidthInPixels = sliderWidth,
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(5, 0),
					Value = ExtrusionMultiplyerStream.ExtrusionRatio
				};
				extrusionRatioSlider.BackgroundColor = new Color();
				extrusionRatioSlider.ValueChanged += (sender, e) =>
				{
					extrusionValue.ActuallNumberEdit.Value = Math.Round(extrusionRatioSlider.Value, 2);
				};
				extrusionRatioSlider.SliderReleased += (s, e) =>
				{
					// Update state for runtime use
					ExtrusionMultiplyerStream.ExtrusionRatio = Math.Round(extrusionRatioSlider.Value, 2);

					// Persist data for future use
					printer.Settings.SetValue(
						SettingsKey.extrusion_ratio,
						ExtrusionMultiplyerStream.ExtrusionRatio.ToString());
				};

				extrusionValue = new MHNumberEdit(Math.Round(ExtrusionMultiplyerStream.ExtrusionRatio, 2), allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40 * GuiWidget.DeviceScale)
				{
					Name = "Extrusion Multiplier NumberEdit",
					SelectAllOnFocus = true,
					Margin = new BorderDouble(0, 0, 5, 0),
					VAnchor = VAnchor.Center | VAnchor.Fit,
					Padding = 0
				};
				extrusionValue.ActuallNumberEdit.EditComplete += (sender, e) =>
				{
					extrusionRatioSlider.Value = extrusionValue.ActuallNumberEdit.Value;

					// Update state for runtime use
					ExtrusionMultiplyerStream.ExtrusionRatio = Math.Round(extrusionRatioSlider.Value, 2);

					// Persist data for future use
					printer.Settings.SetValue(
						SettingsKey.extrusion_ratio,
						ExtrusionMultiplyerStream.ExtrusionRatio.ToString());
				};
				row.AddChild(extrusionRatioSlider);
				row.AddChild(extrusionValue);

				this.AddChild(row);
			}

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				var eventArgs = e as StringEventArgs;
				if (eventArgs?.Data == SettingsKey.extrusion_ratio)
				{
					double extrusionRatio = printer.Settings.GetValue<double>(SettingsKey.extrusion_ratio);
					extrusionRatioSlider.Value = extrusionRatio;
					extrusionValue.ActuallNumberEdit.Value = Math.Round(extrusionRatio, 2);
				}
				else if (eventArgs?.Data == SettingsKey.feedrate_ratio)
				{
					double feedrateRatio = printer.Settings.GetValue<double>(SettingsKey.feedrate_ratio);
					feedRateRatioSlider.Value = feedrateRatio;
					feedRateValue.ActuallNumberEdit.Value = Math.Round(feedrateRatio, 2);
				}
			}, ref unregisterEvents);
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			return new SectionWidget(
				"Tuning Adjustment".Localize(),
				new AdjustmentControls(printer, theme),
				theme);
		}

		public override void OnLoad(EventArgs args)
		{
			// This is a hack to fix the layout issue this control is having.
			Width = Width + 1;
			base.OnLoad(args);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}