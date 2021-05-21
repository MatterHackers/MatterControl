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
	public class ScaleWidthDepthCornerControl : Object3DControl
	{
		/// <summary>
		/// Edge starting from the back (+y) going ccw
		/// </summary>
		private readonly int quadrantIndex;

		private readonly Mesh minXminYMesh;

		private readonly double selectCubeSize = 7 * GuiWidget.DeviceScale;

		private readonly ThemeConfig theme;

		private readonly InlineEditControl xValueDisplayInfo;

		private readonly InlineEditControl yValueDisplayInfo;

		private bool hadClickOnControl;

		private PlaneShape hitPlane;

		private Vector3 initialHitPosition;

		private ScaleController scaleController = new ScaleController();

		public ScaleWidthDepthCornerControl(IObject3DControlContext object3DControlContext, int quadrant)
			: base(object3DControlContext)
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

			this.quadrantIndex = quadrant;

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
			yValueDisplayInfo.Close();
			xValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleWidthDepthCornerControl == null)
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
						GLHelper.Render(minXminYMesh, theme.TextColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
				}

				if (hitPlane != null)
				{
					//Object3DControlContext.World.RenderPlane(hitPlane.Plane, Color.Red, true, 30, 3);
					//Object3DControlContext.World.RenderPlane(initialHitPosition, hitPlane.Plane.Normal, Color.Red, true, 30, 3);
				}

				if (e != null)
				{
					Vector3 startPosition = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex);
					Vector3 endPosition = ObjectSpace.GetCornerPosition(selectedItem, (quadrantIndex + 1) % 4);
					Object3DControlContext.World.Render3DLine(startPosition, endPosition, theme.TextColor.WithAlpha(e.Alpha0to255), e.ZBuffered, GuiWidget.DeviceScale);
				}

				if (MouseIsOver || MouseDownOnControl)
				{
					DrawMeasureLines(e, quadrantIndex);
					DrawMeasureLines(e, quadrantIndex + 1);
				}
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

				var edge0 = ObjectSpace.GetEdgePosition(selectedItem, quadrantIndex);
				var edge1 = ObjectSpace.GetEdgePosition(selectedItem, quadrantIndex + 1);
				var edge3 = ObjectSpace.GetEdgePosition(selectedItem, quadrantIndex + 2);

				var normal01 = (edge1 - edge0).GetNormal();
				var normal03 = (edge3 - edge0).GetNormal();
				var planeNormal = normal01.Cross(normal03).GetNormal();
				hitPlane = new PlaneShape(new Plane(planeNormal, edge0), null);

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
					var lockedEdge = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 2);

					var delta = info.HitPosition - initialHitPosition;

					var corner0 = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex);
					var corner1 = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 1);
					var corner3 = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 3);
					var direction01 = (corner0 - corner1).GetNormal();
					var direction03 = (corner0 - corner3).GetNormal();

					var deltaAlong01 = direction01.Dot(delta);
					var deltaAlong03 = direction03.Dot(delta);

					// scale it
					var newSize = new Vector2(scaleController.InitialState.Width, scaleController.InitialState.Depth);
					if (quadrantIndex % 2 == 0)
					{
						newSize.X += deltaAlong01;
						newSize.X = Math.Max(Math.Max(newSize.X, .001), Object3DControlContext.SnapGridDistance);

						newSize.Y += deltaAlong03;
						newSize.Y = Math.Max(Math.Max(newSize.Y, .001), Object3DControlContext.SnapGridDistance);
					}
					else
					{
						newSize.X += deltaAlong03;
						newSize.X = Math.Max(Math.Max(newSize.X, .001), Object3DControlContext.SnapGridDistance);

						newSize.Y += deltaAlong01;
						newSize.Y = Math.Max(Math.Max(newSize.Y, .001), Object3DControlContext.SnapGridDistance);
					}

					if (Object3DControlContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = Object3DControlContext.SnapGridDistance;

						// snap this position to the grid
						newSize.X = ((int)((newSize.X / snapGridDistance) + .5)) * snapGridDistance;
						newSize.Y = ((int)((newSize.Y / snapGridDistance) + .5)) * snapGridDistance;
					}

					scaleController.ScaleWidthDepth(newSize.X, newSize.Y);

					await selectedItem.Rebuild();

					// and keep the locked edge in place
					var newLockedEdge = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex + 2);

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
			Vector3 cornerPosition = ObjectSpace.GetCornerPosition(selectedItem, quadrantIndex);

			Vector3 boxCenter = cornerPosition;

			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(cornerPosition);
			switch (quadrantIndex)
			{
				case 0:
					boxCenter.X += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					boxCenter.Y += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;

				case 1:
					boxCenter.X -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					boxCenter.Y += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;

				case 2:
					boxCenter.X -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					boxCenter.Y -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;

				case 3:
					boxCenter.X += selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					boxCenter.Y -= selectCubeSize / 2 * distBetweenPixelsWorldSpace;
					break;
			}

			boxCenter.Z += selectCubeSize / 2 * distBetweenPixelsWorldSpace;

			var rotation = Matrix4X4.CreateRotation(new Quaternion(selectedItem.Matrix));

			var centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = rotation * Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;
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

		private async void EditComplete(object s, EventArgs e)
		{
			var newWidth = xValueDisplayInfo.Value != 0 ? xValueDisplayInfo.Value : scaleController.FinalState.Width;
			var newDepth = yValueDisplayInfo.Value != 0 ? yValueDisplayInfo.Value : scaleController.FinalState.Depth;

			if (newWidth == scaleController.FinalState.Width
				&& newDepth == scaleController.FinalState.Depth)
			{
				return;
			}

			var lockedEdge = ObjectSpace.GetCornerPosition(ActiveSelectedItem, quadrantIndex + 2);
			scaleController.ScaleWidthDepth(newWidth, newDepth);
			await ActiveSelectedItem.Rebuild();
			// and keep the locked edge in place
			var newLockedEdge = ObjectSpace.GetCornerPosition(ActiveSelectedItem, quadrantIndex + 2);
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

		private (Vector3 start0, Vector3 end0, Vector3 start1, Vector3 end1) GetMeasureLine(int quadrant)
		{
			var selectedItem = RootSelection;
			var corner = new Vector3[4];
			var screen = new Vector3[4];
			for (int i = 0; i < 4; i++)
			{
				corner[i] = ObjectSpace.GetCornerPosition(selectedItem, quadrant + i);
				screen[i] = Object3DControlContext.World.WorldToScreenSpace(corner[i]);
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

		public static void SetWidthDepthUndo(IObject3D selectedItem, UndoBuffer undoBuffer, Vector2 doWidthDepth, Matrix4X4 doMatrix, Vector2 undoWidthDepth, Matrix4X4 undoMatrix)
		{
			if (selectedItem is IObjectWithWidthAndDepth widthDepthItem)
			{
				undoBuffer.AddAndDo(new UndoRedoActions(async () =>
				{
					widthDepthItem.Width = undoWidthDepth.X;
					widthDepthItem.Depth = undoWidthDepth.Y;
					await selectedItem.Rebuild();
					selectedItem.Matrix = undoMatrix;
					selectedItem?.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.DisplayValues));
				},
				async () =>
				{
					widthDepthItem.Width = doWidthDepth.X;
					widthDepthItem.Depth = doWidthDepth.Y;
					await selectedItem.Rebuild();
					selectedItem.Matrix = doMatrix;
					selectedItem?.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.DisplayValues));
				}));
			}
		}
	}
}