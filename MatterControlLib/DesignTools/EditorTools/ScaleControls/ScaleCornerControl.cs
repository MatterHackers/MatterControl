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
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Plugins.EditorTools
{
	public class ScaleCornerControl : InteractionVolume
	{
		public IObject3D ActiveSelectedItem;
		protected PlaneShape hitPlane;
		protected Vector3 initialHitPosition;
		protected Mesh minXminYMesh;
		protected AxisAlignedBoundingBox mouseDownSelectedBounds;
		protected Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		protected Matrix4X4 transformAppliedByThis = Matrix4X4.Identity;
		private double distToStart = 10;
		private double lineLength = 35;
		private List<Vector2> lines = new List<Vector2>();
		private Vector3 originalPointToMove;
		private int quadrantIndex;
		private double selectCubeSize = 7 * GuiWidget.DeviceScale;
		private ThemeConfig theme;
		private InlineEditControl xValueDisplayInfo;
		private InlineEditControl yValueDisplayInfo;
		private bool HadClickOnControl;

		public ScaleCornerControl(IInteractionVolumeContext context, int cornerIndex)
			: base(context)
		{
			theme = MatterControl.AppContext.Theme;

			xValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = ForceHideScale,
				GetDisplayString = (value) => "{0:0.0}mm".FormatWith(value),
			};

			xValueDisplayInfo.EditComplete += EditComplete;

			xValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!xValueDisplayInfo.Visible)
				{
					HadClickOnControl = false;
				}
			};

			yValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = ForceHideScale,
				GetDisplayString = (value) => "{0:0.0}mm".FormatWith(value)
			};

			yValueDisplayInfo.EditComplete += EditComplete;

			yValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!yValueDisplayInfo.Visible)
				{
					HadClickOnControl = false;
				}
			};

			InteractionContext.GuiSurface.AddChild(xValueDisplayInfo);
			InteractionContext.GuiSurface.AddChild(yValueDisplayInfo);

			this.quadrantIndex = cornerIndex;

			DrawOnTop = true;

			minXminYMesh = PlatonicSolids.CreateCube(selectCubeSize, selectCubeSize, selectCubeSize);

			CollisionVolume = minXminYMesh.CreateTraceData();

			InteractionContext.GuiSurface.AfterDraw += InteractionLayer_AfterDraw;
		}

		void EditComplete(object s, EventArgs e)
		{
			var selectedItem = ActiveSelectedItem;
			Matrix4X4 startingTransform = selectedItem.Matrix;

			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 cornerPosition = GetCornerPosition(selectedItem, quadrantIndex);
			Vector3 cornerPositionCcw = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
			Vector3 lockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);
			Vector3 cornerPositionCw = GetCornerPosition(selectedItem, (quadrantIndex + 3) % 4);

			Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem, quadrantIndex);

			Vector3 newSize = Vector3.Zero;
			newSize.X = xValueDisplayInfo.Value;
			newSize.Y = yValueDisplayInfo.Value;

			Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, InteractionContext.GuiSurface.ModifierKeys);

			// scale it
			Matrix4X4 scale = Matrix4X4.CreateScale(scaleAmount);

			selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

			// and keep the locked edge in place
			Vector3 newLockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);

			AxisAlignedBoundingBox postScaleBounds = selectedItem.GetAxisAlignedBoundingBox();
			newLockedCorner.Z = 0;
			lockedCorner.Z = originalSelectedBounds.MinXYZ.Z - postScaleBounds.MinXYZ.Z;

			selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedCorner - newLockedCorner);

			Invalidate();

			InteractionContext.Scene.AddTransformSnapshot(startingTransform);

			transformAppliedByThis = selectedItem.Matrix;
		}

		public override void DrawGlContent(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			if (InteractionContext.SelectedInteractionVolume != null
				&& InteractionContext.SelectedInteractionVolume as ScaleCornerControl == null)
			{
				shouldDrawScaleControls = false;
			}

			var selectedItem = RootSelection;

			if (selectedItem != null
				&& InteractionContext.Scene.ShowSelectionShadow)
			{
				// Ensures that functions in this scope run against the original instance reference rather than the
				// current value, thus avoiding null reference errors that would occur otherwise

				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					if (MouseOver)
					{
						GLHelper.Render(minXminYMesh, theme.PrimaryAccentColor, TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(minXminYMesh, theme.TextColor, TotalTransform, RenderTypes.Shaded);
					}
				}

				if (e != null)
				{
					Vector3 startPosition = GetCornerPosition(selectedItem, quadrantIndex);

					Vector3 endPosition = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);

					Frustum clippingFrustum = InteractionContext.World.GetClippingFrustum();

					if (clippingFrustum.ClipLine(ref startPosition, ref endPosition))
					{
						if (e.ZBuffered)
						{
							InteractionContext.World.Render3DLine(clippingFrustum, startPosition, endPosition, theme.TextColor);
						}
						else
						{
							// render on top of everything very lightly
							InteractionContext.World.Render3DLine(clippingFrustum, startPosition, endPosition, new Color(theme.TextColor, 20), false);
						}
					}

					//Vector3 startScreenSpace = InteractionContext.World.GetScreenSpace(startPosition);
					//e.graphics2D.Circle(startScreenSpace.x, startScreenSpace.y, 5, theme.PrimaryAccentColor);

					//Vector2 startScreenPosition = InteractionContext.World.GetScreenPosition(startPosition);
					//e.graphics2D.Circle(startScreenPosition.x, startScreenPosition.y, 5, theme.PrimaryAccentColor);
				}
			}

			base.DrawGlContent(e);
		}

		public Vector3 GetCornerPosition(IObject3D item, int quadrantIndex)
		{
			AxisAlignedBoundingBox originalSelectedBounds = item.GetAxisAlignedBoundingBox();
			Vector3 cornerPosition = originalSelectedBounds.GetBottomCorner(quadrantIndex);

			return SetBottomControlHeight(originalSelectedBounds, cornerPosition);
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			var selectedItem = RootSelection;
			ActiveSelectedItem = selectedItem;

			if (mouseEvent3D.MouseEvent2D.Button == MouseButtons.Left
				&& mouseEvent3D.info != null
				&& selectedItem != null)
			{
				HadClickOnControl = true;

				yValueDisplayInfo.Visible = true;
				xValueDisplayInfo.Visible = true;

				hitPlane = new PlaneShape(Vector3.UnitZ, mouseEvent3D.info.HitPosition.Z, null);
				originalPointToMove = GetCornerPosition(selectedItem, quadrantIndex);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = transformAppliedByThis = selectedItem.Matrix;
				mouseDownSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
			var selectedItem = RootSelection;
			ActiveSelectedItem = selectedItem;

			if (MouseOver)
			{
				xValueDisplayInfo.Visible = true;
				yValueDisplayInfo.Visible = true;
			}
			else if (!HadClickOnControl
				|| (selectedItem != null && selectedItem.Matrix != transformAppliedByThis))
			{
				xValueDisplayInfo.Visible = false;
				yValueDisplayInfo.Visible = false;
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

					if (InteractionContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = InteractionContext.SnapGridDistance;

						// snap this position to the grid
						newPosition.X = ((int)((newPosition.X / snapGridDistance) + .5)) * snapGridDistance;
						newPosition.Y = ((int)((newPosition.Y / snapGridDistance) + .5)) * snapGridDistance;
					}

					Vector3 cornerPosition = GetCornerPosition(selectedItem, quadrantIndex);
					Vector3 cornerPositionCcw = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
					Vector3 lockedCorner = GetCornerPosition(selectedItem, (quadrantIndex + 2) % 4);
					Vector3 cornerPositionCw = GetCornerPosition(selectedItem, (quadrantIndex + 3) % 4);

					Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem, quadrantIndex);

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

					Vector3 scaleAmount = GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, InteractionContext.GuiSurface.ModifierKeys);

					// scale it
					Matrix4X4 scale = Matrix4X4.CreateScale(scaleAmount);

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

			base.OnMouseMove(mouseEvent3D);
		}

		public override void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			if (HadClickOnControl)
			{
				InteractionContext.Scene.AddTransformSnapshot(transformOnMouseDown);
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
				MouseOver = false;

				InteractionContext.Scene.DrawSelection = true;
				InteractionContext.Scene.ShowSelectionShadow = true;
			}

			base.CancelOperation();
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 cornerPosition = GetCornerPosition(selectedItem, quadrantIndex);
			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(cornerPosition);

			Vector3 cornerPositionCcw = GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
			Vector3 cornerPositionCw = GetCornerPosition(selectedItem, (quadrantIndex + 3) % 4);

			// figure out which way the corner is relative to the bounds
			Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem, quadrantIndex);

			double xSign = otherSideDelta.X > 0 ? 1 : -1;
			double ySign = otherSideDelta.Y > 0 ? 1 : -1;

			Vector3 boxCenter = cornerPosition;
			boxCenter.X -= xSign * selectCubeSize / 2 * distBetweenPixelsWorldSpace;
			boxCenter.Y -= ySign * selectCubeSize / 2 * distBetweenPixelsWorldSpace;
			boxCenter.Z += selectCubeSize / 2 * distBetweenPixelsWorldSpace;

			Matrix4X4 centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;

			Vector3 xOtherSide = new Vector3(cornerPosition.X + otherSideDelta.X, cornerPosition.Y, cornerPosition.Z);
			Vector3 yOtherSide = new Vector3(cornerPosition.X, cornerPosition.Y + otherSideDelta.Y, cornerPosition.Z);

			lines.Clear();
			// left lines
			lines.Add(InteractionContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(InteractionContext.World.GetScreenPosition(cornerPosition - new Vector3(xSign * (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0)));

			lines.Add(InteractionContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(InteractionContext.World.GetScreenPosition(yOtherSide - new Vector3(xSign * (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0, 0)));

			// bottom lines
			lines.Add(InteractionContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * distToStart * distBetweenPixelsWorldSpace, 0)));
			lines.Add(InteractionContext.World.GetScreenPosition(cornerPosition - new Vector3(0, ySign * (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0)));

			lines.Add(InteractionContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * distToStart * distBetweenPixelsWorldSpace, 0)));
			lines.Add(InteractionContext.World.GetScreenPosition(xOtherSide - new Vector3(0, ySign * (distToStart + lineLength) * distBetweenPixelsWorldSpace, 0)));
		}

		public static Vector3 GetScalingConsideringShiftKey(AxisAlignedBoundingBox originalSelectedBounds,
			AxisAlignedBoundingBox mouseDownSelectedBounds,
			Vector3 newSize,
			Keys modifierKeys)
		{
			var minimumSize = .1;
			Vector3 scaleAmount = Vector3.One;

			if(originalSelectedBounds.XSize <= 0
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
			if (InteractionContext.HoveredInteractionVolume != this
			&& InteractionContext.HoveredInteractionVolume != null)
			{
				return true;
			}

			// if we clicked on the control
			if (HadClickOnControl)
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
			if (xDirection == 0) xDirection = cornerPositionCw.X - cornerPosition.X;
			double yDirection = cornerPositionCcw.Y - cornerPosition.Y;
			if (yDirection == 0) yDirection = cornerPositionCw.Y - cornerPosition.Y;

			return new Vector3(xDirection, yDirection, cornerPosition.Z);
		}

		private void InteractionLayer_AfterDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (MouseOver || MouseDownOnControl)
				{

					for (int i = 0; i < lines.Count; i += 2)
					{
						// draw the line that is on the ground
						drawEvent.Graphics2D.Line(lines[i], lines[i + 1], theme.TextColor);
					}

					for (int i = 0; i < lines.Count; i += 4)
					{
						DrawMeasureLine(drawEvent.Graphics2D, (lines[i] + lines[i + 1]) / 2, (lines[i + 2] + lines[i + 3]) / 2, LineArrows.Both, theme);
					}

					int j = 4;

					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					Vector2 widthDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
					xValueDisplayInfo.Value = selectedBounds.XSize;
					xValueDisplayInfo.OriginRelativeParent = widthDisplayCenter - xValueDisplayInfo.LocalBounds.Center;

					j = 0;
					Vector2 heightDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
					yValueDisplayInfo.Value = selectedBounds.YSize;
					yValueDisplayInfo.OriginRelativeParent = heightDisplayCenter - yValueDisplayInfo.LocalBounds.Center;
				}
			}
		}
	}
}