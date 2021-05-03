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
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Plugins.EditorTools
{
	public class ScaleDiameterControl : Object3DControl
	{
		/// <summary>
		/// Edge starting from the back (+y) going ccw
		/// </summary>
		private readonly Mesh grabControlMesh;

		private readonly double grabControlSize = 15 * GuiWidget.DeviceScale;

		private readonly ThemeConfig theme;

		private readonly InlineEditControl diameterValueDisplayInfo;

		private bool hadClickOnControl;

		private PlaneShape hitPlane;

		private Vector3 initialHitPosition;

		private ScaleController scaleController;
		private List<Func<double>> getDiameters;
		private readonly Func<bool> controlVisible;
		private readonly ObjectSpace.Placement placement;
		private readonly int diameterIndex;
		private readonly double angleOffset;

		public ScaleDiameterControl(IObject3DControlContext context,
			List<Func<double>> getDiameters,
			List<Action<double>> setDiameters,
			int diameterIndex,
			ObjectSpace.Placement placement = ObjectSpace.Placement.Bottom,
			Func<bool> controlVisible = null,
			double angleOffset = 0)
			: base(context)
		{
			this.getDiameters = getDiameters;
			this.controlVisible = controlVisible;
			this.placement = placement;
			this.diameterIndex = diameterIndex;
			this.angleOffset = angleOffset;
			theme = MatterControl.AppContext.Theme;

			scaleController = new ScaleController(getDiameters, setDiameters);

			diameterValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = ForceHideScale,
				GetDisplayString = (value) => "{0:0.0}".FormatWith(value),
			};

			diameterValueDisplayInfo.EditComplete += async (s, e) =>
			{
				var newDiameter = diameterValueDisplayInfo.Value != 0 ? diameterValueDisplayInfo.Value : getDiameters[diameterIndex]();

				if (newDiameter == scaleController.FinalState.Diameters[diameterIndex])
				{
					return;
				}

				Vector3 lockedEdge = ObjectSpace.GetCenterPosition(ActiveSelectedItem, placement);
				scaleController.ScaleDiameter(newDiameter, diameterIndex);
				await ActiveSelectedItem.Rebuild();
				// and keep the locked edge in place
				Vector3 newLockedEdge = ObjectSpace.GetCenterPosition(ActiveSelectedItem, placement);
				ActiveSelectedItem.Translate(lockedEdge - newLockedEdge);

				scaleController.EditComplete();
			};

			diameterValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!diameterValueDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			Object3DControlContext.GuiSurface.AddChild(diameterValueDisplayInfo);

			DrawOnTop = true;

			grabControlMesh = SphereObject3D.CreateSphere(grabControlSize, 15, 10);

			CollisionVolume = grabControlMesh.CreateBVHData();

			Object3DControlContext.GuiSurface.BeforeDraw += Object3DControl_BeforeDraw;
		}

		public IObject3D ActiveSelectedItem { get; set; }

		private double LineLength => 35 * GuiWidget.DeviceScale;

		public override void CancelOperation()
		{
			IObject3D selectedItem = RootSelection;
			if (selectedItem != null
				&& MouseDownOnControl)
			{
				scaleController.Cancel();

				MouseDownOnControl = false;
				MouseIsOver = false;

				Object3DControlContext.Scene.DrawSelection = true;
				Object3DControlContext.Scene.ShowSelectionShadow = true;
			}
		}

		public override void Dispose()
		{
			diameterValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = controlVisible == null ? true : controlVisible();
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleDiameterControl == null)
			{
				shouldDrawScaleControls = false;
			}

			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				// Ensures that functions in this scope run against the original instance reference rather than the
				// current value, thus avoiding null reference errors that would occur otherwise

				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					var color = theme.PrimaryAccentColor.WithAlpha(e.Alpha0to255);
					if (MouseIsOver || MouseDownOnControl)
					{
						GLHelper.Render(grabControlMesh, color, TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						color = theme.TextColor;
						GLHelper.Render(grabControlMesh, color.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}

					Vector3 newBottomCenter = ObjectSpace.GetCenterPosition(selectedItem, placement);
					var rotation = Matrix4X4.CreateRotation(new Quaternion(selectedItem.Matrix));
					var translation = Matrix4X4.CreateTranslation(newBottomCenter);
					Object3DControlContext.World.RenderRing(rotation * translation, Vector3.Zero, getDiameters[diameterIndex](), 60, color.WithAlpha(e.Alpha0to255), 2, 0, e.ZBuffered);
				}

				if (hitPlane != null)
				{
					//Object3DControlContext.World.RenderPlane(hitPlane.Plane, Color.Red, true, 50, 3);
					//Object3DControlContext.World.RenderPlane(initialHitPosition, hitPlane.Plane.Normal, Color.Red, true, 50, 3);
				}
			}

			if (shouldDrawScaleControls
				&& (MouseIsOver || MouseDownOnControl))
			{
				DrawMeasureLines(e);
			}

			base.Draw(e);
		}

		public override void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			var selectedItem = RootSelection;
			ActiveSelectedItem = selectedItem;

			if (mouseEvent3D.MouseEvent2D.Button == MouseButtons.Left
				&& mouseEvent3D.info != null
				&& selectedItem != null)
			{
				hadClickOnControl = true;

				diameterValueDisplayInfo.Visible = true;

				var (edge, otherSide) = GetHitPosition(selectedItem);

				var upNormal = (edge - otherSide).GetNormal();
				var sideNormal = upNormal.Cross(mouseEvent3D.MouseRay.directionNormal).GetNormal();
				var planeNormal = upNormal.Cross(sideNormal).GetNormal();
				hitPlane = new PlaneShape(new Plane(planeNormal, mouseEvent3D.info.HitPosition), null);

				initialHitPosition = mouseEvent3D.info.HitPosition;

				scaleController.SetInitialState(Object3DControlContext);

				Object3DControlContext.Scene.ShowSelectionShadow = false;
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override async void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			var selectedItem = RootSelection;
			ActiveSelectedItem = selectedItem;

			if (MouseIsOver || MouseDownOnControl)
			{
				diameterValueDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl || scaleController.HasChange)
			{
				diameterValueDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl && hitPlane != null)
			{
				var info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

				if (info != null
					&& selectedItem != null)
				{
					var delta = info.HitPosition - initialHitPosition;

					var lockedBottomCenter = ObjectSpace.GetCenterPosition(selectedItem, placement);

					var (hit, otherSide) = GetHitIndices(selectedItem);
					var grabPositionEdge = ObjectSpace.GetEdgePosition(selectedItem, otherSide);
					var stretchDirection = (ObjectSpace.GetEdgePosition(selectedItem, hit) - grabPositionEdge).GetNormal();
					var deltaAlongStretch = stretchDirection.Dot(delta);

					// scale it
					var newSize = scaleController.InitialState.Diameters[diameterIndex];
					newSize += deltaAlongStretch * 2;
					newSize = Math.Max(Math.Max(newSize, .001), Object3DControlContext.SnapGridDistance);

					if (Object3DControlContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = Object3DControlContext.SnapGridDistance;

						// snap this position to the grid
						newSize = ((int)((newSize / snapGridDistance) + .5)) * snapGridDistance;
					}

					scaleController.ScaleDiameter(newSize, diameterIndex);

					await selectedItem.Rebuild();

					// and keep the locked edge in place
					Vector3 newBottomCenter = ObjectSpace.GetCenterPosition(selectedItem, placement);

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedBottomCenter - newBottomCenter);

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent3D, mouseIsOver);
		}

		public override void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			if (hadClickOnControl)
			{
				if (getDiameters[diameterIndex]() != scaleController.InitialState.Diameters[diameterIndex])
				{
					scaleController.EditComplete();
				}
				Object3DControlContext.Scene.ShowSelectionShadow = true;
			}

			base.OnMouseUp(mouseEvent3D);
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			var (hitPos, _) = GetHitPosition(selectedItem);

			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(hitPos);

			var centerMatrix = Matrix4X4.CreateTranslation(hitPos);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;
		}

		private (Vector3 edge, Vector3 otherSide) GetHitPosition(IObject3D selectedItem)
		{
			Vector3 GetEdgePosition(IObject3D item, double angle, ObjectSpace.Placement placement)
			{
				var aabb = item.GetAxisAlignedBoundingBox(item.Matrix.Inverted);
				var centerPosition = aabb.Center;
				switch (placement)
				{
					case ObjectSpace.Placement.Bottom:
						centerPosition.Z = aabb.MinXYZ.Z;
						break;
					case ObjectSpace.Placement.Center:
						centerPosition.Z = aabb.Center.Z;
						break;
					case ObjectSpace.Placement.Top:
						centerPosition.Z = aabb.MaxXYZ.Z;
						break;
				}

				var offset = new Vector3(getDiameters[diameterIndex]() / 2, 0, 0).Transform(Matrix4X4.CreateRotationZ(angle + angleOffset));
				centerPosition += offset;

				return centerPosition.Transform(item.Matrix);
			}

			var bestZEdgePosition = Vector3.Zero;
			var otherSide = Vector3.Zero;
			var bestCornerZ = double.PositiveInfinity;
			// get the closest z on the bottom in view space
			var rotations = 16;
			for (int i = 0; i < rotations; i++)
			{
				Vector3 cornerPosition = GetEdgePosition(selectedItem, MathHelper.Tau * i / rotations, placement);
				Vector3 cornerScreenSpace = Object3DControlContext.World.GetScreenSpace(cornerPosition);
				if (cornerScreenSpace.Z < bestCornerZ)
				{
					bestCornerZ = cornerScreenSpace.Z;
					bestZEdgePosition = cornerPosition;
					otherSide = GetEdgePosition(selectedItem, MathHelper.Tau * i / rotations + MathHelper.Tau / 2, placement);
				}
			}

			return (bestZEdgePosition, otherSide);
		}

		private (int edge, int otherSide) GetHitIndices(IObject3D selectedItem)
		{
			var bestZEdgePosition = -1;
			var otherSide = -1;
			var bestCornerZ = double.PositiveInfinity;
			// get the closest z on the bottom in view space
			for (int i = 0; i < 4; i++)
			{
				Vector3 cornerPosition = ObjectSpace.GetEdgePosition(selectedItem, i, placement);
				Vector3 cornerScreenSpace = Object3DControlContext.World.GetScreenSpace(cornerPosition);
				if (cornerScreenSpace.Z < bestCornerZ)
				{
					bestCornerZ = cornerScreenSpace.Z;
					bestZEdgePosition = i;
					otherSide = (i + 2) % 4;
				}
			}

			return (bestZEdgePosition, otherSide);
		}

		private void DrawMeasureLines(DrawGlContentEventArgs e)
		{
			var limitsLines = GetMeasureLine();

			var color = theme.TextColor.WithAlpha(e.Alpha0to255);
			if (!e.ZBuffered)
			{
				theme.TextColor.WithAlpha(Constants.LineAlpha);
			}

			Frustum clippingFrustum = Object3DControlContext.World.GetClippingFrustum();

			Object3DControlContext.World.Render3DLine(clippingFrustum, limitsLines.start0, limitsLines.end0, color, e.ZBuffered, GuiWidget.DeviceScale);
			Object3DControlContext.World.Render3DLine(clippingFrustum, limitsLines.start1, limitsLines.end1, color, e.ZBuffered, GuiWidget.DeviceScale);
			var start = (limitsLines.start0 + limitsLines.end0) / 2;
			var end = (limitsLines.start1 + limitsLines.end1) / 2;
			Object3DControlContext.World.Render3DLine(clippingFrustum, start, end, color, e.ZBuffered, GuiWidget.DeviceScale * 1.2, true, true);
		}

		private bool ForceHideScale()
		{
			var selectedItem = RootSelection;
			// if the selection changes
			if (selectedItem != ActiveSelectedItem)
			{
				return true;
			}

			// if another control gets a hover
			if (Object3DControlContext.HoveredObject3DControl != this
			&& Object3DControlContext.HoveredObject3DControl != null)
			{
				return true;
			}

			// if we clicked on the control
			if (hadClickOnControl)
			{
				return false;
			}

			return false;
		}

		private Vector3 GetDeltaToOtherSideXy(IObject3D selectedItem, int quadrantIndex)
		{
			Vector3 cornerPosition = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex);
			Vector3 cornerPositionCcw = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 1);
			Vector3 cornerPositionCw = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 3);

			double xDirection = cornerPositionCcw.X - cornerPosition.X;
			if (xDirection == 0)
			{
				xDirection = cornerPositionCw.X - cornerPosition.X;
			}

			double yDirection = cornerPositionCcw.Y - cornerPosition.Y;
			if (yDirection == 0)
			{
				yDirection = cornerPositionCw.Y - cornerPosition.Y;
			}

			return new Vector3(xDirection, yDirection, cornerPosition.Z);
		}

		private (Vector3 start0, Vector3 end0, Vector3 start1, Vector3 end1) GetMeasureLine()
		{
			var selectedItem = RootSelection;
			var corner = new Vector3[4];
			var screen = new Vector3[4];
			for (int i = 0; i < 4; i++)
			{
				corner[i] = ObjectSpace.GetCornerPosition(selectedItem, i);
				screen[i] = Object3DControlContext.World.GetScreenSpace(corner[i]);
			}

			var start = corner[0];
			var direction = (start - corner[1]).GetNormal();
			var end = corner[3];
			// find out which side we should render on (the one closer to the screen)
			if (screen[0].Z > screen[1].Z)
			{
				start = corner[1];
				end = corner[2];
				direction = (start - corner[0]).GetNormal();
			}

			var startScale = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(start);
			var endScale = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(end);
			var offset = .3;
			var start0 = start + direction * LineLength * offset * startScale;
			var end0 = start + direction * LineLength * (1 + offset) * endScale;
			var start1 = end + direction * LineLength * offset * endScale;
			var end1 = end + direction * LineLength * (1 + offset) * endScale;
			return (start0, end0, start1, end1);
		}

		private void Object3DControl_BeforeDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (MouseIsOver || MouseDownOnControl)
				{
					var limitsLines = GetMeasureLine();
					var start = (limitsLines.start0 + limitsLines.end0) / 2;
					var end = (limitsLines.start1 + limitsLines.end1) / 2;
					var screenStart = Object3DControlContext.World.GetScreenPosition(start);
					var screenEnd = Object3DControlContext.World.GetScreenPosition(end);

					diameterValueDisplayInfo.Value = (start - end).Length;
					diameterValueDisplayInfo.OriginRelativeParent = (screenStart + screenEnd) / 2 - diameterValueDisplayInfo.LocalBounds.Center;
				}
			}
		}
	}
}