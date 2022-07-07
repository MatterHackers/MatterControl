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
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracerNS;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MoveInZControl : Object3DControl
	{
		public IObject3D ActiveSelectedItem { get; set; }

		private PlaneShape hitPlane;
		private Vector3 initialHitPosition;
		private readonly Mesh upArrowMesh;
		private AxisAlignedBoundingBox mouseDownSelectedBounds;
		private Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
		private readonly double distToStart = 5 * GuiWidget.DeviceScale;
		private readonly double lineLength = 55 * GuiWidget.DeviceScale;
		private readonly List<Vector2> lines = new List<Vector2>();
		private readonly double upArrowSize = 7 * GuiWidget.DeviceScale;
		private readonly InlineEditControl zHeightDisplayInfo;
		private bool hadClickOnControl;
		private readonly ThemeConfig theme;

		public override string UiHint => "Type 'Esc' to cancel".Localize();
	
		public MoveInZControl(IObject3DControlContext context)
			: base(context)
		{
			theme = AppContext.Theme;
			Name = "MoveInZControl";
			zHeightDisplayInfo = new InlineEditControl()
			{
				ForceHide = () =>
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
				},
				GetDisplayString = (value) => "{0:0.0#}".FormatWith(value)
			};

			zHeightDisplayInfo.VisibleChanged += (s, e) =>
			{
				if (!zHeightDisplayInfo.Visible)
				{
					hadClickOnControl = false;
				}
			};

			zHeightDisplayInfo.EditComplete += (s, e) =>
			{
				var selectedItem = RootSelection;

				Matrix4X4 startingTransform = selectedItem.Matrix;

				var newZPosition = zHeightDisplayInfo.Value;

				if (Object3DControlContext.SnapGridDistance > 0)
				{
					// snap this position to the grid
					double snapGridDistance = Object3DControlContext.SnapGridDistance;

					// snap this position to the grid
					newZPosition = ((int)((newZPosition / snapGridDistance) + .5)) * snapGridDistance;
				}

				AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
				var moveAmount = newZPosition - originalSelectedBounds.MinXYZ.Z;

				if (moveAmount != 0)
				{
					selectedItem.Matrix *= Matrix4X4.CreateTranslation(0, 0, moveAmount);
					Invalidate();
				}

				context.Scene.AddTransformSnapshot(startingTransform);
			};

			Object3DControlContext.GuiSurface.AddChild(zHeightDisplayInfo);

			DrawOnTop = true;

			using (Stream arrowStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "up_pointer.stl")))
			{
				upArrowMesh = StlProcessing.Load(arrowStream, CancellationToken.None);
			}

			CollisionVolume = upArrowMesh.CreateBVHData();

			Object3DControlContext.GuiSurface.BeforeDraw += Object3DControl_BeforeDraw;
		}

		public override void Dispose()
		{
			zHeightDisplayInfo.Close();
			Object3DControlContext.GuiSurface.BeforeDraw -= Object3DControl_BeforeDraw;
		}

		bool ShouldDrawMoveControls()
		{
			bool shouldDrawMoveControls = true;
			if (Object3DControlContext.SelectedObject3DControl != null
				&& Object3DControlContext.SelectedObject3DControl as MoveInZControl == null)
			{
				shouldDrawMoveControls = false;
			}
			return shouldDrawMoveControls;
		}

		public override void Draw(DrawGlContentEventArgs e)
		{
			bool shouldDrawMoveControls = ShouldDrawMoveControls();

			var selectedItem = RootSelection;
			if (selectedItem != null)
			{
				if (shouldDrawMoveControls)
				{
					// don't draw if any other control is dragging
					if (MouseIsOver || MouseDownOnControl)
					{
						GLHelper.Render(upArrowMesh, theme.PrimaryAccentColor, TotalTransform, RenderTypes.Shaded);
					}
					else
					{
						GLHelper.Render(upArrowMesh, theme.TextColor, TotalTransform, RenderTypes.Shaded);
					}
				}
			}

			base.Draw(e);
		}

		public Vector3 GetTopPosition(IObject3D selectedItem)
		{
			AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
			if (originalSelectedBounds.MinXYZ.X != double.PositiveInfinity)
			{
				return new Vector3(originalSelectedBounds.Center.X, originalSelectedBounds.Center.Y, originalSelectedBounds.MaxXYZ.Z);
			}

			return Vector3.Zero;
		}

		public override void OnMouseDown(Mouse3DEventArgs mouseEvent3D)
		{
			var selectedItem = RootSelection;

			if (mouseEvent3D.info != null
				&& selectedItem != null)
			{
				hadClickOnControl = true;
				ActiveSelectedItem = selectedItem;

				zHeightDisplayInfo.Visible = true;

				var upNormal = Vector3.UnitZ;
				var sideNormal = upNormal.Cross(mouseEvent3D.MouseRay.directionNormal).GetNormal();
				var planeNormal = upNormal.Cross(sideNormal).GetNormal();
				hitPlane = new PlaneShape(new Plane(planeNormal, mouseEvent3D.info.HitPosition), null);

				initialHitPosition = mouseEvent3D.info.HitPosition;
				transformOnMouseDown = selectedItem.Matrix;
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
				zHeightDisplayInfo.Visible = true;
			}
			else if (!hadClickOnControl)
			{
				zHeightDisplayInfo.Visible = false;
			}

			if (MouseDownOnControl && hitPlane != null)
			{
				IntersectInfo info = hitPlane.GetClosestIntersectionWithinRayDistanceRange(mouseEvent3D.MouseRay);

				if (info != null
					&& selectedItem != null
					&& mouseDownSelectedBounds != null)
				{
					var delta = info.HitPosition.Z - initialHitPosition.Z;

					double newZPosition = mouseDownSelectedBounds.MinXYZ.Z + delta;

					if (Object3DControlContext.SnapGridDistance > 0)
					{
						// snap this position to the grid
						double snapGridDistance = Object3DControlContext.SnapGridDistance;

						// snap this position to the grid
						newZPosition = ((int)((newZPosition / snapGridDistance) + .5)) * snapGridDistance;
					}

					AxisAlignedBoundingBox originalSelectedBounds = selectedItem.GetAxisAlignedBoundingBox();
					var moveAmount = newZPosition - originalSelectedBounds.MinXYZ.Z;

					if (moveAmount != 0)
					{
						selectedItem.Matrix *= Matrix4X4.CreateTranslation(0, 0, moveAmount);
						Invalidate();
					}
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

			Vector3 boxCenter = topPosition;
			boxCenter.Z += (10 * GuiWidget.DeviceScale + upArrowSize / 2) * distBetweenPixelsWorldSpace;

			var centerMatrix = Matrix4X4.CreateTranslation(boxCenter);
			TotalTransform = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace * GuiWidget.DeviceScale) * centerMatrix;

			lines.Clear();
			// left lines
			// the lines on the bed
			var bedPosition = new Vector3(topPosition.X, topPosition.Y, 0);
			lines.Add(Object3DControlContext.World.GetScreenPosition(bedPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[0].X + lineLength, lines[0].Y));

			lines.Add(Object3DControlContext.World.GetScreenPosition(bottomPosition + new Vector3(distToStart * distBetweenPixelsWorldSpace, 0, 0)));
			lines.Add(new Vector2(lines[2].X + lineLength, lines[2].Y));
		}

		private void Object3DControl_BeforeDraw(object sender, DrawEventArgs drawEvent)
		{
			var selectedItem = RootSelection;

			if (selectedItem != null
				&& lines.Count > 2)
			{
				if (zHeightDisplayInfo.Visible)
				{
					for (int i = 0; i < lines.Count; i += 2)
					{
						// draw the measure line
						drawEvent.Graphics2D.Line(lines[i], lines[i + 1], theme.TextColor);
					}

					for (int i = 0; i < lines.Count; i += 4)
					{
						drawEvent.Graphics2D.DrawMeasureLine((lines[i] + lines[i + 1]) / 2, (lines[i + 2] + lines[i + 3]) / 2, LineArrows.Both, theme);
					}

					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox();

					zHeightDisplayInfo.Value = selectedBounds.MinXYZ.Z;
					zHeightDisplayInfo.OriginRelativeParent = lines[1] + new Vector2(10, -zHeightDisplayInfo.LocalBounds.Center.Y);
				}
			}
		}

		public override AxisAlignedBoundingBox GetWorldspaceAABB()
		{
			AxisAlignedBoundingBox box = AxisAlignedBoundingBox.Empty();

			bool shouldDrawScaleControls = ShouldDrawMoveControls();
			var selectedItem = RootSelection;

			if (selectedItem != null)
			{
				if (shouldDrawScaleControls)
				{
					box = AxisAlignedBoundingBox.Union(box, upArrowMesh.GetAxisAlignedBoundingBox().NewTransformed(TotalTransform));
				}
			}

			return box;
		}

	}
}
