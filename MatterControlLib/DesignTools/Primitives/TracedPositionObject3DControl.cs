/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TracedPositionObject3DControl : IObject3DControl
	{
		private readonly double blockSize = 11 * GuiWidget.DeviceScale;

		private ITraceable collisionVolume;

		public IObject3DControlContext Object3DControlContext { get; }

		private ThemeConfig theme;
		private IObject3DControlContext context;

		private Func<Vector3> getPosition;

		private Action<Vector3> setPosition;
		
		private Action<Vector3> editComplete;

		private Mesh shape;
		private bool mouseOver;
		public PlaneShape HitPlane { get; private set; }
		public bool DownOnControl { get; private set; }

		private Vector3 mouseDownPosition;

		public TracedPositionObject3DControl(IObject3DControlContext object3DControlContext,
			IObject3D owner,
			Func<Vector3> getPosition,
			Action<Vector3> setPosition,
			Action<Vector3> editComplete)
		{
			this.Object3DControlContext = object3DControlContext;

			this.theme = ApplicationController.Instance.Theme;
			this.context = object3DControlContext;
			this.getPosition = getPosition;
			this.setPosition = setPosition;
			this.editComplete = editComplete;
			this.shape = PlatonicSolids.CreateCube();
			this.shape = SphereObject3D.CreateSphere(1, 15, 10);
			collisionVolume = shape.CreateBVHData();
		}

		public bool DrawOnTop => true;

		public string Name => "Traced Position";

		public bool Visible { get; set; }

		public string UiHint => "Type 'Esc' to cancel".Localize();

		public void CancelOperation()
		{
			if (DownOnControl)
			{
				ApplicationController.Instance.UiHint = "";
				DownOnControl = false;
				setPosition(mouseDownPosition);
				ApplicationController.Instance.UiHint = "";
			}
		}

		public void Draw(DrawGlContentEventArgs e)
		{
			var color = Color.Black;
			if (mouseOver)
			{
				color = theme.PrimaryAccentColor;
			}

			GLHelper.Render(shape, color.WithAlpha(e.Alpha0to255), ShapeMatrix(), RenderTypes.Shaded);

			if (HitPlane != null)
			{
				//Object3DControlContext.World.RenderPlane(hitPlane.Plane, Color.Red, true, 30, 3);
				//Object3DControlContext.World.RenderPlane(initialHitPosition, hitPlane.Plane.Normal, Color.Red, true, 30, 3);
			}
		}

		private Matrix4X4 ShapeMatrix()
		{
			var worldPosition = getPosition();
			double distBetweenPixelsWorldSpace = context.World.GetWorldUnitsPerScreenPixelAtPosition(worldPosition);
			var scale = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace * blockSize);
			var offset = Matrix4X4.CreateTranslation(getPosition());

			var cubeMatrix = scale * offset;
			return cubeMatrix;
		}

		public ITraceable GetTraceable()
		{
			return new Transform(collisionVolume, ShapeMatrix());
		}

		public void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			DownOnControl = true;
			ApplicationController.Instance.UiHint = UiHint;
			mouseDownPosition = getPosition();
			// Make sure we always get a new hit plane
			ResetHitPlane();
		}

		public void ResetHitPlane()
		{
			HitPlane = null;
		}

		public void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			if (mouseOver != mouseIsOver)
			{
				mouseOver = mouseIsOver;
				context.GuiSurface.Invalidate();
			}

			if (DownOnControl)
			{
				UpdatePosition(mouseEvent3D.MouseEvent2D.Position);
			}
		}

		private void UpdatePosition(Vector2 screenPosition)
		{
			var ray = context.World.GetRayForLocalBounds(screenPosition);
			var scene = context.Scene;
			var intersectionInfo = scene.GetBVHData().GetClosestIntersection(ray);

			var oldPosition = getPosition();
			var newPosition = oldPosition;
			var world = Object3DControlContext.World;
			var rayNormal = (oldPosition - world.EyePosition).GetNormal();
			if (intersectionInfo == null)
			{
				if (HitPlane == null)
				{
					HitPlane = new PlaneShape(new Plane(rayNormal, oldPosition), null);
				}

				intersectionInfo = HitPlane.GetClosestIntersection(ray);
				if (intersectionInfo != null)
				{
					newPosition = intersectionInfo.HitPosition;
				}
			}
			else
			{
				HitPlane = new PlaneShape(new Plane(rayNormal, oldPosition), null);

				foreach (var object3D in scene.Children)
				{
					if (object3D.GetBVHData().Contains(intersectionInfo.HitPosition))
					{
						newPosition = intersectionInfo.HitPosition;
						break;
					}
				}
			}

			if (newPosition != oldPosition)
			{
				setPosition(newPosition);
				context.GuiSurface.Invalidate();
			}
		}

		public void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			if (DownOnControl)
			{
				DownOnControl = false;
				editComplete(mouseDownPosition);
				ApplicationController.Instance.UiHint = "";
			}
		}

		public void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
		}

		public void MoveToScreenPosition(Vector2 screenPosition)
		{
			UpdatePosition(screenPosition);
		}

		public void Dispose()
		{
		}
	}
}