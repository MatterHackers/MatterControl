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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class HeightValueDisplay : GuiWidget
	{
		private static readonly int HorizontalLineLength = 30;
		private View3DWidget view3DWidget;
		ValueDisplayInfo heightValueDisplayInfo = new ValueDisplayInfo();

		public HeightValueDisplay(View3DWidget view3DWidget)
		{
			BackgroundColor = new RGBA_Bytes(RGBA_Bytes.White, 150);
			this.view3DWidget = view3DWidget;
			view3DWidget.meshViewerWidget.AddChild(this);
			VAnchor = VAnchor.FitToChildren;
			HAnchor = HAnchor.FitToChildren;

			MeshViewerToDrawWith.AfterDraw += new DrawEventHandler(MeshViewerToDrawWith_Draw);
		}

		private MeshViewerWidget MeshViewerToDrawWith { get { return view3DWidget.meshViewerWidget; } }

		private void MeshViewerToDrawWith_Draw(GuiWidget drawingWidget, DrawEventArgs drawEvent)
		{
			if (Visible)
			{
				if (drawEvent != null)
				{
					Vector2 startLineGroundPos = Vector2.Zero;
					Vector2 startLineSelectionPos = Vector2.Zero;
					Vector2 midLinePos = Vector2.Zero;

					if (MeshViewerToDrawWith.Scene.HasSelection)
					{
						// draw the hight from the bottom to the bed
						AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.Scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

						Vector2 screenPosition = new Vector2(-100, 0);
						Vector3[] bottomPoints = new Vector3[4];
						bottomPoints[0] = new Vector3(selectedBounds.minXYZ.x, selectedBounds.minXYZ.y, selectedBounds.minXYZ.z);
						bottomPoints[1] = new Vector3(selectedBounds.minXYZ.x, selectedBounds.maxXYZ.y, selectedBounds.minXYZ.z);
						bottomPoints[2] = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.minXYZ.y, selectedBounds.minXYZ.z);
						bottomPoints[3] = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.maxXYZ.y, selectedBounds.minXYZ.z);

						for (int i = 0; i < 4; i++)
						{
							Vector2 testScreenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomPoints[i]);
							if (testScreenPosition.x > screenPosition.x)
							{
								startLineSelectionPos = testScreenPosition;
								startLineGroundPos = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomPoints[i] + new Vector3(0, 0, -bottomPoints[i].z));
								midLinePos = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomPoints[i] + new Vector3(0, 0, -bottomPoints[i].z/2));
								screenPosition = testScreenPosition + new Vector2(HorizontalLineLength, 0);
							}
						}
						heightValueDisplayInfo.DisplaySizeInfo(drawEvent.graphics2D, midLinePos, selectedBounds.minXYZ.z);


						OriginRelativeParent = screenPosition;

						// draw the line that is on the ground
						double yGround = (int)(startLineGroundPos.y + .5) + .5;
						drawEvent.graphics2D.Line(startLineGroundPos.x, yGround, startLineGroundPos.x + HorizontalLineLength - 5, yGround, RGBA_Bytes.Black);
						// and the line that is at the base of the selection
						double ySelection = Math.Round(startLineSelectionPos.y) + .5;
						drawEvent.graphics2D.Line(startLineSelectionPos.x, ySelection, startLineSelectionPos.x + HorizontalLineLength - 5, ySelection, RGBA_Bytes.Black);

						// draw the vertical line that shows the measurement
						Vector2 pointerBottom = new Vector2(startLineGroundPos.x + HorizontalLineLength / 2, yGround);
						Vector2 pointerTop = new Vector2(startLineSelectionPos.x + HorizontalLineLength / 2, ySelection);

						InteractionVolume.DrawMeasureLine(drawEvent.graphics2D, pointerBottom, pointerTop, RGBA_Bytes.Black, InteractionVolume.LineArrows.End);
					}
				}
			}
		}
	}
}