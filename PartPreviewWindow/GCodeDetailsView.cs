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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeDetailsView : FlowLayoutWidget
	{
		private TextWidget massTextWidget;
		private TextWidget costTextWidget;

		// Cost info is only displayed when available - conditionalCostPanel is invisible when cost <= 0
		private GuiWidget conditionalCostPanel;

		private EventHandler unregisterEvents;

		public GCodeDetailsView(GCodeDetails gcodeDetails)
			: base(FlowDirection.TopToBottom)
		{
			// put in the print time
			this.AddChild(new TextWidget("Print Time".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			this.AddChild(new TextWidget(gcodeDetails.EstimatedPrintTime, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 14)
			{
				Margin = new BorderDouble(0, 9, 0, 3)
			});

			// show the filament used
			this.AddChild(new TextWidget("Filament Length".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			this.AddChild(new TextWidget(gcodeDetails.FilamentUsed, pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 9, 0, 3)
			});

			this.AddChild(new TextWidget("Filament Volume".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			this.AddChild(new TextWidget(gcodeDetails.FilamentVolume, pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 9, 0, 3)
			});

			this.AddChild(new TextWidget("Estimated Mass".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));

			this.AddChild(massTextWidget = new TextWidget(gcodeDetails.EstimatedMass, pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 9, 0, 3)
			});

			conditionalCostPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = gcodeDetails.TotalCost > 0
			};

			conditionalCostPanel.AddChild(new TextWidget("Estimated Cost".Localize() + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 9));
			conditionalCostPanel.AddChild(costTextWidget = new TextWidget(gcodeDetails.EstimatedCost, pointSize: 14, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(top: 3)
			});

			this.AddChild(conditionalCostPanel);

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
