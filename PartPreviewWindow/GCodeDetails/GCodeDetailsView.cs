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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
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
