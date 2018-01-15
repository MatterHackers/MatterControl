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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrintPopupMenu : PopupMenuButton
	{
		private TextImageButtonFactory buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;
		private PrinterConfig printer;
		private PrinterTabPage printerTabPage;

		public PrintPopupMenu(PrinterConfig printer, ThemeConfig theme, PrinterTabPage printerTabPage)
		{
			this.printerTabPage = printerTabPage;
			this.printer = printer;
			this.DrawArrow = true;
			this.BackgroundColor = theme.ButtonFactory.Options.NormalFillColor;
			//this.HoverColor = theme.ButtonFactory.Options.HoverFillColor;
			this.Name = "PrintPopupMenu";
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.DynamicPopupContent = () =>
			{
				int tabIndex = 0;

				var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					BackgroundColor = new Color(245, 245, 245),
					Padding = 10
				};

				column.AddChild(new TextWidget("Options".Localize())
				{
					HAnchor = HAnchor.Left
				});

				var optionsPanel = new IgnoredFlowLayout()
				{
					Name = "PrintPopupMenu Panel",
					HAnchor = HAnchor.Fit | HAnchor.Left,
					VAnchor = VAnchor.Fit,
					Padding = 5,
					MinimumSize = new VectorMath.Vector2(400, 65),
					Margin = new BorderDouble(left: 8)
				};
				column.AddChild(optionsPanel);

				var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);

				foreach (var key in new[] { "layer_height", "fill_density", "support_material", "create_raft", "spiral_vase", "layer_to_pause" })
				{
					var settingsData = SettingsOrganizer.Instance.GetSettingsData(key);
					var row = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, Color.Black, theme, ref tabIndex);

					optionsPanel.AddChild(row);
				}

				var button = new TextButton("Start Print".Localize(), theme, Color.Black)
				{
					Name = "Start Print Button",
					HAnchor = HAnchor.Right,
					VAnchor = VAnchor.Absolute,
					Margin = new BorderDouble(top: 10),
					BackgroundColor = theme.MinimalShade
				};
				button.Click += (s, e) =>
				{
					UiThread.RunOnIdle(async () =>
					{
						// Save any pending changes before starting print operation
						await ApplicationController.Instance.Tasks.Execute(printerTabPage.view3DWidget.SaveChanges);

						var context = printer.Bed.EditContext;
						await ApplicationController.Instance.PrintPart(
							context.PartFilePath,
							context.GCodeFilePath,
							context.SourceItem.Name,
							printer,
							null,
							CancellationToken.None);
					});
				};
				button.EnabledChanged += (s, e) => Console.WriteLine();
				column.AddChild(button);

				return column;
			};

			this.AddChild(new TextButton("Print".Localize(), theme)
			{
				Selectable = false,
				Padding = theme.ButtonFactory.Options.Margin.Clone(right: 5)
			});
		}

		private class IgnoredFlowLayout : FlowLayoutWidget, IIgnoredPopupChild
		{
			public IgnoredFlowLayout()
				: base(FlowDirection.TopToBottom)
			{
			}
		}
	}
}