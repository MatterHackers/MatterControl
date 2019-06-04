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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class FitToBoundsObject3D_2 : TransformWrapperObject3D, IEditorDraw
	{
		private Vector3 boundsSize;

		public FitToBoundsObject3D_2()
		{
			Name = "Fit to Bounds".Localize();
		}

		private IObject3D FitBounds => Children.Last();

		[DisplayName("Width")]
		public double SizeX
		{
			get => boundsSize.X;
			set
			{
				boundsSize.X = value;
				if (this.Children.Count() > 0)
				{
					Rebuild();
				}
			}
		}

		[DisplayName("Depth")]
		public double SizeY
		{
			get => boundsSize.Y;
			set
			{
				boundsSize.Y = value;
				if (this.Children.Count() > 0)
				{
					Rebuild();
				}
			}
		}

		[DisplayName("Height")]
		public double SizeZ
		{
			get => boundsSize.Z;
			set
			{
				boundsSize.Z = value;
				if (this.Children.Count() > 0)
				{
					Rebuild();
				}
			}
		}

		[Description("Set the rules for how to maintain the part while scaling.")]
		public MaintainRatio MaintainRatio { get; set; } = MaintainRatio.X_Y;

		[Description("Allows you turn on and off applying the fit to the x axis.")]
		public bool StretchX { get; set; } = true;

		[Description("Allows you turn on and off applying the fit to the y axis.")]
		public bool StretchY { get; set; } = true;

		[Description("Allows you turn on and off applying the fit to the z axis.")]
		public bool StretchZ { get; set; } = true;

		public static async Task<FitToBoundsObject3D_2> Create(IObject3D itemToFit)
		{
			var fitToBounds = new FitToBoundsObject3D_2();
			using (fitToBounds.RebuildLock())
			{
				var startingAabb = itemToFit.GetAxisAlignedBoundingBox();
				itemToFit.Translate(-startingAabb.Center);

				// add the fit item
				var scaleItem = new Object3D();
				fitToBounds.Children.Add(scaleItem);
				scaleItem.Children.Add(itemToFit);

				// create an object that just represents the bounds in the scene
				var fitBounds = new Object3D()
				{
					Visible = false,
					Color = new Color(Color.Red, 100),
					Mesh = PlatonicSolids.CreateCube()
				};
				// add the item that holds the bounds
				fitToBounds.Children.Add(fitBounds);

				fitToBounds.boundsSize.X = startingAabb.XSize;
				fitToBounds.boundsSize.Y = startingAabb.YSize;
				fitToBounds.boundsSize.Z = startingAabb.ZSize;
				await fitToBounds.Rebuild();

				var finalAabb = fitToBounds.GetAxisAlignedBoundingBox();
				fitToBounds.Translate(startingAabb.Center - finalAabb.Center);
			}

			return fitToBounds;
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
			var center = aabb.Center;
			var worldMatrix = this.WorldMatrix();

			var minXyz = center - new Vector3(SizeX / 2, SizeY / 2, SizeZ / 2);
			var maxXyz = center + new Vector3(SizeX / 2, SizeY / 2, SizeZ / 2);
			var bounds = new AxisAlignedBoundingBox(minXyz, maxXyz);
			layer.World.RenderAabb(bounds, worldMatrix, Color.Red, 1, 1);
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			if (Children.Count == 2)
			{
				AxisAlignedBoundingBox bounds;
				using (FitBounds.RebuildLock())
				{
					FitBounds.Visible = true;
					bounds = base.GetAxisAlignedBoundingBox(matrix);
					FitBounds.Visible = false;
				}

				return bounds;
			}

			return base.GetAxisAlignedBoundingBox(matrix);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Children))
			{
				base.OnInvalidate(invalidateType);
			}

			base.OnInvalidate(invalidateType);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					AdjustChildSize(null, null);

					UpdateBoundsItem();
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
			return Task.CompletedTask;
		}

		private void AdjustChildSize(object sender, EventArgs e)
		{
			if (Children.Count > 0)
			{
				var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
				ItemWithTransform.Matrix = Matrix4X4.Identity;
				var scale = Vector3.One;
				if (StretchX)
				{
					scale.X = SizeX / aabb.XSize;
				}
				if (StretchY)
				{
					scale.Y = SizeY / aabb.YSize;
				}
				if (StretchZ)
				{
					scale.Z = SizeZ / aabb.ZSize;
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

				if (aabb.XSize > 0 && aabb.YSize > 0 && aabb.ZSize > 0)
				{
					ItemWithTransform.Matrix = Object3DExtensions.ApplyAtPosition(ItemWithTransform.Matrix, aabb.Center, Matrix4X4.CreateScale(scale));
				}
			}
		}

		private void UpdateBoundsItem()
		{
			if (Children.Count == 2)
			{
				var transformAabb = ItemWithTransform.GetAxisAlignedBoundingBox();
				var fitAabb = FitBounds.GetAxisAlignedBoundingBox();
				var fitSize = fitAabb.Size;
				if (boundsSize.X != 0 && boundsSize.Y != 0 && boundsSize.Z != 0
					&& (fitSize != boundsSize
					|| fitAabb.Center != transformAabb.Center))
				{
					FitBounds.Matrix *= Matrix4X4.CreateScale(
						boundsSize.X / fitSize.X,
						boundsSize.Y / fitSize.Y,
						boundsSize.Z / fitSize.Z);
					FitBounds.Matrix *= Matrix4X4.CreateTranslation(
						transformAabb.Center - FitBounds.GetAxisAlignedBoundingBox().Center);
				}
			}
		}
	}
}