/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl;
using MatterHackers.VectorMath;
using System;

namespace MatterControlLib.SetupWizard
{
	public class TourOverlay : GuiWidget
	{
		private GuiWidget targetWidget;
		private string MarkDown { get; }

		public TourOverlay(GuiWidget targetWidget, string markDown, ThemeConfig theme)
		{
			this.targetWidget = targetWidget;
			this.MarkDown = markDown;

			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;
		}

		public override void OnLoad(EventArgs args)
		{
			var contentBounds = GetContentBounds();

			var content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = new Vector2(contentBounds.Left, contentBounds.Bottom),
				Size = new Vector2(contentBounds.Width, contentBounds.Height),
				Padding = new BorderDouble(5),
				BackgroundColor = Color.White
			};

			this.AddChild(content);

			var scrollable = new ScrollableWidget(true)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch
			};

			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			content.AddChild(scrollable);

			scrollable.ScrollArea.Margin = new BorderDouble(0, 0, 15, 0);
			scrollable.AddChild(new WrappedTextWidget(MarkDown));

			base.OnLoad(args);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DoubleBuffer = true;
			var dimRegion = new VertexStorage();
			dimRegion.MoveTo(LocalBounds.Left, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Top);
			dimRegion.LineTo(LocalBounds.Left, LocalBounds.Top);

			var childBounds = GetChildBounds();

			var childRect = new VertexStorage();
			childRect.MoveTo(childBounds.Right, childBounds.Bottom);
			childRect.LineTo(childBounds.Left, childBounds.Bottom);
			childRect.LineTo(childBounds.Left, childBounds.Top);
			childRect.LineTo(childBounds.Right, childBounds.Top);

			var combine = new CombinePaths(dimRegion, childRect);
			//var combine = new CombinePaths(dimRegion, new ReversePath(round));

			graphics2D.Render(combine, new Color(Color.Black, 120));

			base.OnDraw(graphics2D);

			graphics2D.Render(new Stroke(new RoundedRect(GetChildBounds(), 3), 4), Color.Red);
			graphics2D.Render(new Stroke(new RoundedRect(GetContentBounds(), 3), 4), Color.Red);
		}

		private RectangleDouble GetContentBounds()
		{
			var childBounds = GetChildBounds();

			// depending on where the child is create the content next to it
			return new RectangleDouble(childBounds.Left, childBounds.Bottom - 100, childBounds.Left + 250, childBounds.Bottom);
		}

		private RectangleDouble GetChildBounds()
		{
			var childBounds = targetWidget.TransformToScreenSpace(targetWidget.LocalBounds);
			childBounds = this.TransformFromScreenSpace(childBounds);
			return childBounds;
		}
	}
}
