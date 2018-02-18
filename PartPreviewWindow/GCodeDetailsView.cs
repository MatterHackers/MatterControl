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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{

	public interface IToggleOption
	{
		string Title { get; }
		Func<bool> IsChecked { get; }
		Action<bool> SetValue { get; }
	}

	public class BoolOption : IToggleOption
	{
		public string Title { get; }

		public Func<bool> IsChecked { get; }

		public Func<bool> IsVisible { get; }

		public Action<bool> SetValue { get; }


		public BoolOption(string title, Func<bool> isChecked, Action<bool> setValue)
			: this(title, isChecked, setValue, () => true)
		{
		}

		public BoolOption(string title, Func<bool> isChecked, Action<bool> setValue, Func<bool> isVisible)
		{
			this.Title = title;
			this.IsChecked = isChecked;
			this.SetValue = setValue;
			this.IsVisible = isVisible;
		}
	}

	public class GCodeOptionsPanel : FlowLayoutWidget
	{
		public GCodeOptionsPanel(BedConfig sceneContext, PrinterConfig printer)
			: base(FlowDirection.TopToBottom)
		{
			var gcodeOptions = sceneContext.RendererOptions;

			var viewOptions = new[]
			{
				new BoolOption(
					"Show Print Bed".Localize(),
					() => gcodeOptions.RenderBed,
					(value) =>
					{
						gcodeOptions.RenderBed = value;
					}),
				new BoolOption(
					"Moves".Localize(),
					() => gcodeOptions.RenderMoves,
					(value) => gcodeOptions.RenderMoves = value),
				new BoolOption(
					"Retractions".Localize(),
					() => gcodeOptions.RenderRetractions,
					(value) => gcodeOptions.RenderRetractions = value),
				new BoolOption(
					"Extrusion".Localize(),
					() => gcodeOptions.SimulateExtrusion,
					(value) => gcodeOptions.SimulateExtrusion = value),
				new BoolOption(
					"Transparent".Localize(),
					() => gcodeOptions.TransparentExtrusion,
					(value) => gcodeOptions.TransparentExtrusion = value),
				new BoolOption(
					"Hide Offsets".Localize(),
					() => gcodeOptions.HideExtruderOffsets,
					(value) => gcodeOptions.HideExtruderOffsets = value,
					() => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1),
				new BoolOption(
					"Sync To Print".Localize(),
					() => gcodeOptions.SyncToPrint,
					(value) =>
					{
						gcodeOptions.SyncToPrint = value;
						if (!gcodeOptions.SyncToPrint)
						{
							// If we are turning off sync to print, set the slider to full.
							//layerRenderRatioSlider.SecondValue = 1;
						}
					})
			};

			foreach(var option in viewOptions)
			{
				if (option.IsVisible())
				{
					this.AddChild(
						new SettingsItem(
							option.Title,
							new SettingsItem.ToggleSwitchConfig()
							{
								Checked = option.IsChecked(),
								ToggleAction = option.SetValue
							},
							enforceGutter: false)
					);
				}
			}
		}
	}

	public class GCodeDetailsView : FlowLayoutWidget
	{
		private TextWidget massTextWidget;
		private TextWidget costTextWidget;

		private EventHandler unregisterEvents;

		public GCodeDetailsView(GCodeDetails gcodeDetails, int dataPointSize, int headingPointSize)
			: base(FlowDirection.TopToBottom)
		{
			var margin = new BorderDouble(0, 9, 0, 3);

			TextWidget AddSetting(string title, string value, GuiWidget parentWidget)
			{
				parentWidget.AddChild(
					new TextWidget(title + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: headingPointSize)
					{
						HAnchor = HAnchor.Left
					});

				var textWidget = new TextWidget(value, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: dataPointSize)
				{
					HAnchor = HAnchor.Left,
					Margin = margin
				};

				parentWidget.AddChild(textWidget);

				return textWidget;
			}

			// put in the print time
			AddSetting("Print Time".Localize(), gcodeDetails.EstimatedPrintTime, this);

			// show the filament used
			AddSetting("Filament Length".Localize(), gcodeDetails.FilamentUsed, this);

			AddSetting("Filament Volume".Localize(), gcodeDetails.FilamentVolume, this);

			massTextWidget = AddSetting("Estimated Mass".Localize(), gcodeDetails.EstimatedMass, this);

			// Cost info is only displayed when available - conditionalCostPanel is invisible when cost <= 0
			var conditionalCostPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = gcodeDetails.TotalCost > 0
			};
			this.AddChild(conditionalCostPanel);

			costTextWidget = AddSetting("Estimated Cost".Localize(), gcodeDetails.EstimatedCost, conditionalCostPanel);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == SettingsKey.filament_cost
						|| stringEvent.Data == SettingsKey.filament_diameter
						|| stringEvent.Data == SettingsKey.filament_density)
					{
						massTextWidget.Text = gcodeDetails.EstimatedMass;
						conditionalCostPanel.Visible = gcodeDetails.TotalCost > 0;

						if (gcodeDetails.TotalCost > 0)
						{
							costTextWidget.Text = gcodeDetails.EstimatedCost;
						}
					}
				}
			}, ref unregisterEvents);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
