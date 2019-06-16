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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
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
	public class RotateCornerControl : InteractionVolume
	{
		private IObject3D selectedItemOnMouseDown;
		private static readonly VertexStorage Arrows = new VertexStorage("M267.96599,177.26875L276.43374,168.80101C276.43374,170.2123 276.43374,171.62359 276.43374,173.03488C280.02731,173.01874 282.82991,174.13254 286.53647,171.29154C290.08503,168.16609 288.97661,164.24968 289.13534,160.33327L284.90147,160.33327L293.36921,151.86553L301.83695,160.33327L297.60308,160.33327C297.60308,167.38972 298.67653,171.4841 293.23666,177.24919C286.80975,182.82626 283.014,181.02643 276.43374,181.50262L276.43374,185.73649L267.96599,177.26875L267.96599,177.26875z");
		private static ImageBuffer rotationImageWhite;

		private readonly double arrowsOffset = 15;
		private readonly double ringWidth = 20;
		private readonly ThemeConfig theme;
		private readonly InlineEditControl angleTextControl;

		private double lastSnappedRotation = 0;
		private Mouse3DInfo mouseDownInfo = null;
		private Mouse3DInfo mouseMoveInfo = null;
		private readonly int numSnapPoints = 8;
		private readonly Mesh rotationHandle;
		private double rotationTransformScale = 1;
		private Vector3 selectCubeSize = new Vector3(30, 30, .1) * GuiWidget.DeviceScale;

		public RotateCornerControl(IInteractionVolumeContext context, int axisIndex)
			: base(context)
		{
			theme = MatterControl.AppContext.Theme;

			angleTextControl = new InlineEditControl()
			{
				ForceHide = ForceHideAngle,
				GetDisplayString = (value) => "{0:0.0#}°".FormatWith(value),
				Visible = false
			};

			angleTextControl.VisibleChanged += (s, e) =>
			{
				mouseDownInfo = null;
				Invalidate();
			};

			InteractionContext.GuiSurface.AddChild(angleTextControl);

			angleTextControl.EditComplete += (s, e) =>
			{
				var selectedItem = RootSelection;

				if (selectedItem != null
					&& mouseDownInfo != null)
				{
					if (mouseMoveInfo != null)
					{
						SnappedRotationAngle = MathHelper.DegreesToRadians(angleTextControl.Value);

						mouseMoveInfo.AngleOfHit = mouseDownInfo.AngleOfHit + SnappedRotationAngle;

						RotatingCW = DeltaAngle(0, SnappedRotationAngle) < 0;

						// undo the last rotation
						RotateAroundAxis(selectedItem, -lastSnappedRotation);

						// rotate it
						RotateAroundAxis(selectedItem, SnappedRotationAngle);

						Invalidate();

						lastSnappedRotation = SnappedRotationAngle;
					}

					InteractionContext.Scene.AddTransformSnapshot(mouseDownInfo.SelectedObjectTransform);
				}
			};

			this.RotationAxis = axisIndex;

			DrawOnTop = true;

			rotationHandle = PlatonicSolids.CreateCube(selectCubeSize);

			RectangleDouble bounds = Arrows.GetBounds();
			if (rotationImageWhite == null)
			{
				rotationImageWhite = new ImageBuffer(64, 64, 32, new BlenderBGRA());
			}

			var arrows2 = new VertexSourceApplyTransform(Arrows, Affine.NewTranslation(-bounds.Center)
				* Affine.NewScaling(rotationImageWhite.Width / bounds.Width, rotationImageWhite.Height / bounds.Height)
				* Affine.NewTranslation(rotationImageWhite.Width / 2, rotationImageWhite.Height / 2));

			Graphics2D imageGraphics = rotationImageWhite.NewGraphics2D();
			imageGraphics.Clear(new Color(Color.White, 0));
			imageGraphics.Render(new FlattenCurves(arrows2), Color.White);

			var clearImage = new ImageBuffer(2, 2, 32, new BlenderBGRA());

			for (int i = 0; i < rotationHandle.Faces.Count; i++)
			{
				if (i == 0 || i == 1)
				{
					rotationHandle.PlaceTextureOnFace(i, rotationImageWhite);
				}
				else
				{
					rotationHandle.PlaceTextureOnFace(i, clearImage);
				}
			}

			CollisionVolume = rotationHandle.CreateTraceData();

			InteractionContext.GuiSurface.AfterDraw += InteractionLayer_AfterDraw;
		}

		public int RotationAxis { get; private set; }

		public Vector3 RotationPlanNormal
		{
			get
			{
				var rotationPlanNormal = Vector3.Zero;
				rotationPlanNormal[RotationAxis] = 1;
				return rotationPlanNormal;
			}
		}

		private bool RotatingCW { get; set; } = true;

		private double SnappedRotationAngle { get; set; }

		public override void DrawGlContent(DrawGlContentEventArgs e)
		{
			IObject3D selectedItem = RootSelection;
			// We only draw rotation controls if something is selected
			if (selectedItem != null)
			{
				// make sure the image is initialized
				RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(rotationImageWhite, true);

				// We only draw the rotation arrows when the user has not selected any interaction volumes (they are not actively scaling or rotating anything).
				if (InteractionContext.SelectedInteractionVolume == null)
				{
					var color = MouseOver ? theme.PrimaryAccentColor : theme.TextColor;
					GLHelper.Render(rotationHandle, new Color(color, 254), TotalTransform, RenderTypes.Shaded);
				}

				// If the user is over the control or has clicked on it
				if (mouseMoveInfo != null || mouseDownInfo != null
					|| MouseOver)
				{
					DrawRotationCompass(selectedItem, e);
				}
			}

			base.DrawGlContent(e);
		}

		public Vector3 GetCornerPosition(IObject3D objectBeingRotated)
		{
			return GetCornerPosition(objectBeingRotated, out _);
		}

		public Vector3 GetCornerPosition(IObject3D objectBeingRotated, out int cornerIndexOut)
		{
			cornerIndexOut = 0;
			AxisAlignedBoundingBox currentSelectedBounds = objectBeingRotated.GetAxisAlignedBoundingBox();

			Vector3 bestZCornerPosition = Vector3.Zero;
			int xCornerIndex = 0;
			Vector3 bestXCornerPosition = Vector3.Zero;
			int yCornerIndex = 1;
			Vector3 bestYCornerPosition = Vector3.Zero;
			int zCornerIndex = 2;

			double bestCornerZ = double.PositiveInfinity;
			// get the closest z on the bottom in view space
			for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
			{
				Vector3 cornerPosition = currentSelectedBounds.GetBottomCorner(cornerIndex);
				Vector3 cornerScreenSpace = InteractionContext.World.GetScreenSpace(cornerPosition);
				if (cornerScreenSpace.Z < bestCornerZ)
				{
					zCornerIndex = cornerIndex;
					bestCornerZ = cornerScreenSpace.Z;
					bestZCornerPosition = cornerPosition;

					bestZCornerPosition = SetBottomControlHeight(currentSelectedBounds, bestZCornerPosition);

					Vector3 testCornerPosition = currentSelectedBounds.GetBottomCorner((cornerIndex + 1) % 4);
					if (testCornerPosition.Y == cornerPosition.Y)
					{
						xCornerIndex = (cornerIndex + 1) % 4;
						yCornerIndex = (cornerIndex + 3) % 4;
					}
					else
					{
						xCornerIndex = (cornerIndex + 3) % 4;
						yCornerIndex = (cornerIndex + 1) % 4;
					}

					bestXCornerPosition = currentSelectedBounds.GetBottomCorner(xCornerIndex);
					bestXCornerPosition.Z = currentSelectedBounds.MaxXYZ.Z;
					bestYCornerPosition = currentSelectedBounds.GetBottomCorner(yCornerIndex);
					bestYCornerPosition.Z = currentSelectedBounds.MaxXYZ.Z;
				}
			}

			switch (RotationAxis)
			{
				case 0:
					cornerIndexOut = xCornerIndex;
					return bestXCornerPosition;

				case 1:
					cornerIndexOut = yCornerIndex;
					return bestYCornerPosition;

				case 2:
					cornerIndexOut = zCornerIndex;
					return bestZCornerPosition;
			}

			return bestZCornerPosition;
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			InteractionContext.Scene.DrawSelection = false;

			IObject3D selectedItem = RootSelection;

			if (mouseEvent3D.info != null && selectedItem != null)
			{
				selectedItemOnMouseDown = selectedItem;

				angleTextControl.Visible = true;

				var selectedObject = selectedItemOnMouseDown;
				AxisAlignedBoundingBox currentSelectedBounds = selectedObject.GetAxisAlignedBoundingBox();

				var selectedObjectRotationCenter = currentSelectedBounds.Center;
				Vector3 cornerForAxis = GetCornerPosition(selectedObject);
				// move the rotation center to the correct side of the bounding box
				selectedObjectRotationCenter[RotationAxis] = cornerForAxis[RotationAxis];

				mouseDownInfo = new Mouse3DInfo(
					mouseEvent3D.info.HitPosition,
					selectedObject.Matrix,
					selectedObjectRotationCenter,
					GetControlCenter(selectedObject),
					RotationAxis);

				// Get move data updated
				lastSnappedRotation = 0;
				SnappedRotationAngle = 0;
				RotatingCW = true;
				mouseMoveInfo = mouseDownInfo;
				InteractionContext.Scene.ShowSelectionShadow = false;

				mouseMoveInfo = new Mouse3DInfo(
					mouseEvent3D.info.HitPosition,
					selectedObject.Matrix,
					selectedObjectRotationCenter,
					GetControlCenter(selectedObject),
					RotationAxis);
			}

			base.OnMouseDown(mouseEvent3D);
		}

		public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
			IObject3D selectedItem = RootSelection;
			if (selectedItem != null)
			{
				var controlCenter = GetControlCenter(selectedItem);
				if (mouseDownInfo != null)
				{
					controlCenter = mouseDownInfo.ControlCenter;
				}

				var hitPlane = new PlaneShape(RotationPlanNormal, Vector3Ex.Dot(RotationPlanNormal, controlCenter), null);
				IntersectInfo hitOnRotationPlane = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);
				if (hitOnRotationPlane != null)
				{
					AxisAlignedBoundingBox currentSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
					var selectedObjectRotationCenter = currentSelectedBounds.Center;
					if (mouseDownInfo != null)
					{
						selectedObjectRotationCenter = mouseDownInfo.SelectedObjectRotationCenter;
					}

					Vector3 cornerForAxis = GetCornerPosition(selectedItem);
					// move the rotation center to the correct side of the bounding box
					selectedObjectRotationCenter[RotationAxis] = cornerForAxis[RotationAxis];

					mouseMoveInfo = new Mouse3DInfo(
						hitOnRotationPlane.HitPosition,
						selectedItem.Matrix,
						selectedObjectRotationCenter,
						controlCenter,
						RotationAxis);

					if (MouseDownOnControl
						&& mouseDownInfo != null)
					{
						double rawDeltaRotationAngle = mouseMoveInfo.AngleOfHit - mouseDownInfo.AngleOfHit;
						double snapRadians = MathHelper.DegreesToRadians(1);

						// snap this position to the grid
						if (rawDeltaRotationAngle > 0)
						{
							SnappedRotationAngle = ((int)((rawDeltaRotationAngle / snapRadians) + .5)) * snapRadians;
						}
						else
						{
							SnappedRotationAngle = ((int)((rawDeltaRotationAngle / snapRadians) - .5)) * snapRadians;
						}

						int snappingIndex = GetSnapIndex(selectedItem, numSnapPoints);
						if (snappingIndex != -1)
						{
							SnappedRotationAngle = snappingIndex * MathHelper.Tau / numSnapPoints;
						}
						else if (InteractionContext.GuiSurface.ModifierKeys == Keys.Shift)
						{
							snapRadians = MathHelper.DegreesToRadians(45);

							if (rawDeltaRotationAngle > 0)
							{
								SnappedRotationAngle = ((int)((rawDeltaRotationAngle / snapRadians) + .5)) * snapRadians;
							}
							else
							{
								SnappedRotationAngle = ((int)((rawDeltaRotationAngle / snapRadians) - .5)) * snapRadians;
							}
						}

						if (SnappedRotationAngle < 0)
						{
							SnappedRotationAngle += MathHelper.Tau;
						}

						// check if this move crosses zero degrees
						if (lastSnappedRotation == 0 && SnappedRotationAngle != 0)
						{
							RotatingCW = DeltaAngle(0, SnappedRotationAngle) < 0;
						}
						else if ((DeltaAngle(0, SnappedRotationAngle) < 0
							&& DeltaAngle(0, lastSnappedRotation) > 0
							&& Math.Abs(DeltaAngle(0, lastSnappedRotation)) < 1)
							|| (DeltaAngle(0, SnappedRotationAngle) > 0
							&& DeltaAngle(0, lastSnappedRotation) < 0
							&& Math.Abs(DeltaAngle(0, lastSnappedRotation)) < 1))
						{
							// let's figure out which way we are going
							RotatingCW = DeltaAngle(0, SnappedRotationAngle) < 0 && DeltaAngle(0, lastSnappedRotation) > 0;
						}

						// undo the last rotation
						RotateAroundAxis(selectedItem, -lastSnappedRotation);

						// rotate it
						RotateAroundAxis(selectedItem, SnappedRotationAngle);

						lastSnappedRotation = SnappedRotationAngle;

						Invalidate();
					}
				}
			}

			base.OnMouseMove(mouseEvent3D);
		}

		public override void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			InteractionContext.Scene.DrawSelection = true;
			// if we rotated it
			if (mouseDownInfo != null)
			{
				// put in the start transform so we can go back to it if we have to
				InteractionContext.Scene.AddTransformSnapshot(mouseDownInfo.SelectedObjectTransform);
			}

			if (mouseDownInfo != null)
			{
				InteractionContext.Scene.ShowSelectionShadow = true;
			}

			base.OnMouseUp(mouseEvent3D);
		}

		public override void CancelOperation()
		{
			IObject3D selectedItem = RootSelection;
			if (selectedItem != null
				&& MouseDownOnControl
				&& mouseDownInfo != null)
			{
				selectedItem.Matrix = mouseDownInfo.SelectedObjectTransform;
				MouseDownOnControl = false;
				MouseOver = false;
				mouseDownInfo = null;
				mouseMoveInfo = null;

				InteractionContext.Scene.DrawSelection = true;
				InteractionContext.Scene.ShowSelectionShadow = true;
			}

			base.CancelOperation();
		}

		public override void SetPosition(IObject3D selectedItem)
		{
			Vector3 boxCenter = GetControlCenter(selectedItem);
			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(boxCenter);

			GetCornerPosition(selectedItem, out int cornerIndexOut);

			Matrix4X4 centerMatrix = Matrix4X4.Identity;
			switch (RotationAxis)
			{
				case 0:
					if (cornerIndexOut == 1 || cornerIndexOut == 3)
					{
						centerMatrix *= Matrix4X4.CreateRotationX(MathHelper.DegreesToRadians(90));
					}
					else
					{
						centerMatrix *= Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(-90));
					}

					centerMatrix *= Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(90) * cornerIndexOut);
					break;

				case 1:
					if (cornerIndexOut == 1 || cornerIndexOut == 3)
					{
						centerMatrix *= Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(-90));
					}
					else
					{
						centerMatrix *= Matrix4X4.CreateRotationX(MathHelper.DegreesToRadians(90));
					}

					centerMatrix *= Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(90) * cornerIndexOut);
					break;

				case 2:
					centerMatrix *= Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(90) * cornerIndexOut);
					break;
			}

			centerMatrix *= Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * Matrix4X4.CreateTranslation(boxCenter);
			TotalTransform = centerMatrix;
		}

		/// <summary>
		/// Measure the difference between two angles.
		/// </summary>
		/// <param name="startAngle">The starting angle.</param>
		/// <param name="endAngle">The ending angle.</param>
		/// <returns>The angle from a to b. If A = 2 and B = 0 return 2.
		/// If A = 0 and B = 2 return -2.</returns>
		private static double DeltaAngle(double startAngle, double endAngle)
		{
			return Math.Atan2(Math.Sin(startAngle - endAngle), Math.Cos(startAngle - endAngle));
		}

		private void DrawRotationCompass(IObject3D selectedItem, DrawGlContentEventArgs drawEventArgs)
		{
			if (InteractionContext.Scene.SelectedItem == null)
			{
				return;
			}

			double alphaValue = 1;
			if (!drawEventArgs.ZBuffered)
			{
				alphaValue = .3;
			}

			AxisAlignedBoundingBox currentSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			if (currentSelectedBounds.XSize > 100000)
			{
				// something is wrong the part is too big (probably in invalid selection)
				return;
			}

			if (mouseMoveInfo != null)
			{
				Matrix4X4 rotationCenterTransform = GetRotationTransform(selectedItem, out double radius);

				double innerRadius = radius + ringWidth / 2;
				double outerRadius = innerRadius + ringWidth;
				double snappingMarkRadius = outerRadius + 20;

				double startBlue = 0;
				double endBlue = MathHelper.Tau;

				double mouseAngle = 0;
				if (mouseMoveInfo != null)
				{
					mouseAngle = mouseMoveInfo.AngleOfHit;
				}

				if (mouseDownInfo != null)
				{
					mouseAngle = mouseDownInfo.AngleOfHit;
				}

				var graphics2DOpenGL = new Graphics2DOpenGL();

				if (mouseDownInfo != null || MouseOver)
				{
					IVertexSource blueRing = new JoinPaths(new Arc(0, 0, outerRadius, outerRadius, startBlue, endBlue, Arc.Direction.CounterClockWise),
						new Arc(0, 0, innerRadius, innerRadius, startBlue, endBlue, Arc.Direction.ClockWise));
					graphics2DOpenGL.RenderTransformedPath(rotationCenterTransform, blueRing, new Color(theme.PrimaryAccentColor, (int)(50 * alphaValue)), drawEventArgs.ZBuffered);
					// tick 60 marks
					DrawTickMarks(drawEventArgs, alphaValue, rotationCenterTransform, innerRadius, outerRadius, 60);
				}

				if (mouseDownInfo != null)
				{
					double startRed = mouseDownInfo.AngleOfHit;
					double endRed = SnappedRotationAngle + mouseDownInfo.AngleOfHit;

					if (!RotatingCW)
					{
						var temp = startRed;
						startRed = endRed;
						endRed = temp;
					}

					IVertexSource redAngle = new JoinPaths(new Arc(0, 0, 0, 0, startRed, endRed, Arc.Direction.CounterClockWise),
					new Arc(0, 0, innerRadius, innerRadius, startRed, endRed, Arc.Direction.ClockWise));
					graphics2DOpenGL.RenderTransformedPath(rotationCenterTransform, redAngle, new Color(theme.PrimaryAccentColor, (int)(70 * alphaValue)), drawEventArgs.ZBuffered);

					// draw a line to the mouse on the rotation circle
					if (mouseMoveInfo != null && MouseDownOnControl)
					{
						var unitPosition = new Vector3(Math.Cos(mouseMoveInfo.AngleOfHit), Math.Sin(mouseMoveInfo.AngleOfHit), 0);
						Vector3 startPosition = Vector3Ex.Transform(unitPosition * innerRadius, rotationCenterTransform);
						var center = Vector3Ex.Transform(Vector3.Zero, rotationCenterTransform);
						if ((mouseMoveInfo.HitPosition - center).Length > rotationTransformScale * innerRadius)
						{
							InteractionContext.World.Render3DLine(startPosition, mouseMoveInfo.HitPosition, theme.PrimaryAccentColor, drawEventArgs.ZBuffered);
						}

						DrawSnappingMarks(drawEventArgs, mouseAngle, alphaValue, rotationCenterTransform, snappingMarkRadius, numSnapPoints, GetSnapIndex(selectedItem, numSnapPoints));
					}
				}
			}
		}

		private void DrawSnappingMarks(DrawGlContentEventArgs drawEventArgs, double mouseAngle, double alphaValue, Matrix4X4 rotationCenterTransform, double distanceFromCenter, int numSnapPoints, int markToSnapTo)
		{
			var graphics2DOpenGL = new Graphics2DOpenGL();

			double snappingRadians = MathHelper.Tau / numSnapPoints;

			for (int i = 0; i < numSnapPoints; i++)
			{
				double startAngle = i * snappingRadians + mouseAngle;

				var snapShape = new VertexStorage();
				snapShape.MoveTo(-10, 0);
				snapShape.LineTo(5, 7);
				snapShape.LineTo(5, -7);
				snapShape.ClosePolygon();

				var transformed = new VertexSourceApplyTransform(snapShape, Affine.NewTranslation(distanceFromCenter, 0) * Affine.NewRotation(startAngle));
				// new Ellipse(startPosition.x, startPosition.y, dotRadius, dotRadius);

				var color = theme.TextColor;
				if (i == markToSnapTo)
				{
					color = theme.PrimaryAccentColor;
				}

				graphics2DOpenGL.RenderTransformedPath(rotationCenterTransform, transformed, new Color(color, (int)(254 * alphaValue)), drawEventArgs.ZBuffered);
			}
		}

		private void DrawTickMarks(DrawGlContentEventArgs drawEventArgs, double alphaValue, Matrix4X4 rotationCenterTransform, double innerRadius, double outerRadius, int numTicks)
		{
			double snappingRadians = MathHelper.Tau / numTicks;
			for (int i = 0; i < numTicks; i++)
			{
				double startAngle = i * snappingRadians;

				var unitPosition = new Vector3(Math.Cos(startAngle), Math.Sin(startAngle), 0);
				Vector3 startPosition = Vector3Ex.Transform(unitPosition * innerRadius, rotationCenterTransform);
				Vector3 endPosition = Vector3Ex.Transform(unitPosition * outerRadius, rotationCenterTransform);

				InteractionContext.World.Render3DLine(startPosition, endPosition, new Color(theme.TextColor, (int)(254 * alphaValue)), drawEventArgs.ZBuffered);
			}
		}

		private bool ForceHideAngle()
		{
			return (InteractionContext.HoveredInteractionVolume != this
			&& InteractionContext.HoveredInteractionVolume != null)
			|| RootSelection != selectedItemOnMouseDown;
		}

		/// <summary>
		/// This gets the world position of the rotation control handle. It is
		/// complicated by the fact that the control is pushed away from the corner
		/// that it is part of by a number of pixels in screen space.
		/// </summary>
		/// <param name="selectedItem">The item to the center of</param>
		/// <returns>The center of the control in item space.</returns>
		private Vector3 GetControlCenter(IObject3D selectedItem)
		{
			Vector3 boxCenter = GetCornerPosition(selectedItem);
			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(boxCenter);
			// figure out which way the corner is relative to the bounds
			Vector3 otherSideDelta = GetDeltaToOtherSideXy(selectedItem);

			double xSign = otherSideDelta.X > 0 ? 1 : -1;
			double ySign = otherSideDelta.Y > 0 ? 1 : -1;

			var delta = new Vector3(xSign * (selectCubeSize.X / 2 + arrowsOffset) * distBetweenPixelsWorldSpace,
				ySign * (selectCubeSize.Y / 2 + arrowsOffset) * distBetweenPixelsWorldSpace,
				-selectCubeSize.Z / 2 * distBetweenPixelsWorldSpace);
			delta[RotationAxis] = 0;
			boxCenter -= delta;

			return boxCenter;
		}

		private Vector3 GetDeltaToOtherSideXy(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox currentSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 cornerPosition = GetCornerPosition(selectedItem, out int cornerIndex);
			Vector3 cornerPositionCcw = currentSelectedBounds.GetBottomCorner((cornerIndex + 1) % 4);
			Vector3 cornerPositionCw = currentSelectedBounds.GetBottomCorner((cornerIndex + 3) % 4);

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

		private Vector3 GetRotationCenter(IObject3D selectedItem, AxisAlignedBoundingBox currentSelectedBounds)
		{
			var rotationCenter = currentSelectedBounds.Center;
			Vector3 cornerForAxis = GetCornerPosition(selectedItem);
			// move the rotation center to the plane of the control
			rotationCenter[RotationAxis] = cornerForAxis[RotationAxis];

			if (mouseDownInfo != null)
			{
				rotationCenter = mouseDownInfo.SelectedObjectRotationCenter;
			}

			return rotationCenter;
		}

		private Matrix4X4 GetRotationTransform(IObject3D selectedItem, out double radius)
		{
			Matrix4X4 rotationCenterTransform;

			AxisAlignedBoundingBox currentSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();

			Vector3 controlCenter = GetControlCenter(selectedItem);
			Vector3 rotationCenter = GetRotationCenter(selectedItem, currentSelectedBounds);
			if (mouseDownInfo != null)
			{
				rotationCenter = mouseDownInfo.SelectedObjectRotationCenter;
				controlCenter = mouseDownInfo.ControlCenter;
			}

			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(rotationCenter);
			double lengthFromCenterToControl = (rotationCenter - controlCenter).Length;

			radius = lengthFromCenterToControl * (1 / distBetweenPixelsWorldSpace);

			rotationTransformScale = distBetweenPixelsWorldSpace;
			rotationCenterTransform = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * Matrix4X4.CreateTranslation(rotationCenter);

			var center = Vector3Ex.Transform(Vector3.Zero, rotationCenterTransform);

			switch (RotationAxis)
			{
				case 0:
					{
						rotationCenterTransform =
							Matrix4X4.CreateTranslation(-center)
							* Matrix4X4.CreateRotation(new Vector3(0, -MathHelper.Tau / 4, 0))
							* Matrix4X4.CreateRotation(new Vector3(-MathHelper.Tau / 4, 0, 0))
							* rotationCenterTransform;

						var center2 = Vector3Ex.Transform(Vector3.Zero, rotationCenterTransform);
						rotationCenterTransform *= Matrix4X4.CreateTranslation(center - center2);
					}

					break;

				case 1:
					{
						rotationCenterTransform =
							Matrix4X4.CreateTranslation(-center)
							* Matrix4X4.CreateRotation(new Vector3(MathHelper.Tau / 4, 0, 0))
							* Matrix4X4.CreateRotation(new Vector3(0, MathHelper.Tau / 4, 0))
							* rotationCenterTransform;

						var center2 = Vector3Ex.Transform(Vector3.Zero, rotationCenterTransform);
						rotationCenterTransform *= Matrix4X4.CreateTranslation(center - center2);
					}

					break;

				case 2:
					break;
			}

			return rotationCenterTransform;
		}

		private int GetSnapIndex(IObject3D selectedItem, int numSnapPoints)
		{
			// If we have the control grabbed
			if (mouseMoveInfo != null
				&& mouseDownInfo != null)
			{
				double angleAroundPoint = MathHelper.DegreesToRadians(5);
				// check if we are within the snap control area
				double snappingRadians = MathHelper.Tau / numSnapPoints;

				for (int i = 0; i < numSnapPoints; i++)
				{
					double markAngle = i * snappingRadians;

					double differenceAngle = DeltaAngle(markAngle, SnappedRotationAngle);

					if (Math.Abs(differenceAngle) < angleAroundPoint)
					{
						if (mouseMoveInfo != null)
						{
							Matrix4X4 rotationCenterTransform = GetRotationTransform(selectedItem, out double radius);

							double innerRadius = radius + ringWidth / 2;
							double outerRadius = innerRadius + ringWidth;
							double snappingMarkRadius = outerRadius + 20;

							var center = Vector3Ex.Transform(Vector3.Zero, rotationCenterTransform);
							if (Math.Abs((mouseMoveInfo.HitPosition - center).Length - rotationTransformScale * snappingMarkRadius) < 20 * rotationTransformScale)
							{
								return i;
							}
						}
					}
				}
			}

			return -1;
		}

		private void InteractionLayer_AfterDraw(object sender, DrawEventArgs drawEvent)
		{
			IObject3D selectedItem = RootSelection;
			if (selectedItem != null
				&& mouseDownInfo != null)
			{
				Matrix4X4 rotationCenterTransform = GetRotationTransform(selectedItem, out double radius);

				var unitPosition = new Vector3(Math.Cos(mouseDownInfo.AngleOfHit), Math.Sin(mouseDownInfo.AngleOfHit), 0);
				Vector3 anglePosition = Vector3Ex.Transform(unitPosition * (radius + 100), rotationCenterTransform);

				Vector2 angleDisplayPosition = InteractionContext.World.GetScreenPosition(anglePosition);

				var displayAngle = MathHelper.RadiansToDegrees(SnappedRotationAngle);
				if (!RotatingCW && SnappedRotationAngle > 0)
				{
					displayAngle -= 360;
				}

				angleTextControl.Value = displayAngle;
				angleTextControl.OriginRelativeParent = angleDisplayPosition - angleTextControl.LocalBounds.Center;
			}
		}

		private void RotateAroundAxis(IObject3D selectedItem, double rotationAngle)
		{
			var rotationVector = Vector3.Zero;
			rotationVector[RotationAxis] = -rotationAngle;
			var rotationMatrix = Matrix4X4.CreateRotation(rotationVector);

			selectedItem.Matrix = selectedItem.Matrix.ApplyAtPosition(mouseDownInfo.SelectedObjectRotationCenter, rotationMatrix);
		}

		internal class Mouse3DInfo
		{
			internal Mouse3DInfo(Vector3 downPosition, Matrix4X4 selectedObjectTransform, Vector3 selectedObjectRotationCenter, Vector3 controlCenter, int rotationAxis)
			{
				HitPosition = downPosition;
				SelectedObjectTransform = selectedObjectTransform;
				SelectedObjectRotationCenter = selectedObjectRotationCenter;
				ControlCenter = controlCenter;
				DeltaObjectToHit = HitPosition - SelectedObjectRotationCenter;

				AngleOfHit = GetAngleForAxis(DeltaObjectToHit, rotationAxis);
			}

			internal double AngleOfHit { get; set; }

			internal Vector3 ControlCenter { get; private set; }

			internal Vector3 DeltaObjectToHit { get; private set; }

			internal Vector3 HitPosition { get; set; }

			internal Vector3 SelectedObjectRotationCenter { get; private set; }

			internal Matrix4X4 SelectedObjectTransform { get; private set; } = Matrix4X4.Identity;

			internal static double GetAngleForAxis(Vector3 deltaVector, int axisIndex)
			{
				switch (axisIndex)
				{
					case 0:
						return Math.Atan2(deltaVector.Z, deltaVector.Y);

					case 1:
						return Math.Atan2(deltaVector.X, deltaVector.Z);

					default:
						return Math.Atan2(deltaVector.Y, deltaVector.X);
				}
			}
		}
	}
}