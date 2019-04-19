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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public enum FitType { Box, Cylinder }

	public enum MaintainRatio { None, X_Y, X_Y_Z }

	[Obsolete("Not used anymore. Replaced with FitToBoundsObject3D_2", true)]
	public class FitToBoundsObject3D : Object3D, IEditorDraw, IPropertyGridModifier
	{
		[Description("Set the shape the part will be fit into.")]
		public FitType FitType { get; set; } = FitType.Box;

		public double Width { get; set; }
		public double Depth { get; set; }
		public double Diameter { get; set; }
		public double Height { get; set; } 

		[Description("Set the rules for how to maintain the part while scaling.")]
		public MaintainRatio MaintainRatio { get; set; } = MaintainRatio.X_Y;
		[Description("Allows you turn on and off applying the fit to the x axis.")]
		public bool StretchX { get; set; } = true;
		[Description("Allows you turn on and off applying the fit to the y axis.")]
		public bool StretchY { get; set; } = true;
		[Description("Allows you turn on and off applying the fit to the z axis.")]
		public bool StretchZ { get; set; } = true;

		IObject3D ScaleItem => Children.First();
		[JsonIgnore]
		public IObject3D ItemToScale => Children.First().Children.First();

		public FitToBoundsObject3D()
		{
			Name = "Fit to Bounds".Localize();
		}

		public override void Flatten(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// push our matrix into our children
				foreach (var child in this.Children)
				{
					child.Matrix *= this.Matrix;
				}

				// push child into children
				ItemToScale.Matrix *= ScaleItem.Matrix;

				// add our children to our parent and remove from parent
				this.Parent.Children.Modify(list =>
				{
					list.Remove(this);
					list.AddRange(ScaleItem.Children);
				});
			}
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// push our matrix into inner children
				foreach (var child in ScaleItem.Children)
				{
					child.Matrix *= this.Matrix;
				}

				// add inner children to our parent and remove from parent
				this.Parent.Children.Modify(list =>
				{
					list.Remove(this);
					list.AddRange(ScaleItem.Children);
				});
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public static async Task<FitToBoundsObject3D> Create(IObject3D itemToFit)
		{
			FitToBoundsObject3D fitToBounds = new FitToBoundsObject3D();
			var aabb = itemToFit.GetAxisAlignedBoundingBox();

			fitToBounds.Width = aabb.XSize;
			fitToBounds.Depth = aabb.YSize;
			fitToBounds.Height = aabb.ZSize;

			fitToBounds.Diameter = aabb.XSize;

			var scaleItem = new Object3D();
			fitToBounds.Children.Add(scaleItem);
			scaleItem.Children.Add(itemToFit);

			await fitToBounds.Rebuild();

			return fitToBounds;
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					AdjustChildSize(null, null);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
			return Task.CompletedTask;
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
			var aabb = ItemToScale.GetAxisAlignedBoundingBox();
			ScaleItem.Matrix = Matrix4X4.Identity;
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

			ScaleItem.Matrix = Object3DExtensions.ApplyAtPosition(ScaleItem.Matrix, aabb.Center, Matrix4X4.CreateScale(scale));
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				var aabb = ItemToScale.GetAxisAlignedBoundingBox();

				if (FitType == FitType.Box)
				{
					var center = aabb.Center;
					var worldMatrix = this.WorldMatrix();

					var minXyz = center - new Vector3(Width / 2, Depth / 2, Height / 2);
					var maxXyz = center + new Vector3(Width / 2, Depth / 2, Height / 2);
					var bounds = new AxisAlignedBoundingBox(minXyz, maxXyz);
					//var leftW = Vector3Ex.Transform(, worldMatrix);
					var right = Vector3Ex.Transform(center + new Vector3(Width / 2, 0, 0), worldMatrix);
					// layer.World.Render3DLine(left, right, Agg.Color.Red);
					layer.World.RenderAabb(bounds, worldMatrix, Agg.Color.Red, 1, 1);
				}
				else
				{
					layer.World.RenderCylinderOutline(this.WorldMatrix(), aabb.Center, Diameter, Height, 30, Color.Red, 1, 1);
				}
				// turn the lighting back on
				GL.Enable(EnableCap.Lighting);
			}
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(Diameter), () => FitType != FitType.Box);
			change.SetRowVisible(nameof(Width), () => FitType != FitType.Box);
			change.SetRowVisible(nameof(Depth), () => FitType != FitType.Box);
			change.SetRowVisible(nameof(MaintainRatio), () => FitType != FitType.Box);
			change.SetRowVisible(nameof(StretchX), () => FitType != FitType.Box);
			change.SetRowVisible(nameof(StretchY), () => FitType != FitType.Box);
		}
	}
}