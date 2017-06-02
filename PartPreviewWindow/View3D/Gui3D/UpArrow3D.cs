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
using MatterHackers.DataConverters3D;
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
		internal HeightValueDisplay heightDisplay;
		private PlaneShape hitPlane;
		private Vector3 lastMoveDelta;
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;

		private static IObject3D upArrowItem = null;

		private View3DWidget view3DWidget;
		private double zHitHeight;
		
		public UpArrow3D(View3DWidget view3DWidget)
			: base(null, view3DWidget.meshViewerWidget)
		{
			heightDisplay = new HeightValueDisplay(view3DWidget);
			heightDisplay.Visible = false;

			DrawOnTop = true;

			this.view3DWidget = view3DWidget;

			if (upArrowItem == null)
			{
				string arrowFile = Path.Combine("Icons", "3D Icons", "up_pointer.stl");
				using (Stream arrowStream = StaticData.Instance.OpenSteam(arrowFile))
				{
					upArrowItem = MeshFileIo.Load(arrowStream, Path.GetExtension(arrowFile));
				}
			}

			CollisionVolume = upArrowItem.TraceData();
		}

		public override void DrawGlContent(EventArgs e)
		{
			bool shouldDrawScaleControls = true;
			if (MeshViewerToDrawWith.SelectedInteractionVolume != null
				&& MeshViewerToDrawWith.SelectedInteractionVolume as UpArrow3D == null)
			{
				shouldDrawScaleControls = false;
			}
			if (MeshViewerToDrawWith.Scene.HasSelection
				&& shouldDrawScaleControls)
			{
				if (MouseOver)
				{
					GLHelper.Render(upArrowItem.Mesh, RGBA_Bytes.Red, TotalTransform, RenderTypes.Shaded);
				}
				else
				{
					GLHelper.Render(upArrowItem.Mesh, RGBA_Bytes.Black, TotalTransform, RenderTypes.Shaded);
				}
			}

			base.DrawGlContent(e);
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			zHitHeight = mouseEvent3D.info.hitPosition.z;
			lastMoveDelta = new Vector3();
			double distanceToHit = Vector3.Dot(mouseEvent3D.info.hitPosition, mouseEvent3D.MouseRay.directionNormal);
			hitPlane = new PlaneShape(mouseEvent3D.MouseRay.directionNormal, distanceToHit, null);

			IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);
			zHitHeight = info.hitPosition.z;

			var selectedItem = MeshViewerToDrawWith.Scene.SelectedItem;
			if (selectedItem != null)
			{
				transformOnMouseDown = selectedItem.Matrix;
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
			IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

			if (info != null && MeshViewerToDrawWith.Scene.HasSelection)
			{
				var selectedItem = MeshViewerToDrawWith.Scene.SelectedItem;
				Vector3 delta = new Vector3(0, 0, info.hitPosition.z - zHitHeight);

				// move it back to where it started
				selectedItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(-lastMoveDelta));

				if (MeshViewerToDrawWith.SnapGridDistance > 0)
				{
					// snap this position to the grid
					double snapGridDistance = MeshViewerToDrawWith.SnapGridDistance;
					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					// snap the z position
					double bottom = selectedBounds.minXYZ.z + delta.z;
					double snappedBottom = (Math.Round((bottom / snapGridDistance))) * snapGridDistance;
					delta.z = snappedBottom - selectedBounds.minXYZ.z;
				}

				// and move it from there to where we are now
				selectedItem.Matrix *= Matrix4X4.CreateTranslation(new Vector3(delta));

				lastMoveDelta = delta;

				view3DWidget.PartHasBeenChanged();
				Invalidate();
			}

			base.OnMouseMove(mouseEvent3D);
		}

		public override void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			view3DWidget.AddUndoForSelectedMeshGroupTransform(transformOnMouseDown);
			base.OnMouseUp(mouseEvent3D);
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			Vector3 boundsCenter = selectedBounds.Center;
			Vector3 centerTop = new Vector3(boundsCenter.x, boundsCenter.y, selectedBounds.maxXYZ.z);

			Vector2 centerTopScreenPosition = MeshViewerToDrawWith.World.GetScreenPosition(centerTop);

			double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.World.GetWorldUnitsPerScreenPixelAtPosition(centerTop);

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
	}
}