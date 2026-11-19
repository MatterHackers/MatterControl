/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Library.Widgets.HardwarePage
{
    public class PrinterDetails : FlowLayoutWidget
	{
		private ThemeConfig theme;
		private GuiWidget headingRow;
		private PrinterInfo printerInfo;
		public GuiWidget ProductDataContainer { get; private set; }
		public event EventHandler AfterLoad;

		public PrinterDetails(PrinterInfo printerInfo, ThemeConfig theme, bool showOpenButton)
			: base(FlowDirection.TopToBottom)
		{
			this.printerInfo = printerInfo;
			this.theme = theme;

			headingRow = this.AddHeading(
				OemSettings.Instance.GetIcon(printerInfo.Make, theme),
				printerInfo.Name);

			headingRow.AddChild(new HorizontalSpacer());
			headingRow.HAnchor = HAnchor.Stretch;

			if (showOpenButton)
			{
				var openButton = new ThemedTextButton("Open".Localize(), theme)
				{
					BackgroundColor = theme.AccentMimimalOverlay,
					Margin = Margin.Clone(right: 17)
				};
				openButton.Click += (s, e) =>
				{
					ApplicationController.Instance.OpenPrinter(printerInfo);
				};
				headingRow.AddChild(openButton);
			}

			this.AddChild(headingRow);
		}

		public bool ShowHeadingRow { get; set; } = true;

		public override void OnLoad(EventArgs args)
		{
			// add a section to hold the data about the printer
			var scrollableWidget = new ScrollableWidget(true);
			scrollableWidget.ScrollArea.HAnchor |= HAnchor.Stretch;
			scrollableWidget.ScrollArea.VAnchor = VAnchor.Fit;
			scrollableWidget.AnchorAll();

			scrollableWidget.AddChild(ProductDataContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			});

			this.AddChild(scrollableWidget);

			AfterLoad?.Invoke(this, null);

			scrollableWidget.Width += 1;
			scrollableWidget.Width -= 1;

			scrollableWidget.TopLeftOffset = new Vector2(0, 0);

			headingRow.Visible = this.ShowHeadingRow;

			base.OnLoad(args);
		}

		private GuiWidget AddHeading(ImageBuffer icon, string text)
		{
			var row = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(top: 5)
			};

			row.AddChild(new ImageWidget(icon, false)
			{
				Margin = new BorderDouble(right: 4),
				VAnchor = VAnchor.Center
			});

			row.AddChild(
				new TextWidget(
					text,
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize)
					{
						VAnchor = VAnchor.Center
					});

			return row;
		}
	}
}
