/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodePanel : FlowLayoutWidget
	{
		private ISceneContext sceneContext;
		private ThemeConfig theme;
		private PrinterConfig printer;
		private PrinterTabPage printerTabPage;
		private SectionWidget speedsWidget;
		private GuiWidget loadedGCodeSection;

		public GCodePanel(PrinterTabPage printerTabPage, PrinterConfig printer, ISceneContext sceneContext, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.sceneContext = sceneContext;
			this.theme = theme;
			this.printer = printer;
			this.printerTabPage = printerTabPage;

			SectionWidget sectionWidget;

			this.AddChild(
				sectionWidget = new SectionWidget(
					"Options".Localize(),
					new GCodeOptionsPanel(sceneContext, printer, theme),
					theme)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					Padding = 0
				});

			sectionWidget.ContentPanel.Descendants<SettingsRow>().First().Border = 0;

			var scrollable = new ScrollableWidget(true)
			{
				Name = "editorPanel",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;

			scrollable.AddChild(loadedGCodeSection = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			});

			this.AddChild(scrollable);

			this.RefreshGCodeDetails(printer);

			this.EnsureSectionWidgetStyling(this.Children<SectionWidget>());

			var firstSection = this.Children<SectionWidget>().First();
			firstSection.BorderColor = Color.Transparent; // Disable top border on first item to produce a more flat, dark top edge

			// Register listeners
			printer.Bed.LoadedGCodeChanged += Bed_LoadedGCodeChanged;
			printer.Bed.RendererOptions.PropertyChanged += RendererOptions_PropertyChanged;
		}

		private void RefreshGCodeDetails(PrinterConfig printer)
		{
			loadedGCodeSection.CloseAllChildren();

			if (sceneContext.LoadedGCode?.LineCount > 0)
			{
				bool renderSpeeds = printer.Bed.RendererOptions.GCodeLineColorStyle == "Speeds";
				loadedGCodeSection.AddChild(
					speedsWidget = new SectionWidget(
						"Speeds".Localize(),
						new SpeedsLegend(sceneContext.LoadedGCode, theme, printer)
						{
							HAnchor = HAnchor.Stretch,
							Visible = renderSpeeds,
							Padding = new BorderDouble(15, 4)
						},
						theme,
						serializationKey: "gcode_panel_speeds",
						expanded: true)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					});

				speedsWidget.Visible = renderSpeeds;

				// Single instance shared across widgets
				loadedGCodeSection.AddChild(
					new SectionWidget(
						"Details".Localize(),
						new GCodeDetailsView(printer.Bed.LoadedGCode, printer, theme)
						{
							HAnchor = HAnchor.Stretch,
							Margin = new BorderDouble(bottom: 3),
							Padding = new BorderDouble(15, 4)
						},
						theme,
						serializationKey: "gcode_panel_details",
						expanded: true)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					});

				loadedGCodeSection.AddChild(
					new SectionWidget(
						"Layer".Localize(),
						new GCodeLayerDetailsView(printer.Bed.LoadedGCode, sceneContext, theme)
						{
							HAnchor = HAnchor.Stretch,
							Margin = new BorderDouble(bottom: 3),
							Padding = new BorderDouble(15, 4)
						},
						theme,
						serializationKey: "gcode_panel_layer_details",
						expanded: true)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					});

				SectionWidget lineInspectorWidget;

				loadedGCodeSection.AddChild(
					lineInspectorWidget = new SectionWidget(
						"Line Inspector".Localize(),
						new GCodeDebugView(printerTabPage, printer.Bed.LoadedGCode, sceneContext, theme)
						{
							HAnchor = HAnchor.Stretch,
							Margin = new BorderDouble(bottom: 3),
							Padding = new BorderDouble(15, 4)
						},
						theme,
						serializationKey: "gcode_panel_line_inspector",
						expanded: false)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					});

				lineInspectorWidget.ExpandedChanged += (s, sectionVisible) =>
				{
					sceneContext.GCodeRenderer.GCodeInspector = sectionVisible;
				};

				sceneContext.GCodeRenderer.GCodeInspector = lineInspectorWidget.ContentPanel.Visible;
			}

			// Enforce panel padding in sidebar
			this.EnsureSectionWidgetStyling(loadedGCodeSection.Children<SectionWidget>());

			this.Invalidate();
		}

		private void EnsureSectionWidgetStyling(IEnumerable<SectionWidget> sectionWidgets)
		{
			foreach (var sectionWidget in sectionWidgets)
			{
				var contentPanel = sectionWidget.ContentPanel;
				var firstItem = contentPanel.Children<SettingsItem>().FirstOrDefault();
				if (firstItem != null)
				{
					firstItem.Border = firstItem.Border.Clone(top: 1);
					contentPanel.Padding = new BorderDouble(10, 0, 10, 0);
					contentPanel.Margin = contentPanel.Margin.Clone(bottom: 4);
				}
				else
				{
					contentPanel.Padding = new BorderDouble(10, 10, 10, 0);
				}

				var lastItem = contentPanel.Children<SettingsItem>().LastOrDefault();
				if (lastItem != null)
				{
					lastItem.Border = lastItem.Border.Clone(bottom: 0);
				}
			}
		}

		private void Bed_LoadedGCodeChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() => this.RefreshGCodeDetails(printer));
		}

		private void RendererOptions_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (speedsWidget != null
				&& e.PropertyName == "GCodeLineColorStyle")
			{
				speedsWidget.Visible = printer.Bed.RendererOptions.GCodeLineColorStyle == "Speeds";
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Bed.RendererOptions.PropertyChanged -= RendererOptions_PropertyChanged;
			printer.Bed.LoadedGCodeChanged -= Bed_LoadedGCodeChanged;

			base.OnClosed(e);
		}
	}
}
