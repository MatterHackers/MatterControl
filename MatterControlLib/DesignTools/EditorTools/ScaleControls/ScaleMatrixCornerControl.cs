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

using System;
using System.Collections.Generic;
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

namespace MatterHackers.Plugins.EditorTools
{
	public class ScaleMatrixCornerControl : Object3DControl
	{
		public IObject3D ActiveSelectedItem { get; set; }

		private PlaneShape hitPlane;
		private Vector3 initialHitPosition;
		private readonly Mesh minXminYMesh;
		private AxisAlignedBoundingBox mouseDownSelectedBounds;
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private Matrix4X4 transformAppliedByThis = Matrix4X4.Identity;

		private double DistToStart => 10 * GuiWidget.DeviceScale;

		private double LineLength => 35 * GuiWidget.DeviceScale;

		private Vector3 originalPointToMove;
		private readonly int quadrantIndex;
		private readonly double selectCubeSize = 7 * GuiWidget.DeviceScale;
		private readonly ThemeConfig theme;
		private readonly InlineEditControl xValueDisplayInfo;
		private readonly InlineEditControl yValueDisplayInfo;
		private bool hadClickOnControl;

		public ScaleMatrixCornerControl(IObject3DControlContext context, int cornerIndex)
			: base(context)
		{
			theme = MatterControl.AppContext.Theme;

			xValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = ForceHideScale,
				GetDisplayString = (value) => "{0:0.0}".FormatWith(value),
			};

			xValueDisplayInfo.EditComplete += EditComplete;

			xValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!xValueDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			yValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = ForceHideScale,
				GetDisplayString = (value) => "{0:0.0}".FormatWith(value)
			};

			yValueDisplayInfo.EditComplete += EditComplete;

			yValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!yValueDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			Object3DControlContext.GuiSurface.AddChild(xValueDisplayInfo);
			Object3DControlContext.GuiSurface.AddChild(yValueDisplayInfo);

			this.quadrantIndex = cornerIndex;

			DrawOnTop = true;

			minXminYMesh = PlatonicSolids.CreateCube(selectCubeSize, selectCubeSize, selectCubeSize);

			CollisionVolume = minXminYMesh.CreateBVHData();

			Object3DControlContext.GuiSurface.BeforeDraw += Object3DControl_BeforeDraw;
		}

		private void EditComplete(object s, EventArgs e)
		{
			var selectedItem = ActiveSelectedItem;
			Matrix4X4 startingTransform = selectedItem.Matrix;

			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 lockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);

			Vector3 newSize = Vector3.Zero;
			newSize.X = xValueDisplayInfo.Value;
			newSize.Y = yValueDisplayInfo.Value;

			Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

			// scale it
			var scale = Matrix4X4.CreateScale(scaleAmount);

			selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

			// and keep the locked edge in place
			Vector3 newLockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);

			AxisAlignedBoundingBox postScaleBounds = selectedItem.GetAxisAlignedBoundingBox();
			newLockedCorner.Z = 0;
			lockedCorner.Z = originalSelectedBounds.MinXYZ.Z - postScaleBounds.MinXYZ.Z;

			selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedCorner - newLockedCorner);

			Invalidate();

			Object3DControlContext.Scene.AddTransformSnapshot(startingTransform);

			transformAppliedByThis = selectedItem.Matrix;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleMatrixCornerControl == null)
			{
				shouldDrawScaleControls = false;
			}

			var selectedItem = RootSelection;

			if (selectedItem != null
				&& Object3DControlContext.Scene.ShowSelectionShadow)
			{
				// Ensures that functions in this scope run against the original instance reference rather than the
				// current value, thus avoiding null reference errors that would occur otherwise

				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					if (MouseIsOver || MouseDownOnControl)
					{
						GLHelper.Render(minXminYMesh, theme.PrimaryAccentColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(minXminYMesh, theme.TextColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
				}

				if (e != null)
				{
					Vector3 startPosition = GetCornerPosition(selectedItem, quadrantIndex);
					Vector3 endPosition = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
					Object3DControlContext.World.Render3DLine(startPosition, endPosition, theme.TextColor.WithAlpha(e.Alpha0to255), e.ZBuffered, GuiWidget.DeviceScale);
				}
			}

			if (MouseIsOver || MouseDownOnControl)
			{
				DrawMeasureLines(e, quadrantIndex);
				DrawMeasureLines(e, quadrantIndex + 1);
			}

			base.Draw(e);
		}

		private (Vector3 start0, Vector3 end0, Vector3 start1, Vector3 end1) GetMeasureLine(int quadrant)
		{
			var selectedItem = RootSelection;
			var corner = new Vector3[4];
			var screen = new Vector3[4];
			for (int i = 0; i < 4; i++)
			{
				corner[i] = GetCornerPosition(selectedItem, quadrant + i);
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

		private void DrawMeasureLines(DrawGlContentEventArgs e, int quadrant)
		{
			var (start0, end0, start1, end1) = GetMeasureLine(quadrant);

			var color = theme.TextColor.WithAlpha(e.Alpha0to255);
			if (!e.ZBuffered)
			{
				theme.TextColor.WithAlpha(Constants.LineAlpha);
			}

			Frustum clippingFrustum = Object3DControlContext.World.GetClippingFrustum();

			Object3DControlContext.World.Render3DLine(clippingFrustum, start0, end0, color, e.ZBuffered, GuiWidget.DeviceScale);
			Object3DControlContext.World.Render3DLine(clippingFrustum, start1, end1, color, e.ZBuffered, GuiWidget.DeviceScale);
			var start = (start0 + end0) / 2;
			var end = (start1 + end1) / 2;
			Object3DControlContext.World.Render3DLine(clippingFrustum, start, end, color, e.ZBuffered, GuiWidget.DeviceScale * 1.2, true, true);
		}

		public Vector3 GetCornerPosition(IObject3D item, int quadrantIndex)
		{
			quadrantIndex %= 4;
			AxisAlignedBoundingBox originalSelectedBounds = item.GetAxisAlignedBoundingBox();
			Vector3 cornerPosition = originalSelectedBounds.GetBottomCorner(quadrantIndex);

			return SetBottomControlHeight(originalSelectedBounds, cornerPosition);
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

				xValueDisplayInfo.Visible = true;
				yValueDisplayInfo.Visible = true;

				hitPlane = new PlaneShape(Vector3.UnitZ, mouseEvent3D.info.HitPosition.Z, null);
				originalPointToMove = GetCornerPosition(selectedItem, quadrantIndex);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = transformAppliedByThis = selectedItem.Matrix;
				mouseDownSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(Mouse3DEventArgs mouseEvent3D, bool mouseIsOver)
		{
			var selectedItem = RootSelection;
			ActiveSelectedItem = selectedItem;

			if (MouseIsOver)
			{
				xValueDisplayInfo.Visible = true;
				yValueDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl
				|| (selectedItem != null && selectedItem.Matrix != transformAppliedByThis))
			{
				xValueDisplayInfo.Visible = false;
				yValueDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl && hitPlane != null)
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
						newPosition.X = ((int)((newPosition.X / snapGridDistance) + .5)) * snapGridDistance;
						newPosition.Y = ((int)((newPosition.Y / snapGridDistance) + .5)) * snapGridDistance;
					}

					Vector3 lockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);

					Vector3 newSize = Vector3.Zero;
					newSize.X = lockedCorner.X - newPosition.X;
					if (quadrantIndex == 0 || quadrantIndex == 3)
					{
						newSize.X *= -1;
					}

					newSize.Y = lockedCorner.Y - newPosition.Y;
					if (quadrantIndex == 0 || quadrantIndex == 1)
					{
						newSize.Y *= -1;
					}

					Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

					// scale it
					var scale = Matrix4X4.CreateScale(scaleAmount);

					selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

					// and keep the locked edge in place
					Vector3 newLockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);

					AxisAlignedBoundingBox postScaleBounds = selectedItem.GetAxisAlignedBoundingBox();
					newLockedCorner.Z = 0;
					lockedCorner.Z = mouseDownSelectedBounds.MinXYZ.Z - postScaleBounds.MinXYZ.Z;

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedCorner - newLockedCorner);

					Invalidate();
				}
			}

			if (selectedItem != null)
			{
				transformAppliedByThis = selectedItem.Matrix;
			}

			base.OnMouseMove(mouseEvent3D, mouseIsOver);
		}

		public override void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			if (hadClickOnControl)
			{
				Object3DControlContext.Scene.AddTransformSnapshot(transformOnMouseDown);
			}

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
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			Vector3 cornerPosition = GetCornerPosition(selectedItem, quadrantIndex);
			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(cornerPosition);

			// figure out which way the corner is relative to the bounds
			Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem, quadrantIndex);

			double xSign = otherSideDelta.X > 0 ? 1 : -1;
			double ySign = otherSideDelta.Y > 0 ? 1 : -1;

			Vector3 boxCenter = cornerPosition;
			boxCenter.X -= xSign * selectCubeSize / 2 * distBetweenPixelsWorldSpace;
			boxCenter.Y -= ySign * selectCubeSize / 2 * distBetweenPixelsWorldSpace;
			boxCenter.Z += selectCubeSize / 2 * distBetweenPixelsWorldSpace;

			var centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;
		}

		public static Vector3 GetScalingConsideringShiftKey(AxisAlignedBoundingBox originalSelectedBounds,
			AxisAlignedBoundingBox mouseDownSelectedBounds,
			Vector3 newSize,
			Keys modifierKeys)
		{
			var minimumSize = .1;
			var scaleAmount = Vector3.One;

			if (originalSelectedBounds.XSize <= 0
				|| originalSelectedBounds.YSize <= 0
				|| originalSelectedBounds.ZSize <= 0)
			{
				// don't scale if any dimension will go to 0
				return scaleAmount;
			}

			if (modifierKeys == Keys.Shift)
			{
				newSize.X = newSize.X <= minimumSize ? minimumSize : newSize.X;
				newSize.Y = newSize.Y <= minimumSize ? minimumSize : newSize.Y;
				newSize.Z = newSize.Z <= minimumSize ? minimumSize : newSize.Z;

				scaleAmount.X = mouseDownSelectedBounds.XSize / originalSelectedBounds.XSize;
				scaleAmount.Y = mouseDownSelectedBounds.YSize / originalSelectedBounds.YSize;
				scaleAmount.Z = mouseDownSelectedBounds.ZSize / originalSelectedBounds.ZSize;

				double scaleFromOriginal = Math.Max(newSize.X / mouseDownSelectedBounds.XSize, newSize.Y / mouseDownSelectedBounds.YSize);
				scaleFromOriginal = Math.Max(scaleFromOriginal, newSize.Z / mouseDownSelectedBounds.ZSize);
				scaleAmount *= scaleFromOriginal;
			}
			else
			{
				if (newSize.X > 0)
				{
					newSize.X = newSize.X <= minimumSize ? minimumSize : newSize.X;
					scaleAmount.X = newSize.X / originalSelectedBounds.XSize;
				}

				if (newSize.Y > 0)
				{
					newSize.Y = newSize.Y <= minimumSize ? minimumSize : newSize.Y;
					scaleAmount.Y = newSize.Y / originalSelectedBounds.YSize;
				}

				if (newSize.Z > 0)
				{
					newSize.Z = newSize.Z <= minimumSize ? minimumSize : newSize.Z;
					scaleAmount.Z = newSize.Z / originalSelectedBounds.ZSize;
				}
			}

			return scaleAmount;
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
			Vector3 cornerPosition = GetCornerPosition(selectedItem, quadrantIndex);
			Vector3 cornerPositionCcw = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
			Vector3 cornerPositionCw = GetCornerPosition(selectedItem, (quadrantIndex + 3) % 4);

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

		private void Object3DControl_BeforeDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (MouseIsOver || MouseDownOnControl)
				{
					UpdateNumberControl(quadrantIndex);
					UpdateNumberControl(quadrantIndex + 1);
				}
			}
		}

		private void UpdateNumberControl(int quadrant)
		{
			var (start0, end0, start1, end1) = GetMeasureLine(quadrant);
			var start = (start0 + end0) / 2;
			var end = (start1 + end1) / 2;
			var screenStart = Object3DControlContext.World.GetScreenPosition(start);
			var screenEnd = Object3DControlContext.World.GetScreenPosition(end);

			if (quadrant % 2 == 1)
			{
				xValueDisplayInfo.Value = (start - end).Length;
				xValueDisplayInfo.OriginRelativeParent = (screenStart + screenEnd) / 2 - xValueDisplayInfo.LocalBounds.Center;
			}
			else
			{
				yValueDisplayInfo.Value = (start - end).Length;
				yValueDisplayInfo.OriginRelativeParent = (screenStart + screenEnd) / 2 - yValueDisplayInfo.LocalBounds.Center;
			}
		}

		public override void Dispose()
		{
			xValueDisplayInfo.Close();
			yValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}
	}
}