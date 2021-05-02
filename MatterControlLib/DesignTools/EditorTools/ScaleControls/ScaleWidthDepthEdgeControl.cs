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

namespace MatterHackers.Plugins.EditorTools
{
	public class ScaleWidthDepthEdgeControl : Object3DControl
	{
		/// <summary>
		/// Edge starting from the back (+y) going ccw
		/// </summary>
		private readonly int edgeIndex;

		private readonly Mesh minXminYMesh;

		private readonly double selectCubeSize = 7 * GuiWidget.DeviceScale;

		private readonly ThemeConfig theme;

		private readonly InlineEditControl xValueDisplayInfo;

		private readonly InlineEditControl yValueDisplayInfo;

		private bool hadClickOnControl;

		private PlaneShape hitPlane;

		private Vector3 initialHitPosition;

		private ScaleController scaleController = new ScaleController();

		public ScaleWidthDepthEdgeControl(IObject3DControlContext context, int edgeIndex)
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
			xValueDisplayInfo.Close();
			yValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleWidthDepthEdgeControl == null)
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
					if (MouseIsOver || MouseDownOnControl)
					{
						GLHelper.Render(minXminYMesh, theme.PrimaryAccentColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(minXminYMesh, theme.TextColor.Blend(theme.BackgroundColor, .35).WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
				}

				if (hitPlane != null)
				{
					//Object3DControlContext.World.RenderPlane(hitPlane.Plane, Color.Red, true, 50, 3);
					//Object3DControlContext.World.RenderPlane(initialHitPosition, hitPlane.Plane.Normal, Color.Red, true, 50, 3);
				}
			}

			if (MouseIsOver || MouseDownOnControl)
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

				xValueDisplayInfo.Visible = true;
				yValueDisplayInfo.Visible = true;

				var edge = ObjectSpace.GetEdgePosition(selectedItem, edgeIndex);
				var otherSide = ObjectSpace.GetEdgePosition(selectedItem, edgeIndex + 2);

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
				xValueDisplayInfo.Visible = true;
				yValueDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl || scaleController.HasChange)
			{
				xValueDisplayInfo.Visible = false;
				yValueDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl && hitPlane != null)
			{
				var info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

				if (info != null
					&& selectedItem != null)
				{
					var delta = info.HitPosition - initialHitPosition;

					var lockedEdge = ObjectSpace.GetEdgePosition(selectedItem, edgeIndex + 2);

					var stretchDirection = (ObjectSpace.GetEdgePosition(selectedItem, edgeIndex) - lockedEdge).GetNormal();
					var deltaAlongStretch = stretchDirection.Dot(delta);

					// scale it
					var newSize = new Vector2(scaleController.InitialState.Width, scaleController.InitialState.Depth);
					if (edgeIndex % 2 == 1)
					{
						newSize.X += deltaAlongStretch;
						newSize.X = Math.Max(Math.Max(newSize.X, .001), Object3DControlContext.SnapGridDistance);
					}
					else
					{
						newSize.Y += deltaAlongStretch;
						newSize.Y = Math.Max(Math.Max(newSize.Y, .001), Object3DControlContext.SnapGridDistance);
					}

					if (Object3DControlContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = Object3DControlContext.SnapGridDistance;

						// snap this position to the grid
						if (edgeIndex % 2 == 1)
						{
							newSize.X = ((int)((newSize.X / snapGridDistance) + .5)) * snapGridDistance;
						}
						else
						{
							newSize.Y = ((int)((newSize.Y / snapGridDistance) + .5)) * snapGridDistance;
						}
					}

					if (edgeIndex % 2 == 1)
					{
						scaleController.ScaleWidth(newSize.X);
					}
					else
					{
						scaleController.ScaleDepth(newSize.Y);
					}

					await selectedItem.Rebuild();

					// and keep the locked edge in place
					Vector3 newLockedEdge = ObjectSpace.GetEdgePosition(selectedItem, edgeIndex + 2);

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedEdge - newLockedEdge);

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent3D, mouseIsOver);
		}

		public override void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			if (hadClickOnControl)
			{
				if (RootSelection is IObjectWithWidthAndDepth widthDepthItem
					&& (widthDepthItem.Width != scaleController.InitialState.Width
						|| widthDepthItem.Depth != scaleController.InitialState.Depth))
				{
					scaleController.EditComplete();
				}
				Object3DControlContext.Scene.ShowSelectionShadow = true;
			}

			base.OnMouseUp(mouseEvent3D);
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			// create the transform for the box
			Vector3 edgePosition = ObjectSpace.GetEdgePosition(selectedItem, edgeIndex);

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

			var rotation = Matrix4X4.CreateRotation(new Quaternion(selectedItem.Matrix));

			var centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = rotation * Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;
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

		private async void EditComplete(object s, EventArgs e)
		{
			var newWidth = xValueDisplayInfo.Value != 0 ? xValueDisplayInfo.Value : scaleController.FinalState.Width;
			var newDepth = yValueDisplayInfo.Value != 0 ? yValueDisplayInfo.Value : scaleController.FinalState.Depth;

			Vector3 lockedEdge = ObjectSpace.GetEdgePosition(ActiveSelectedItem, edgeIndex + 2);
			if (edgeIndex % 2 == 1)
			{
				if (newWidth == scaleController.FinalState.Width)
				{
					return;
				}
				scaleController.ScaleWidth(newWidth);
			}
			else
			{
				if (newDepth == scaleController.FinalState.Depth)
				{
					return;
				}
				scaleController.ScaleDepth(newDepth);
			}
			await ActiveSelectedItem.Rebuild();
			// and keep the locked edge in place
			Vector3 newLockedEdge = ObjectSpace.GetEdgePosition(ActiveSelectedItem, edgeIndex + 2);
			ActiveSelectedItem.Translate(lockedEdge - newLockedEdge);

			scaleController.EditComplete();
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
				corner[i] = ObjectSpace.GetCornerPosition(selectedItem, edgeIndex + i);
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

					if (edgeIndex % 2 == 1)
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
			}
		}
	}

	public static class ObjectSpace
	{
		public static Vector3 GetCornerPosition(IObject3D item, int quadrantIndex)
		{
			quadrantIndex %= 4;
			var originalSelectedBounds = item.GetAxisAlignedBoundingBox(item.Matrix.Inverted);
			var cornerPosition = originalSelectedBounds.GetBottomCorner(quadrantIndex);

			return cornerPosition.Transform(item.Matrix);
		}

		public static Vector3 GetCenterPosition(IObject3D item, Placement placement)
		{
			var aabb = item.GetAxisAlignedBoundingBox(item.Matrix.Inverted);
			var cornerPosition = aabb.Center;
			switch (placement)
			{
				case Placement.Bottom:
					cornerPosition.Z = aabb.MinXYZ.Z;
					break;
				case Placement.Center:
					cornerPosition.Z = aabb.Center.Z;
					break;
				case Placement.Top:
					cornerPosition.Z = aabb.MaxXYZ.Z;
					break;
			}

			return cornerPosition.Transform(item.Matrix);
		}

		public enum Placement
		{
			Bottom,
			Center,
			Top,
		}

		public static Vector3 GetEdgePosition(IObject3D item, int edegIndex, Placement placement = Placement.Bottom)
		{
			edegIndex %= 4;
			var aabb = item.GetAxisAlignedBoundingBox(item.Matrix.Inverted);
			var edgePosition = default(Vector3);
			double z = 0;
			switch (placement)
			{
				case Placement.Bottom:
					z = aabb.MinXYZ.Z;
					break;
				case Placement.Center:
					z = aabb.Center.Z;
					break;
				case Placement.Top:
					z = aabb.MaxXYZ.Z;
					break;
			}

			switch (edegIndex)
			{
				case 0:
					edgePosition = new Vector3(aabb.Center.X, aabb.MaxXYZ.Y, z);
					break;

				case 1:
					edgePosition = new Vector3(aabb.MinXYZ.X, aabb.Center.Y, z);
					break;

				case 2:
					edgePosition = new Vector3(aabb.Center.X, aabb.MinXYZ.Y, z);
					break;

				case 3:
					edgePosition = new Vector3(aabb.MaxXYZ.X, aabb.Center.Y, z);
					break;
			}

			return edgePosition.Transform(item.Matrix);
		}
	}
}