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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCode3DWidget : GuiWidget
	{
		private EventHandler unregisterEvents;

		private BedConfig sceneContext;
		private ThemeConfig theme;

		public GCode3DWidget(PrinterConfig printer, BedConfig sceneContext, ThemeConfig theme)
		{
			this.sceneContext = sceneContext;
			this.theme = theme;

			CreateAndAddChildren(printer);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == "extruder_offset")
					{
						printer.Bed.GCodeRenderer?.Clear3DGCode();
					}
				}
			}, ref unregisterEvents);
		}

		internal void CreateAndAddChildren(PrinterConfig printer)
		{
			this.CloseAllChildren();

			if (sceneContext.LoadedGCode?.LineCount > 0)
			{
				var gcodeResultsPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(0, 0, 35, 0),
					Padding = new BorderDouble(10, 10, 10, 8),
					BackgroundColor = theme.InteractionLayerOverlayColor,
					HAnchor = HAnchor.Absolute | HAnchor.Right,
					VAnchor = VAnchor.Top | VAnchor.Fit,
					Width = 175
				};
				this.AddChild(gcodeResultsPanel);

				gcodeResultsPanel.AddChild(
					new SectionWidget(
						"Details".Localize(),
						ActiveTheme.Instance.PrimaryTextColor,
						new GCodeDetailsView(new GCodeDetails(printer, printer.Bed.LoadedGCode), theme.FontSize12, theme.FontSize9)
						{
							HAnchor = HAnchor.Fit,
							Margin = new BorderDouble(bottom: 3)
						}));

				gcodeResultsPanel.AddChild(
					new SectionWidget(
						"Speeds".Localize(),
						ActiveTheme.Instance.PrimaryTextColor,
						new SpeedsLegend(sceneContext.LoadedGCode, theme, pointSize: theme.FontSize12)
						{
							HAnchor = HAnchor.Stretch,
							Visible = sceneContext.RendererOptions.RenderSpeeds,
						}));
			}

			this.Invalidate();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
