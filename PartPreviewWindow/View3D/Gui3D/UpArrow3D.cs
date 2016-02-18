﻿/*
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
	public class UpArrow3D : InteractionVolume
	{
		private Mesh upArrow;
		private double zHitHeight;
		private Vector3 lastMoveDelta;
		private PlaneShape hitPlane;
		private View3DWidget view3DWidget;
		internal HeightValueDisplay heightDisplay;

		public UpArrow3D(View3DWidget view3DWidget)
			: base(null, view3DWidget.meshViewerWidget)
		{
			heightDisplay = new HeightValueDisplay(view3DWidget);
			heightDisplay.Visible = false;

			DrawOnTop = true;

			this.view3DWidget = view3DWidget;
			string arrowFile = Path.Combine("Icons", "3D Icons", "up_pointer.stl");
			if (StaticData.Instance.FileExists(arrowFile))
			{
				using (Stream staticDataStream = StaticData.Instance.OpenSteam(arrowFile))
				{
					using (MemoryStream arrowStream = new MemoryStream())
					{
						staticDataStream.CopyTo(arrowStream, 1 << 16);
						List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(arrowStream, Path.GetExtension(arrowFile));
						upArrow = loadedMeshGroups[0].Meshes[0];

						CollisionVolume = PlatingHelper.CreateTraceDataForMesh(upArrow);
						AxisAlignedBoundingBox arrowBounds = upArrow.GetAxisAlignedBoundingBox();
						//CollisionVolume = new CylinderShape(arrowBounds.XSize / 2, arrowBounds.ZSize, new SolidMaterial(RGBA_Floats.Red, .5, 0, .4));
						//CollisionVolume = new CylinderShape(arrowBounds.XSize / 2 * 4, arrowBounds.ZSize * 4, new SolidMaterial(RGBA_Floats.Red, .5, 0, .4));
					}
				}
			}
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			zHitHeight = mouseEvent3D.info.hitPosition.z;
			lastMoveDelta = new Vector3();
			double distanceToHit = Vector3.Dot(mouseEvent3D.info.hitPosition, mouseEvent3D.MouseRay.directionNormal);
			hitPlane = new PlaneShape(mouseEvent3D.MouseRay.directionNormal, distanceToHit, null);

			IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);
			zHitHeight = info.hitPosition.z;

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
			IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

			if (info != null && MeshViewerToDrawWith.SelectedMeshGroupIndex != -1)
			{
				Vector3 delta = new Vector3(0, 0, info.hitPosition.z - zHitHeight);

				// move it back to where it started
				ScaleRotateTranslate translated = MeshViewerToDrawWith.SelectedMeshGroupTransform;
				translated.translation *= Matrix4X4.CreateTranslation(new Vector3(-lastMoveDelta));
				MeshViewerToDrawWith.SelectedMeshGroupTransform = translated;

				if(MeshViewerToDrawWith.SnapGridDistance > 0)
				{
					// snap this position to the grid
					double snapGridDistance = MeshViewerToDrawWith.SnapGridDistance;
					AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();

					// snap the z position
					double bottom = selectedBounds.minXYZ.z + delta.z;
					double snappedBottom = ((int)((bottom / snapGridDistance) + .5)) * snapGridDistance;
					delta.z = snappedBottom - selectedBounds.minXYZ.z;
				}

					// and move it from there to where we are now
					translated.translation *= Matrix4X4.CreateTranslation(new Vector3(delta));
				MeshViewerToDrawWith.SelectedMeshGroupTransform = translated;

				lastMoveDelta = delta;

				view3DWidget.PartHasBeenChanged();
				Invalidate();
			}

			base.OnMouseMove(mouseEvent3D);
		}

		public override void SetPosition()
		{
			heightDisplay.SetPosition();

			AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();
			Vector3 boundsCenter = selectedBounds.Center;
			Vector3 centerTop = new Vector3(boundsCenter.x, boundsCenter.y, selectedBounds.maxXYZ.z);

			Vector2 centerTopScreenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(centerTop);

			double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(centerTop);

			Matrix4X4 arrowTransform = Matrix4X4.CreateTranslation(new Vector3(centerTop.x, centerTop.y, centerTop.z + 20 * distBetweenPixelsWorldSpace));
			arrowTransform = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * arrowTransform;

			TotalTransform = arrowTransform;

			if (MouseOver || MouseDownOnControl)
			{
				heightDisplay.Visible = true;
			}
			else if (!view3DWidget.DisplayAllValueData)
			{
				heightDisplay.Visible = false;
			}
		}

		public override void DrawGlContent(EventArgs e)
		{
			if (MeshViewerToDrawWith.SelectedMeshGroup != null)
			{
				if (MouseOver)
				{
					RenderMeshToGl.Render(upArrow, RGBA_Bytes.Red, TotalTransform, RenderTypes.Shaded);

					// draw the hight from the bottom to the bed
					AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.GetBoundsForSelection();

					Vector3 bottomRight = new Vector3(selectedBounds.maxXYZ.x, selectedBounds.maxXYZ.y, selectedBounds.minXYZ.z);
					Vector2 bottomRightScreenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(bottomRight);
				}
				else
				{
					RenderMeshToGl.Render(upArrow, RGBA_Bytes.Black, TotalTransform, RenderTypes.Shaded);
				}
			}

			base.DrawGlContent(e);
		}
	}
}