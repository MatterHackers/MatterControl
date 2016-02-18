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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectionShadow : InteractionVolume
	{
		private View3DWidget view3DWidget;

		public SelectionShadow(View3DWidget view3DWidget)
			: base(null, view3DWidget.meshViewerWidget)
		{
			this.view3DWidget = view3DWidget;
		}

		public override void SetPosition()
		{
			AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();
			Vector3 boundsCenter = selectedBounds.Center;

			TotalTransform = Matrix4X4.CreateTranslation(new Vector3(boundsCenter.x, boundsCenter.y, 0.1));
		}

		public override void DrawGlContent(EventArgs e)
		{
			if (MeshViewerToDrawWith.SelectedMeshGroup != null)
			{
				// draw the bounds on the bed
				AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();

				Mesh bottomBounds = PlatonicSolids.CreateCube(selectedBounds.XSize, selectedBounds.YSize, .1);
				RenderMeshToGl.Render(bottomBounds, new RGBA_Bytes(22, 80, 220, 30), TotalTransform, RenderTypes.Shaded);

				if (false)// view3DWidget.DragingPart)
				{
					Vector3 lastHitPosition = view3DWidget.LastHitPosition;
					double lineWidth = .5;
					if (lastHitPosition.x < selectedBounds.Center.x)
					{
						Mesh leftSide = PlatonicSolids.CreateCube(lineWidth, selectedBounds.YSize, .1);
						Matrix4X4 leftTransform = Matrix4X4.CreateTranslation(new Vector3(selectedBounds.minXYZ.x, selectedBounds.Center.y, 0.1));
						RenderMeshToGl.Render(leftSide, new RGBA_Bytes(222, 80, 20), leftTransform, RenderTypes.Shaded);
					}
					else
					{
						Mesh rightSide = PlatonicSolids.CreateCube(lineWidth, selectedBounds.YSize, .1);
						Matrix4X4 rightTransform = Matrix4X4.CreateTranslation(new Vector3(selectedBounds.maxXYZ.x, selectedBounds.Center.y, 0.1));
						RenderMeshToGl.Render(rightSide, new RGBA_Bytes(222, 80, 20), rightTransform, RenderTypes.Shaded);
					}
					if (lastHitPosition.y < selectedBounds.Center.y)
					{
						Mesh bottomSide = PlatonicSolids.CreateCube(selectedBounds.XSize, lineWidth, .1);
						Matrix4X4 bottomTransform = Matrix4X4.CreateTranslation(new Vector3(selectedBounds.Center.x, selectedBounds.minXYZ.y, 0.1));
						RenderMeshToGl.Render(bottomSide, new RGBA_Bytes(222, 80, 20), bottomTransform, RenderTypes.Shaded);
					}
					else
					{
						Mesh topSide = PlatonicSolids.CreateCube(selectedBounds.XSize, lineWidth, .1);
						Matrix4X4 topTransform = Matrix4X4.CreateTranslation(new Vector3(selectedBounds.Center.x, selectedBounds.maxXYZ.y, 0.1));
						RenderMeshToGl.Render(topSide, new RGBA_Bytes(222, 80, 20), topTransform, RenderTypes.Shaded);
					}
				}
			}

			base.DrawGlContent(e);
		}
	}
}