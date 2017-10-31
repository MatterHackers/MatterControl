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
using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SnappingIndicators : InteractionVolume
	{
		private double distToStart = 10;
		private double lineLength = 15;
		private Vector2[] lines = new Vector2[4];

		private MeshSelectInfo meshSelectInfo;

		public SnappingIndicators(IInteractionVolumeContext context, MeshSelectInfo currentSelectInfo)
			: base(context)
		{
			this.DrawOnTop = true;
			this.meshSelectInfo = currentSelectInfo;
			InteractionContext.GuiSurface.AfterDraw += InteractionLayer_AfterDraw;
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			// draw the hight from the bottom to the bed
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

			var world = InteractionContext.World;

			switch (meshSelectInfo.HitQuadrant)
			{
				case HitQuadrant.LB:
					{
						Vector3 cornerPoint = new Vector3(selectedBounds.minXYZ.X, selectedBounds.minXYZ.Y, 0);
						double distBetweenPixelsWorldSpace = world.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

						lines[0] = world.GetScreenPosition(cornerPoint - new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
						lines[1] = world.GetScreenPosition(cornerPoint - new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

						lines[2] = world.GetScreenPosition(cornerPoint - new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
						lines[3] = world.GetScreenPosition(cornerPoint - new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
					}
					break;

				case HitQuadrant.LT:
					{
						Vector3 cornerPoint = new Vector3(selectedBounds.minXYZ.X, selectedBounds.maxXYZ.Y, 0);
						double distBetweenPixelsWorldSpace = world.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

						lines[0] = world.GetScreenPosition(cornerPoint - new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
						lines[1] = world.GetScreenPosition(cornerPoint - new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

						lines[2] = world.GetScreenPosition(cornerPoint + new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
						lines[3] = world.GetScreenPosition(cornerPoint + new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
					}
					break;

				case HitQuadrant.RB:
					{
						Vector3 cornerPoint = new Vector3(selectedBounds.maxXYZ.X, selectedBounds.minXYZ.Y, 0);
						double distBetweenPixelsWorldSpace = world.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

						lines[0] = world.GetScreenPosition(cornerPoint + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
						lines[1] = world.GetScreenPosition(cornerPoint + new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

						lines[2] = world.GetScreenPosition(cornerPoint - new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
						lines[3] = world.GetScreenPosition(cornerPoint - new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
					}
					break;

				case HitQuadrant.RT:
					{
						Vector3 cornerPoint = new Vector3(selectedBounds.maxXYZ.X, selectedBounds.maxXYZ.Y, 0);
						double distBetweenPixelsWorldSpace = world.GetWorldUnitsPerScreenPixelAtPosition(cornerPoint);

						lines[0] = world.GetScreenPosition(cornerPoint + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0));
						lines[1] = world.GetScreenPosition(cornerPoint + new Vector3((distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0));

						lines[2] = world.GetScreenPosition(cornerPoint + new Vector3(0, distToStart * distBetweenPixelsWorldSpace, 0));
						lines[3] = world.GetScreenPosition(cornerPoint + new Vector3(0, (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0));
					}
					break;
			}
		}

		private void InteractionLayer_AfterDraw(object drawingWidget, DrawEventArgs drawEvent)
		{
			if (InteractionContext.Scene.HasSelection
				&& InteractionContext.SnapGridDistance > 0
				&& meshSelectInfo.DownOnPart)
			{
				if (drawEvent != null)
				{
					// draw the line that is on the ground
					drawEvent.graphics2D.Line(lines[0], lines[1], Color.Red);
					drawEvent.graphics2D.Line(lines[2], lines[3], Color.Red);
				}
			}
		}
	}
}