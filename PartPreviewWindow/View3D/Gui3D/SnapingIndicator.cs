/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SnappingIndicators : InteractionVolume
	{
		private double distToStart = 10;
		private double lineLength = 15;
		private Vector2[] lines = new Vector2[4];
		private View3DWidget view3DWidget;

		public SnappingIndicators(View3DWidget view3DWidget)
			: base(null, view3DWidget.meshViewerWidget)
		{
			this.view3DWidget = view3DWidget;
			this.DrawOnTop = true;
			MeshViewerToDrawWith.AfterDraw += MeshViewerToDrawWith_Draw;
		}

		public override void SetPosition()
		{
			if (MeshViewerToDrawWith.HaveSelection)
			{
				// draw the hight from the bottom to the bed
				AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();

				MeshSelectInfo meshSelectInfo = view3DWidget.CurrentSelectInfo;

				switch (meshSelectInfo.HitQuadrant)
				{
					case HitQuadrant.LB:
						{
							Vector3 cornerPoint = new Vector3(selectedBounds.minXYZ.x, selectedBounds.minXYZ.y, 0);
							double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

							lines[0] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
							lines[1] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

							lines[2] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
							lines[3] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
						}
						break;

					case HitQuadrant.LT:
						{
							Vector3 cornerPoint = new Vector3(selectedBounds.minXYZ.x, selectedBounds.maxXYZ.y, 0);
							double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

							lines[0] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
							lines[1] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

							lines[2] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
							lines[3] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
						}
						break;

					case HitQuadrant.RB:
						{
							Vector3 cornerPoint = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.minXYZ.y, 0);
							double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

							lines[0] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
							lines[1] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

							lines[2] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
							lines[3] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint - new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
						}
						break;

					case HitQuadrant.RT:
						{
							Vector3 cornerPoint = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.maxXYZ.y, 0);
							double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

							lines[0] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
							lines[1] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

							lines[2] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
							lines[3] = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(cornerPoint + new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
						}
						break;
				}
			}
		}

		private void MeshViewerToDrawWith_Draw(object drawingWidget, DrawEventArgs drawEvent)
		{
			if (MeshViewerToDrawWith.SelectedMeshGroup != null
				&& view3DWidget.meshViewerWidget.SnapGridDistance > 0
				&& view3DWidget.CurrentSelectInfo.DownOnPart)
			{
				if (drawEvent != null)
				{
					// draw the line that is on the ground
					drawEvent.graphics2D.Line(lines[0], lines[1], RGBA_Bytes.Red);
					drawEvent.graphics2D.Line(lines[2], lines[3], RGBA_Bytes.Red);
				}
			}
		}
	}
}