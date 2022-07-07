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

using System;
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
	public class ScaleMatrixEdgeControl : Object3DControl
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

		private readonly List<Vector2> lines = new List<Vector2>();
		private Vector3 originalPointToMove;

		/// <summary>
		/// Edge starting from the back (+y) going ccw
		/// </summary>
		private readonly int edgeIndex;
		private readonly double selectCubeSize = 7 * GuiWidget.DeviceScale;
		private readonly ThemeConfig theme;
		private readonly InlineEditControl xValueDisplayInfo;
		private readonly InlineEditControl yValueDisplayInfo;
		private bool hadClickOnControl;

		public override string UiHint => ScallingHint;
	
		public ScaleMatrixEdgeControl(IObject3DControlContext context, int edgeIndex)
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

			if (edgeIndex % 2 == 1)
			{
				Object3DControlContext.GuiSurface.AddChild(xValueDisplayInfo);
			}
			else
			{
				Object3DControlContext.GuiSurface.AddChild(yValueDisplayInfo);
			}

			this.edgeIndex = edgeIndex;

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

			Vector3 lockedEdge = GetEdgePosition(selectedItem, (edgeIndex + 2) % 4);

			Vector3 newSize = Vector3.Zero;
			newSize.X = xValueDisplayInfo.Value;
			newSize.Y = yValueDisplayInfo.Value;

			Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

			// scale it
			var scale = Matrix4X4.CreateScale(scaleAmount);

			selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

			// and keep the locked edge in place
			Vector3 newLockedEdge = GetEdgePosition(selectedItem, (edgeIndex + 2) % 4);

			AxisAlignedBoundingBox postScaleBounds = selectedItem.GetAxisAlignedBoundingBox();
			newLockedEdge.Z = 0;
			lockedEdge.Z = originalSelectedBounds.MinXYZ.Z - postScaleBounds.MinXYZ.Z;

			selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedEdge - newLockedEdge);

			Invalidate();

			Object3DControlContext.Scene.AddTransformSnapshot(startingTransform);

			transformAppliedByThis = selectedItem.Matrix;
		}

		bool ShouldDrawScaleControls()
		{
			bool shouldDrawScaleControls = true;
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleMatrixEdgeControl == null)
			{
				shouldDrawScaleControls = false;
			}
			return shouldDrawScaleControls;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = ShouldDrawScaleControls();

			var selectedItem = RootSelection;

			if (selectedItem != null)
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
						GLHelper.Render(minXminYMesh, theme.TextColor.Blend(theme.BackgroundColor, .35).WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
				}
			}

			base.Draw(e);
		}

		public override AxisAlignedBoundingBox GetWorldspaceAABB()
		{
			AxisAlignedBoundingBox box = AxisAlignedBoundingBox.Empty();

			bool shouldDrawScaleControls = ShouldDrawScaleControls();
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (shouldDrawScaleControls)
				{
					box = AxisAlignedBoundingBox.Union(box, minXminYMesh.GetAxisAlignedBoundingBox().NewTransformed(TotalTransform));
				}
			}

			return box;
		}

		public Vector3 GetCornerPosition(IObject3D item, int quadrantIndex)
		{
			AxisAlignedBoundingBox originalSelectedBounds = item.GetAxisAlignedBoundingBox();
			Vector3 cornerPosition = originalSelectedBounds.GetBottomCorner(quadrantIndex);

			return SetBottomControlHeight(originalSelectedBounds, cornerPosition);
		}

		public Vector3 GetEdgePosition(IObject3D item, int edegIndex)
		{
			AxisAlignedBoundingBox aabb = item.GetAxisAlignedBoundingBox();
			var edgePosition = default(Vector3);
			switch (edegIndex)
			{
				case 0:
					edgePosition = new Vector3(aabb.Center.X, aabb.MaxXYZ.Y, aabb.MinXYZ.Z);
					break;
				case 1:
					edgePosition = new Vector3(aabb.MinXYZ.X, aabb.Center.Y, aabb.MinXYZ.Z);
					break;
				case 2:
					edgePosition = new Vector3(aabb.Center.X, aabb.MinXYZ.Y, aabb.MinXYZ.Z);
					break;
				case 3:
					edgePosition = new Vector3(aabb.MaxXYZ.X, aabb.Center.Y, aabb.MinXYZ.Z);
					break;
			}

			return SetBottomControlHeight(aabb, edgePosition);
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
				originalPointToMove = GetEdgePosition(selectedItem, edgeIndex);

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
				IntersectInfo info = hitPlane.GetClosestIntersectionWithinRayDistanceRange(mouseEvent3D.MouseRay);

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

					Vector3 lockedEdge = GetEdgePosition(selectedItem, (edgeIndex + 2) % 4);

					Vector3 newSize = Vector3.Zero;
					if (edgeIndex % 2 == 1)
					{
						newSize.X = lockedEdge.X - newPosition.X;
						if (edgeIndex == 0 || edgeIndex == 3)
						{
							newSize.X *= -1;
						}
					}
					else
					{
						newSize.Y = lockedEdge.Y - newPosition.Y;
						if (edgeIndex == 0 || edgeIndex == 1)
						{
							newSize.Y *= -1;
						}
					}

					Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, Object3DControlContext.GuiSurface.ModifierKeys);

					// scale it
					var scale = Matrix4X4.CreateScale(scaleAmount);

					selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

					// and keep the locked edge in place
					Vector3 newLockedEdge = GetEdgePosition(selectedItem, (edgeIndex + 2) % 4);

					AxisAlignedBoundingBox postScaleBounds = selectedItem.GetAxisAlignedBoundingBox();
					newLockedEdge.Z = 0;
					lockedEdge.Z = mouseDownSelectedBounds.MinXYZ.Z - postScaleBounds.MinXYZ.Z;

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedEdge - newLockedEdge);

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

			base.CancelOperation();
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			// create the transform for the box
			Vector3 edgePosition = GetEdgePosition(selectedItem, edgeIndex);

			Vector3 boxCenter = edgePosition;

			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(edgePosition);
			switch (edgeIndex)
			{
				case 0:
					boxCenter.Y += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;
				case 1:
					boxCenter.X -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;
				case 2:
					boxCenter.Y -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;
				case 3:
					boxCenter.X += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;
			}

			boxCenter.Z += selectCubeSize / 2 * distBetweenPixelsWorldSpace;

			var centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;

			// build the scaling lines
			lines.Clear();
			Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem, edgeIndex);
			var cornerPosition = GetCornerPosition(selectedItem, edgeIndex);
			var screen1 = Object3DControlContext.World.WorldToScreenSpace(cornerPosition);
			var screen2 = Object3DControlContext.World.WorldToScreenSpace(GetCornerPosition(selectedItem, (edgeIndex + 1) % 4));

			if (screen1.Z < screen2.Z)
			{
				if (edgeIndex % 2 == 0)
				{
					// left lines
					double xSign = otherSideDelta.X > 0 ? 1 : -1;
					var yOtherSide = new Vector3(cornerPosition.X, cornerPosition.Y + otherSideDelta.Y, cornerPosition.Z);
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0, 0)));

					lines.Add(Object3DControlContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0, 0)));
				}
				else
				{
					// bottom lines
					double ySign = otherSideDelta.Y > 0 ? 1 : -1;
					var xOtherSide = new Vector3(cornerPosition.X + otherSideDelta.X, cornerPosition.Y, cornerPosition.Z);
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * DistToStart * distBetweenPixelsWorldSpace, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0)));

					lines.Add(Object3DControlContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * DistToStart * distBetweenPixelsWorldSpace, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0)));
				}
			}
			else
			{
				cornerPosition = GetCornerPosition(selectedItem, (edgeIndex + 2) % 4);
				if (edgeIndex % 2 == 0)
				{
					// right lines
					double xSign = otherSideDelta.X < 0 ? 1 : -1;
					var yOtherSide = new Vector3(cornerPosition.X, cornerPosition.Y - otherSideDelta.Y, cornerPosition.Z);

					// left lines
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0, 0)));

					lines.Add(Object3DControlContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * DistToStart * distBetweenPixelsWorldSpace, 0, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0, 0)));
				}
				else
				{
					// bottom lines
					double ySign = otherSideDelta.Y < 0 ? 1 : -1;
					var xOtherSide = new Vector3(cornerPosition.X - otherSideDelta.X, cornerPosition.Y, cornerPosition.Z);
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * DistToStart * distBetweenPixelsWorldSpace, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0)));

					lines.Add(Object3DControlContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * DistToStart * distBetweenPixelsWorldSpace, 0)));
					lines.Add(Object3DControlContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * (DistToStart + LineLength) * distBetweenPixelsWorldSpace, 0)));
				}
			}
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
					for (int i = 0; i < lines.Count; i += 2)
					{
						// draw the line that is on the ground
						drawEvent.Graphics2D.Line(lines[i], lines[i + 1], theme.TextColor);
					}

					for (int i = 0; i < lines.Count; i += 4)
					{
						drawEvent.Graphics2D.DrawMeasureLine((lines[i] + lines[i + 1]) / 2,
							(lines[i + 2] + lines[i + 3]) / 2,
							LineArrows.Both,
							theme);
					}

					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					var j = 0;
					if (edgeIndex % 2 == 1)
					{
						Vector2 widthDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
						xValueDisplayInfo.Value = selectedBounds.XSize;
						xValueDisplayInfo.OriginRelativeParent = widthDisplayCenter - xValueDisplayInfo.LocalBounds.Center;
					}
					else
					{
						Vector2 heightDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
						yValueDisplayInfo.Value = selectedBounds.YSize;
						yValueDisplayInfo.OriginRelativeParent = heightDisplayCenter - yValueDisplayInfo.LocalBounds.Center;
					}
				}
			}
		}

		public override void Dispose()
		{
			yValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}
	}
}