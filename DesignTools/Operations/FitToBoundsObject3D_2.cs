/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class FitToBoundsObject3D_2 : TransformWrapperObject3D, IEditorDraw
	{
		public double Width
		{
			get => FitBounds.GetAxisAlignedBoundingBox().XSize;
			set
			{
				var xSize = FitBounds.GetAxisAlignedBoundingBox().XSize;
				FitBounds.Matrix *= Matrix4X4.CreateScale(value / xSize, 1, 1);
				FitBounds.Matrix *= Matrix4X4.CreateTranslation(
					TransformItem.GetAxisAlignedBoundingBox().Center
					- FitBounds.GetAxisAlignedBoundingBox().Center);
			}
		}
		public double Depth
		{
			get => FitBounds.GetAxisAlignedBoundingBox().YSize;
			set
			{
				var ySize = FitBounds.GetAxisAlignedBoundingBox().YSize;
				FitBounds.Matrix *= Matrix4X4.CreateScale(1, value / ySize, 1);
				FitBounds.Matrix *= Matrix4X4.CreateTranslation(
					TransformItem.GetAxisAlignedBoundingBox().Center
					- FitBounds.GetAxisAlignedBoundingBox().Center);
			}
		}
		public double Height
		{
			get => FitBounds.GetAxisAlignedBoundingBox().ZSize;
			set
			{
				var zSize = FitBounds.GetAxisAlignedBoundingBox().ZSize;
				FitBounds.Matrix *= Matrix4X4.CreateScale(1, 1, value / zSize);
				FitBounds.Matrix *= Matrix4X4.CreateTranslation(
					TransformItem.GetAxisAlignedBoundingBox().Center
					- FitBounds.GetAxisAlignedBoundingBox().Center);
			}
		}

		private IObject3D FitBounds => Children.Last();

		[Description("Set the rules for how to maintain the part while scaling.")]
		public MaintainRatio MaintainRatio { get; set; } = MaintainRatio.X_Y;
		[Description("Allows you turn turn on and off applying the fit to the x axis.")]
		public bool StretchX { get; set; } = true;
		[Description("Allows you turn turn on and off applying the fit to the y axis.")]
		public bool StretchY { get; set; } = true;
		[Description("Allows you turn turn on and off applying the fit to the z axis.")]
		public bool StretchZ { get; set; } = true;

		public FitToBoundsObject3D_2()
		{
			Name = "Fit to Bounds".Localize();
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateType.InvalidateType == InvalidateType.Properties
				&& invalidateType.Source == this)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public static FitToBoundsObject3D_2 Create(IObject3D itemToFit)
		{
			var fitToBounds = new FitToBoundsObject3D_2();
			var aabb = itemToFit.GetAxisAlignedBoundingBox();

			var bounds = new Object3D()
			{
				Visible = true,
				Color = new Color(Color.Red, 100),
				Mesh = PlatonicSolids.CreateCube()
			};

			// add all the children
			var scaleItem = new Object3D();
			fitToBounds.Children.Add(scaleItem);
			scaleItem.Children.Add(itemToFit);
			fitToBounds.Children.Add(bounds);

			fitToBounds.Width = aabb.XSize;
			fitToBounds.Depth = aabb.YSize;
			fitToBounds.Height = aabb.ZSize;

			return fitToBounds;
		}

		public void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");
			using (RebuildLock())
			{
				var aabb = this.GetAxisAlignedBoundingBox();

				AdjustChildSize(null, null);

				if (aabb.ZSize > 0)
				{
					// If the part was already created and at a height, maintain the height.
					PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
				}
			}

			base.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			var aabb = base.GetAxisAlignedBoundingBox(matrix);
			var size = aabb.Size;

			if (StretchX)
			{
				size.X = Width;
			}
			if (StretchY)
			{
				size.Y = Depth;
			}
			if (StretchZ)
			{
				size.Z = Height;
			}

			var half = size / 2;
			return new AxisAlignedBoundingBox(aabb.Center - half, aabb.Center + half);
		}

		private void AdjustChildSize(object sender, EventArgs e)
		{
			var aabb = SourceItem.GetAxisAlignedBoundingBox();
			TransformItem.Matrix = Matrix4X4.Identity;
			var scale = Vector3.One;
			if (StretchX)
			{
				scale.X = Width / aabb.XSize;
			}
			if (StretchY)
			{
				scale.Y = Depth / aabb.YSize;
			}
			if (StretchZ)
			{
				scale.Z = Height / aabb.ZSize;
			}

			switch (MaintainRatio)
			{
				case MaintainRatio.None:
					break;
				case MaintainRatio.X_Y:
					var minXy = Math.Min(scale.X, scale.Y);
					scale.X = minXy;
					scale.Y = minXy;
					break;
				case MaintainRatio.X_Y_Z:
					var minXyz = Math.Min(Math.Min(scale.X, scale.Y), scale.Z);
					scale.X = minXyz;
					scale.Y = minXyz;
					scale.Z = minXyz;
					break;
			}

			TransformItem.Matrix = Object3DExtensions.ApplyAtPosition(TransformItem.Matrix, aabb.Center, Matrix4X4.CreateScale(scale));
		}

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				var aabb = SourceItem.GetAxisAlignedBoundingBox();

				var center = aabb.Center;
				var worldMatrix = this.WorldMatrix();

				var minXyz = center - new Vector3(Width / 2, Depth / 2, Height / 2);
				var maxXyz = center + new Vector3(Width / 2, Depth / 2, Height / 2);
				var bounds = new AxisAlignedBoundingBox(minXyz, maxXyz);
				//var leftW = Vector3.Transform(, worldMatrix);
				var right = Vector3.Transform(center + new Vector3(Width / 2, 0, 0), worldMatrix);
				// layer.World.Render3DLine(left, right, Agg.Color.Red);
				layer.World.RenderAabb(bounds, worldMatrix, Agg.Color.Red, 1, 1);
			}
		}
	}
}