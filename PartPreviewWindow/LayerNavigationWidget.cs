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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class LayerNavigationWidget : FlowLayoutWidget
	{
		private TextWidget layerCountTextWidget;
		private ViewGcodeWidget gcodeViewWidget;

		private PrinterConfig printer;

		public LayerNavigationWidget(ViewGcodeWidget gcodeViewWidget, TextImageButtonFactory buttonFactory)
			: base(FlowDirection.LeftToRight)
		{
			this.gcodeViewWidget = gcodeViewWidget;

			printer = ApplicationController.Instance.Printer;

			var prevLayerButton = buttonFactory.Generate("<<");
			prevLayerButton.Click += (s, e) =>
			{
				gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex - 1);
			};
			this.AddChild(prevLayerButton);

			layerCountTextWidget = new TextWidget("/1____", 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter,
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(5, 0)
			};
			this.AddChild(layerCountTextWidget);

			var nextLayerButton = buttonFactory.Generate(">>");
			nextLayerButton.Click += (s, e) =>
			{
				gcodeViewWidget.ActiveLayerIndex = (gcodeViewWidget.ActiveLayerIndex + 1);
			};
			this.AddChild(nextLayerButton);
		}

		
		public override void OnDraw(Graphics2D graphics2D)
		{
			if (printer.BedPlate.LoadedGCode != null)
			{
				layerCountTextWidget.Text = string.Format("{0} / {1}", gcodeViewWidget.ActiveLayerIndex + 1, printer.BedPlate.LoadedGCode.NumChangesInZ.ToString());
			}

			base.OnDraw(graphics2D);
		}
	}
}
