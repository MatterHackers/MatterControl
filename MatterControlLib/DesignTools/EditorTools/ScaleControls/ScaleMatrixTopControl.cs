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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Plugins.EditorTools
{
	public abstract class ScaleTopControl : Object3DControl
	{
		private IObject3D activeSelectedItem;
		private PlaneShape hitPlane;
		private Vector3 initialHitPosition;
		private readonly Mesh topScaleMesh;
		private AxisAlignedBoundingBox mouseDownSelectedBounds;
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;

		private double DistToStart => 5 * GuiWidget.DeviceScale;

		private double LineLength => 55 * GuiWidget.DeviceScale;

		private readonly List<Vector2> lines = new List<Vector2>();
		private Vector3 originalPointToMove;
		private readonly double arrowSize = 7 * GuiWidget.DeviceScale;
		private readonly ThemeConfig theme;
		private readonly InlineEditControl zValueDisplayInfo;
		private bool hadClickOnControl;

		public ScaleTopControl(IObject3DControlContext context)
			: base(context)
		{
			theme = AppContext.Theme;

			zValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = () =>
				{
					// if the selection changes
					if (RootSelection != activeSelectedItem)
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
				},
				GetDisplayString = (value) => "{0:0.0}".FormatWith(value)
			};

			zValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!zValueDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			zValueDisplayInfo.EditComplete += (s, e) =>
			{
				var selectedItem = activeSelectedItem;

				Matrix4X4 startingTransform = selectedItem.Matrix;
				var originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				Vector3 topPosition = GetTopPosition(selectedItem);
				var lockedBottom = new Vector3(topPosition.X, topPosition.Y, originalSelectedBounds.MinXYZ.Z);

				Vector3 newSize = Vector3.Zero;
				newSize.Z = zValueDisplayInfo.Value;
				Vector3 scaleAmount = ScaleCornerControl.GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

				var scale = Matrix4X4.CreateScale(scaleAmount);

				selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

				// and keep the locked edge in place
				AxisAlignedBoundingBox scaledSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				var newLockedBottom = new Vector3(topPosition.X, topPosition.Y, scaledSelectedBounds.MinXYZ.Z);

				selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedBottom - newLockedBottom);

				Invalidate();

				Object3DControlContext.Scene.AddTransformSnapshot(startingTransform);
			};

			Object3DControlContext.GuiSurface.AddChild(zValueDisplayInfo);

			DrawOnTop = true;

			topScaleMesh = PlatonicSolids.CreateCube(arrowSize, arrowSize, arrowSize);

			CollisionVolume = topScaleMesh.CreateBVHData();

			Object3DControlContext.GuiSurface.BeforeDraw += Object3DControl_BeforeDraw;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			var selectedItem = RootSelection;

			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleTopControl == null)
			{
				shouldDrawScaleControls = false;
			}

			if (selectedItem != null)
			{
				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					if (MouseIsOver)
					{
						GLHelper.Render(topScaleMesh, theme.PrimaryAccentColor, TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(topScaleMesh, theme.TextColor, TotalTransform, RenderTypes.Shaded);
					}
				}

				if (e != null)
				{
					// evaluate the position of the up line to draw
					Vector3 topPosition = GetTopPosition(selectedItem);

					var bottomPosition = topPosition;
					var originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
					bottomPosition.Z = originalSelectedBounds.MinXYZ.Z;

					// render with z-buffer full black
					Frustum clippingFrustum = Object3DControlContext.World.GetClippingFrustum();

					if (e.ZBuffered)
					{
						Object3DControlContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, theme.TextColor, width: GuiWidget.DeviceScale);
					}
					else
					{
						// render on top of everything very lightly
						Object3DControlContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, theme.TextColor.WithAlpha(20), false, GuiWidget.DeviceScale);
					}
				}
			}

			base.Draw(e);
		}

		public Vector3 GetTopPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			return new Vector3(originalSelectedBounds.Center.X, originalSelectedBounds.Center.Y, originalSelectedBounds.MaxXYZ.Z);
		}

		public override void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			if (mouseEvent3D.info != null && Object3DControlContext.Scene.SelectedItem != null)
			{
				hadClickOnControl = true;
				activeSelectedItem = RootSelection;

				zValueDisplayInfo.Visible = true;

				var selectedItem = activeSelectedItem;

				double distanceToHit = Vector3Ex.Dot(mouseEvent3D.info.HitPosition, mouseEvent3D.MouseRay.directionNormal);
				hitPlane = new PlaneShape(mouseEvent3D.MouseRay.directionNormal, distanceToHit, null);
				originalPointToMove = GetTopPosition(selectedItem);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = selectedItem.Matrix;
				mouseDownSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			var selectedItem = RootSelection;

			activeSelectedItem = selectedItem;
			if (MouseIsOver)
			{
				zValueDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl)
			{
				zValueDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl)
			{
				IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

				if (info != null
					&& selectedItem != null)
				{
					AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					Vector3 delta = info.HitPosition - initialHitPosition;

					Vector3 newPosition = originalPointToMove + delta;

					if (Object3DControlContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = Object3DControlContext.SnapGridDistance;

						// snap this position to the grid
						newPosition.Z = ((int)((newPosition.Z / snapGridDistance) + .5)) * snapGridDistance;
					}

					Vector3 topPosition = GetTopPosition(selectedItem);
					var lockedBottom = new Vector3(topPosition.X, topPosition.Y, originalSelectedBounds.MinXYZ.Z);

					Vector3 newSize = Vector3.Zero;
					newSize.Z = newPosition.Z - lockedBottom.Z;

					// scale it
					Vector3 scaleAmount = ScaleCornerControl.GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

					var scale = Matrix4X4.CreateScale(scaleAmount);

					selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

					// and keep the locked edge in place
					AxisAlignedBoundingBox scaledSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
					var newLockedBottom = new Vector3(topPosition.X, topPosition.Y, scaledSelectedBounds.MinXYZ.Z);

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedBottom - newLockedBottom);

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent3D, mouseIsOver);
		}

		public override void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			Object3DControlContext.Scene.AddTransformSnapshot(transformOnMouseDown);
			base.OnMouseUp(mouseEvent3D);
		}

		public override void CancelOperation()
		{
			IObject3D selectedItem = RootSelection;
			if (selectedItem != null
				&& MouseDownOnControl)
			{
				selectedItem.Matrix = transformOnMouseDown;
				MouseDownOnControl = false;
				MouseIsOver = false;

				Object3DControlContext.Scene.DrawSelection = true;
				Object3DControlContext.Scene.ShowSelectionShadow = true;
			}

			base.CancelOperation();
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 topPosition = GetTopPosition(selectedItem);
			var bottomPosition = new Vector3(topPosition.X, topPosition.Y, selectedBounds.MinXYZ.Z);
			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);

			Vector3 arrowCenter = topPosition;
			arrowCenter.Z += arrowSize / 2 * distBetweenPixelsWorldSpace;

			var centerMatrix = Matrix4X4.CreateTranslation(arrowCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;

			lines.Clear();
			// left lines
			lines.Add(Object3DControlContext.World.GetScreenPosition(topPosition + new Vector3(DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[0].X + LineLength, lines[0].Y));

			lines.Add(Object3DControlContext.World.GetScreenPosition(bottomPosition + new Vector3(DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[2].X + LineLength, lines[2].Y));
		}

		private void Object3DControl_BeforeDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (zValueDisplayInfo.Visible)
				{
					for (int i = 0; i < lines.Count; i += 2)
					{
						// draw the measure line
						drawEvent.Graphics2D.Line(lines[i], lines[i + 1], theme.TextColor);
					}

					for (int i = 0; i < lines.Count; i += 4)
					{
						drawEvent.Graphics2D.DrawMeasureLine((lines[i] + lines[i + 1]) / 2,
							(lines[i + 2] + lines[i + 3]) / 2,
							LineArrows.Both,
							theme);
					}

					int j = 0;
					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					Vector2 heightDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
					zValueDisplayInfo.Value = selectedBounds.ZSize;
					zValueDisplayInfo.OriginRelativeParent = heightDisplayCenter + new Vector2(10, -zValueDisplayInfo.LocalBounds.Center.Y);
				}
			}
		}

		public override void Dispose()
		{
			zValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}
	}

	public class ScaleMatrixTopControl : ScaleTopControl
	{
		public ScaleMatrixTopControl(IObject3DControlContext context)
			: base(context)
		{
		}
	}

	public class ScaleHeightControl : ScaleTopControl
	{
		public ScaleHeightControl(IObject3DControlContext context)
			: base(context)
		{
		}
	}
}