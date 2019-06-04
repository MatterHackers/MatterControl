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
using MatterHackers.Agg.Image;
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
	public class ScaleTopControl : InteractionVolume
	{
		public IObject3D ActiveSelectedItem;
		protected PlaneShape hitPlane;
		protected Vector3 initialHitPosition;
		protected Mesh topScaleMesh;
		protected AxisAlignedBoundingBox mouseDownSelectedBounds;
		protected Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private double distToStart = 5;
		private double lineLength = 55;
		private List<Vector2> lines = new List<Vector2>();
		private Vector3 originalPointToMove;
		private double selectCubeSize = 7 * GuiWidget.DeviceScale;
		private ThemeConfig theme;
		private InlineEditControl zValueDisplayInfo;
		private bool HadClickOnControl;

		public ScaleTopControl(IInteractionVolumeContext context)
			: base(context)
		{
			theme = MatterControl.AppContext.Theme;

			zValueDisplayInfo = new InlineEditControl()
			{
				ForceHide = () => 
				{
					// if the selection changes
					if (RootSelection != ActiveSelectedItem)
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
,
				GetDisplayString = (value) => "{0:0.0}mm".FormatWith(value)
			};

			zValueDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!zValueDisplayInfo.Visible)
				{
					HadClickOnControl = false;
				}
			};

			zValueDisplayInfo.EditComplete += (s, e) =>
			{
				var selectedItem = ActiveSelectedItem;

				Matrix4X4 startingTransform = selectedItem.Matrix;
				var originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				Vector3 topPosition = GetTopPosition(selectedItem);
				Vector3 lockedBottom = new Vector3(topPosition.X, topPosition.Y, originalSelectedBounds.MinXYZ.Z);

				Vector3 newSize = Vector3.Zero;
				newSize.Z = zValueDisplayInfo.Value;
				Vector3 scaleAmount = ScaleCornerControl.GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, InteractionContext.GuiSurface.ModifierKeys);

				Matrix4X4 scale = Matrix4X4.CreateScale(scaleAmount);

				selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

				// and keep the locked edge in place
				AxisAlignedBoundingBox scaledSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				Vector3 newLockedBottom = new Vector3(topPosition.X, topPosition.Y, scaledSelectedBounds.MinXYZ.Z);

				selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedBottom - newLockedBottom);

				Invalidate();

				InteractionContext.Scene.AddTransformSnapshot(startingTransform);
			};

			InteractionContext.GuiSurface.AddChild(zValueDisplayInfo);

			DrawOnTop = true;

			topScaleMesh = PlatonicSolids.CreateCube(selectCubeSize, selectCubeSize, selectCubeSize);

			CollisionVolume = topScaleMesh.CreateTraceData();

			InteractionContext.GuiSurface.AfterDraw += InteractionLayer_AfterDraw;
		}

		public override void DrawGlContent(DrawGlContentEventArgs e)
		{
			bool shouldDrawScaleControls = true;
			var selectedItem = RootSelection;

			if (InteractionContext.SelectedInteractionVolume != null
				&& InteractionContext.SelectedInteractionVolume as ScaleTopControl == null)
			{
				shouldDrawScaleControls = false;
			}

			if (selectedItem != null)
			{
				if (shouldDrawScaleControls)
				{
					// don't draw if any other control is dragging
					if (MouseOver)
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
					double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);
					Vector3 delta = topPosition - bottomPosition;
					Vector3 centerPosition = (topPosition + bottomPosition) / 2;
					Matrix4X4 rotateTransform = Matrix4X4.CreateRotation(new Quaternion(delta, Vector3.UnitX));
					Matrix4X4 scaleTransform = Matrix4X4.CreateScale((topPosition - bottomPosition).Length, distBetweenPixelsWorldSpace, distBetweenPixelsWorldSpace);
					Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(centerPosition);

					Frustum clippingFrustum = InteractionContext.World.GetClippingFrustum();

					if (e.ZBuffered)
					{
						InteractionContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, theme.TextColor);
					}
					else
					{
						// render on top of everything very lightly
						InteractionContext.World.Render3DLine(clippingFrustum, bottomPosition, topPosition, new Color(theme.TextColor, 20), false);
					}
				}
			}

			base.DrawGlContent(e);
		}

		public Vector3 GetTopPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			return new Vector3(originalSelectedBounds.Center.X, originalSelectedBounds.Center.Y, originalSelectedBounds.MaxXYZ.Z);
		}

		public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			if (mouseEvent3D.info != null && InteractionContext.Scene.SelectedItem != null)
			{
				HadClickOnControl = true;
				ActiveSelectedItem = RootSelection;

				zValueDisplayInfo.Visible = true;

				var selectedItem = ActiveSelectedItem;

				double distanceToHit = Vector3Ex.Dot(mouseEvent3D.info.HitPosition, mouseEvent3D.MouseRay.directionNormal);
				hitPlane = new PlaneShape(mouseEvent3D.MouseRay.directionNormal, distanceToHit, null);
				originalPointToMove = GetTopPosition(selectedItem);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = selectedItem.Matrix;
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
				zValueDisplayInfo.Visible = true;
			}
			else if (!HadClickOnControl)
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

					if (InteractionContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = InteractionContext.SnapGridDistance;

						// snap this position to the grid
						newPosition.Z = ((int)((newPosition.Z / snapGridDistance) + .5)) * snapGridDistance;
					}

					Vector3 topPosition = GetTopPosition(selectedItem);
					Vector3 lockedBottom = new Vector3(topPosition.X, topPosition.Y, originalSelectedBounds.MinXYZ.Z);

					Vector3 newSize = Vector3.Zero;
					newSize.Z = newPosition.Z - lockedBottom.Z;

					// scale it
					Vector3 scaleAmount = ScaleCornerControl.GetScalingConsideringShiftKey(originalSelectedBounds, mouseDownSelectedBounds, newSize, InteractionContext.GuiSurface.ModifierKeys);

					Matrix4X4 scale = Matrix4X4.CreateScale(scaleAmount);

					selectedItem.Matrix = selectedItem.ApplyAtBoundsCenter(scale);

					// and keep the locked edge in place
					AxisAlignedBoundingBox scaledSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
					Vector3 newLockedBottom = new Vector3(topPosition.X, topPosition.Y, scaledSelectedBounds.MinXYZ.Z);

					selectedItem.Matrix *= Matrix4X4.CreateTranslation(lockedBottom - newLockedBottom);

					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent3D);
		}

		public override void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			InteractionContext.Scene.AddTransformSnapshot(transformOnMouseDown);
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

			Vector3 topPosition = GetTopPosition(selectedItem);
			Vector3 bottomPosition = new Vector3(topPosition.X, topPosition.Y, selectedBounds.MinXYZ.Z);
			double distBetweenPixelsWorldSpace = InteractionContext.World.GetWorldUnitsPerScreenPixelAtPosition(topPosition);

			Vector3 boxCenter = topPosition;
			boxCenter.Z += selectCubeSize / 2 * distBetweenPixelsWorldSpace;

			Matrix4X4 centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			centerMatrix = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * centerMatrix;
			TotalTransform = centerMatrix;

			lines.Clear();
			// left lines
			lines.Add(InteractionContext.World.GetScreenPosition(topPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[0].X + lineLength, lines[0].Y));

			lines.Add(InteractionContext.World.GetScreenPosition(bottomPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[2].X + lineLength, lines[2].Y));
		}

		private void InteractionLayer_AfterDraw(object sender, DrawEventArgs drawEvent)
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
						DrawMeasureLine(drawEvent.Graphics2D, (lines[i] + lines[i + 1]) / 2, (lines[i + 2] + lines[i + 3]) / 2, LineArrows.Both, theme);
					}

					int j = 0;
					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					Vector2 heightDisplayCenter = (((lines[j] + lines[j + 1]) / 2) + ((lines[j + 2] + lines[j + 3]) / 2)) / 2;
					zValueDisplayInfo.Value = selectedBounds.ZSize;
					zValueDisplayInfo.OriginRelativeParent = heightDisplayCenter + new Vector2(10, -zValueDisplayInfo.LocalBounds.Center.Y);
				}
			}
		}
	}
}