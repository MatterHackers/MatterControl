/*
Copyright (c) 2016, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.Globalization;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class WrappedQueueRowItem : FlowLayoutWidget
	{
		private bool mouseDownInBounds = false;

		private QueueDataView queueDataView;
		private QueueRowItem queueRowItem;

		public WrappedQueueRowItem(QueueDataView parent, PrintItemWrapper printItem)
		{
			Name = "PrintQueueControl itemHolder";
			Margin = new BorderDouble(0, 0, 0, 0);
			HAnchor = HAnchor.ParentLeftRight;
			VAnchor = VAnchor.FitToChildren;

			this.queueDataView = parent;
			this.queueRowItem = new QueueRowItem(printItem, parent);

			AddChild(this.queueRowItem);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			queueRowItem.IsHoverItem = true;

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			queueRowItem.IsHoverItem = false;
			base.OnMouseLeaveBounds(mouseEvent);
		}


		Vector2 mouseDownAt;
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseDownAt = mouseEvent.Position;
			mouseDownInBounds = true;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var delta = mouseDownAt - mouseEvent.Position;
			if (mouseDownInBounds && delta.Length > 50)
			{
				// Set the QueueRowItem child as the DragSourceRowItem for use in drag/drop
				queueDataView.DragSourceRowItem = queueRowItem;
			}

			base.OnMouseMove(mouseEvent);
		}


		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			// If a valid click event occurs then set the selected index in our parent
			if (mouseDownInBounds &&
				mouseEvent.X > 56 && // Disregard clicks within the thumbnail region (x < 56)
				PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				QueueData.Instance.ToggleSelect(queueRowItem.PrintItemWrapper);
			}

			mouseDownInBounds = false;
			base.OnMouseUp(mouseEvent);
		}
	}
}