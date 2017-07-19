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
using System.Threading;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DragDropLoadProgress
	{
		private View3DWidget view3DWidget;
		private ProgressBar progressBar;

		public DragDropLoadProgress(View3DWidget view3DWidget, IObject3D trackingObject)
		{
			this.TrackingObject = trackingObject;
			this.view3DWidget = view3DWidget;
			view3DWidget.AfterDraw += View3DWidget_AfterDraw;
			progressBar = new ProgressBar(80, 15)
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
			};
		}

		public IObject3D TrackingObject { get; set; }

		private void View3DWidget_AfterDraw(object sender, DrawEventArgs e)
		{
			if (view3DWidget?.HasBeenClosed == false && this.TrackingObject != null)
			{
				AxisAlignedBoundingBox bounds = TrackingObject.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				Vector3 renderPosition = bounds.Center;
				Vector2 cornerScreenSpace = view3DWidget.World.GetScreenPosition(renderPosition) - new Vector2(40, 20);

				e.graphics2D.PushTransform();
				Affine currentGraphics2DTransform = e.graphics2D.GetTransform();
				Affine accumulatedTransform = currentGraphics2DTransform * Affine.NewTranslation(cornerScreenSpace.x, cornerScreenSpace.y);
				e.graphics2D.SetTransform(accumulatedTransform);

				progressBar.OnDraw(e.graphics2D);
				e.graphics2D.PopTransform();
			}
		}

		public void ProgressReporter(double progress0To1, string processingState)
		{
			progressBar.RatioComplete = progress0To1;
			view3DWidget?.Invalidate();

			if (progress0To1 == 1)
			{
				if (view3DWidget != null)
				{
					view3DWidget.AfterDraw -= View3DWidget_AfterDraw;
				}

				view3DWidget = null;
			}
		}
	}
}