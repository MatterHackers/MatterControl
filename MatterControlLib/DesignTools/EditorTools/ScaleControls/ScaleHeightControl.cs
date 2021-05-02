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
	public class ScaleHeightControl : Object3DControl
	{
		private readonly double arrowSize = 7 * GuiWidget.DeviceScale;

		private readonly InlineEditControl heightValueDisplayInfo;

		private readonly ThemeConfig theme;

		private readonly Mesh topScaleMesh;

		private IObject3D activeSelectedItem;

		private bool hadClickOnControl;

		private PlaneShape hitPlane;

		private Vector3 initialHitPosition;

		private Vector3 originalPointToMove;

		private ScaleController scaleController;
		private Func<double> getDiameter;
		private readonly Action<double> setDiameter;

		public ScaleHeightControl(IObject3DControlContext context, Func<double> getDiameter = null, Action<double> setDiameter = null)
			: base(context)
		{
			this.getDiameter = getDiameter;
			this.setDiameter = setDiameter;
			theme = MatterControl.AppContext.Theme;

			scaleController = new ScaleController(getDiameter, setDiameter);
			
			heightValueDisplayInfo = new InlineEditControl()
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

			heightValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!heightValueDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			heightValueDisplayInfo.EditComplete += async (s, e) =>
			{
				var selectedItem = activeSelectedItem;

				if (selectedItem is IObjectWithHeight heightObject
					&& heightValueDisplayInfo.Value != heightObject.Height)
				{
					await selectedItem.Rebuild();

					throw new NotImplementedException("Need to have the object set by the time we edit complete for undo to be right");
					Invalidate();

					scaleController.EditComplete();
				}
			};

			Object3DControlContext.GuiSurface.AddChild(heightValueDisplayInfo);

			DrawOnTop = true;

			topScaleMesh = PlatonicSolids.CreateCube(arrowSize, arrowSize, arrowSize);

			CollisionVolume = topScaleMesh.CreateBVHData();

			Object3DControlContext.GuiSurface.BeforeDraw += Object3DControl_BeforeDraw;
		}

		private double DistToStart => 5 * GuiWidget.DeviceScale;

		private double LineLength => 55 * GuiWidget.DeviceScale;

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
			heightValueDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			var selectedItem = RootSelection;

			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as ScaleHeightControl == null)
			{
				shouldDrawScaleControls = false;
			}

			if (selectedItem != null)
			{
				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					if (MouseIsOver || MouseDownOnControl)
					{
						GLHelper.Render(topScaleMesh, theme.PrimaryAccentColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(topScaleMesh, theme.TextColor.WithAlpha(e.Alpha0to255), TotalTransform, RenderTypes.Shaded);
					}
				}

				if (e != null)
				{
					// evaluate the position of the up line to draw
					Vector3 topPosition = GetTopPosition(selectedItem);

					var bottomPosition = GetBottomPosition(selectedItem);

					// render with z-buffer full black
					Frustum clippingFrustum = Object3DControlContext.World.GetClippingFrustum();

					if (e.ZBuffered)
					{
						Object3DControlContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, theme.TextColor, width: GuiWidget.DeviceScale);
					}
					else
					{
						// render on top of everything very lightly
						Object3DControlContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, theme.TextColor.WithAlpha(Constants.LineAlpha), false, GuiWidget.DeviceScale);
					}
				}

				if (hitPlane != null)
				{
					//Object3DControlContext.World.RenderPlane(hitPlane.Plane, Color.Red, true, 50, 3);
					//Object3DControlContext.World.RenderPlane(initialHitPosition, hitPlane.Plane.Normal, Color.Red, true, 50, 3);
				}
			}

			base.Draw(e);
		}

		public Vector3 GetBottomPosition(IObject3D selectedItem)
		{
			var meshBounds = selectedItem.GetAxisAlignedBoundingBox(selectedItem.Matrix.Inverted);
			var bottom = new Vector3(meshBounds.Center.X, meshBounds.Center.Y, meshBounds.MinXYZ.Z);

			var worldBottom = bottom.Transform(selectedItem.Matrix);
			return worldBottom;
		}

		public Vector3 GetTopPosition(IObject3D selectedItem)
		{
			var meshBounds = selectedItem.GetAxisAlignedBoundingBox(selectedItem.Matrix.Inverted);
			var top = new Vector3(meshBounds.Center.X, meshBounds.Center.Y, meshBounds.MaxXYZ.Z);

			var worldTop = top.Transform(selectedItem.Matrix);
			return worldTop;
		}

		public override void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			if (mouseEvent3D.info != null 
				&& mouseEvent3D.MouseEvent2D.Button == MouseButtons.Left
				&& Object3DControlContext.Scene.SelectedItem != null)
			{
				hadClickOnControl = true;
				activeSelectedItem = RootSelection;

				heightValueDisplayInfo.Visible = true;

				var selectedItem = activeSelectedItem;

				var bottomPosition = GetBottomPosition(selectedItem);
				var topPosition = GetTopPosition(selectedItem);
				originalPointToMove = topPosition;

				var upNormal = (topPosition - bottomPosition).GetNormal();
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

			activeSelectedItem = selectedItem;
			if (MouseIsOver)
			{
				heightValueDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl)
			{
				heightValueDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl)
			{
				IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

				if (info != null
					&& selectedItem != null)
				{
					var delta = info.HitPosition - initialHitPosition;

					var bottom = GetBottomPosition(selectedItem);
					var top = GetTopPosition(selectedItem);

					var up = top - bottom;

					var newPosition = originalPointToMove + delta;

					var newSize = (newPosition - bottom).Length;
					double snapGridDistance = Object3DControlContext.SnapGridDistance;
					// if we are about to scale the object to less than 0
					if (up.Dot(info.HitPosition - bottom) < 0)
					{
						newSize = .001;
					}

					if (snapGridDistance > 0)
					{
						newSize = System.Math.Max(newSize, snapGridDistance);
						// snap this position to the grid
						newSize = ((int)((newSize / snapGridDistance) + .5)) * snapGridDistance;
					}

					scaleController.ScaleHeight(newSize);

					await selectedItem.Rebuild();

					var postScaleBottom = GetBottomPosition(selectedItem);

					selectedItem.Translate(bottom - postScaleBottom);

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent3D, mouseIsOver);
		}

		public override void OnMouseUp(Mouse3DEventArgs mouseEvent3D)
		{
			if (MouseDownOnControl)
			{
				if (activeSelectedItem is IObjectWithHeight heightObject
					&& heightObject.Height != scaleController.InitialState.Height)
				{
					scaleController.EditComplete();
				}

				Object3DControlContext.Scene.ShowSelectionShadow = true;
			}

			base.OnMouseUp(mouseEvent3D);
		}

		public override void SetPosition(IObject3D selectedItem, MeshSelectInfo selectInfo)
		{
			var topPosition = GetTopPosition(selectedItem);
			double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);

			Vector3 arrowCenter = topPosition;
			arrowCenter.Z += arrowSize / 2 * distBetweenPixelsWorldSpace;

			var rotation = Matrix4X4.CreateRotation(new Quaternion(selectedItem.Matrix));
			TotalTransform = rotation * Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * Matrix4X4.CreateTranslation(arrowCenter);
		}

		private void Object3DControl_BeforeDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (heightValueDisplayInfo.Visible)
				{
					var topPosition = GetTopPosition(selectedItem);
					double distBetweenPixelsWorldSpace = Object3DControlContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);
					var topScreen = Object3DControlContext.World.GetScreenPosition(topPosition + new Vector3(DistToStart * distBetweenPixelsWorldSpace, 0, 0));

					var bottomPosition = GetBottomPosition(selectedItem);
					var bottomScreen = Object3DControlContext.World.GetScreenPosition(bottomPosition + new Vector3(DistToStart * distBetweenPixelsWorldSpace, 0, 0));

					var perpRight = (topScreen - bottomScreen).GetPerpendicularRight().GetNormal() * LineLength;
					if (perpRight.X < 0)
					{
						perpRight = -perpRight;
					}

					var lines = new List<Vector2>();
					// left lines
					lines.Add(topScreen);
					lines.Add(topScreen + perpRight);

					lines.Add(bottomScreen);
					lines.Add(bottomScreen + perpRight);

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

					Vector2 heightDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
					if (activeSelectedItem is IObjectWithHeight heightObject)
					{
						heightValueDisplayInfo.Value = heightObject.Height;
					}

					heightValueDisplayInfo.OriginRelativeParent = heightDisplayCenter + new Vector2(10, -heightValueDisplayInfo.LocalBounds.Center.Y);
				}
			}
		}
	}
}