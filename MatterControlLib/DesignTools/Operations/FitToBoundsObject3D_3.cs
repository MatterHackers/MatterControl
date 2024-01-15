﻿/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    [Obsolete("Not used anymore. Replaced with FitToBoundsObject3D_4", true)]
    public class FitToBoundsObject3D_3 : TransformWrapperObject3D, IEditorDraw, IPropertyGridModifier
    {
		private InvalidateType additonalInvalidate;

		public FitToBoundsObject3D_3()
		{
			Name = "Fit to Bounds".Localize();
		}

		public enum StretchOption
		{
			[Description("Do not change this axis")]
			None,
			[Description("Do not grow, only keep in bounds")]
			Shrink,
			[Description("Grow to fit bounds")]
			Expand
		}

        private IObject3D FitBounds => Children.Last();

        public bool Advanced { get; set; } = false;
        
		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		[Description("Ensure that the part maintains its proportions.")]
		public LockProportions LockProportion { get; set; } = LockProportions.X_Y_Z;

		[MaxDecimalPlaces(3)]
		public DoubleOrExpression Width { get; set; } = 0;

		[MaxDecimalPlaces(3)]
		public DoubleOrExpression Depth { get; set; } = 0;

		[MaxDecimalPlaces(3)]
		public DoubleOrExpression Height { get; set; } = 0;

		[Description("Allows you turn on and off applying the fit to the x axis.")]
		public bool StretchX { get; set; } = true;

		[Description("Allows you turn on and off applying the fit to the y axis.")]
		public bool StretchY { get; set; } = true;

		[Description("Allows you turn on and off applying the fit to the z axis.")]
		public bool StretchZ { get; set; } = true;

        [EnumDisplay(IconPaths = new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png", "align_origin.png" }, InvertIcons = true)]
        public Align XAlign { get; set; } = Align.None;

        [EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public StretchOption XStretchOption { get; set; } = StretchOption.Expand;

        [EnumDisplay(IconPaths = new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png", "align_origin.png" }, InvertIcons = true)]
        public Align YAlign { get; set; } = Align.None;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public StretchOption YStretchOption { get; set; } = StretchOption.Expand;

        [EnumDisplay(IconPaths = new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_top.png", "align_origin.png" }, InvertIcons = true)]
        public Align ZAlign { get; set; } = Align.None;

        [EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public StretchOption ZStretchOption { get; set; } = StretchOption.Expand;
        
		public static async Task<FitToBoundsObject3D_3> Create(IObject3D itemToFit)
		{
			var fitToBounds = new FitToBoundsObject3D_3();
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

				fitToBounds.Width = startingAabb.XSize;
				fitToBounds.Depth = startingAabb.YSize;
				fitToBounds.Height = startingAabb.ZSize;
				await fitToBounds.Rebuild();

				var finalAabb = fitToBounds.GetAxisAlignedBoundingBox();
				fitToBounds.Translate(startingAabb.Center - finalAabb.Center);
			}

			return fitToBounds;
		}

		AxisAlignedBoundingBox CalcBounds()
		{
			var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
			var center = aabb.Center;

			var width = Width.Value(this);
			var depth = Depth.Value(this);
			var height = Height.Value(this);

			var minXyz = center - new Vector3(width / 2, depth / 2, height / 2);
			var maxXyz = center + new Vector3(width / 2, depth / 2, height / 2);
			return new AxisAlignedBoundingBox(minXyz, maxXyz);
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			layer.World.RenderAabb(this.CalcBounds(), this.WorldMatrix(), Color.Red, 1, 1);
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return WorldViewExtensions.GetWorldspaceAabbOfRenderAabb(this.CalcBounds(), this.WorldMatrix(), 1, 1);
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

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			additonalInvalidate = invalidateArgs.InvalidateType;

			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children))
			{
				base.OnInvalidate(invalidateArgs);
			}

			base.OnInvalidate(invalidateArgs);

			additonalInvalidate = InvalidateType.None;
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

			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix | additonalInvalidate));
			return Task.CompletedTask;
		}

		private void AdjustChildSize(object sender, EventArgs e)
		{
			if (Children.Count > 0)
			{
				var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
				ItemWithTransform.Matrix = Matrix4X4.Identity;
				var constraint = new Vector3(Width.Value(this), Depth.Value(this), Height.Value(this));

				var scale = GetScale(constraint, aabb.Size);

				switch (LockProportion)
				{
					case LockProportions.None:
						break;

					case LockProportions.X_Y:
						var minXy = Math.Min(scale.X, scale.Y);
						scale.X = minXy;
						scale.Y = minXy;
						break;

					case LockProportions.X_Y_Z:
						var minXyz = Math.Min(Math.Min(scale.X, scale.Y), scale.Z);
						scale.X = minXyz;
						scale.Y = minXyz;
						scale.Z = minXyz;
						break;
				}

				if (aabb.XSize > 0 && aabb.YSize > 0 && aabb.ZSize > 0)
				{
					var scalingMatrix = Matrix4X4.CreateScale(scale);

                    if (Advanced)
					{
						var scaledBounds = aabb.Size * scale;
					}
					else
					{
						ItemWithTransform.Matrix = Object3DExtensions.ApplyAtPosition(ItemWithTransform.Matrix, aabb.Center, scalingMatrix);
					}
				}
			}
		}

        private Vector3 GetScale(Vector3 constraint, Vector3 size)
        {
            var scale = Vector3.One;

			bool[] stretchs = { StretchX, StretchY, StretchZ };
            StretchOption[] stretchOptions = { XStretchOption, YStretchOption, ZStretchOption };

			for (var i = 0; i < 3; i++)
			{
				if (Advanced)
				{
					switch (stretchOptions[i])
					{
                        case StretchOption.None:
							scale[i] = 1;
                            break;

                        case StretchOption.Shrink:
							scale[i] = Math.Min(constraint[i] / size[i], 1);
                            break;

                        case StretchOption.Expand:
                            scale[i] = constraint[i] / size[i];
                            break;
                    }
				}
				else
				{
					if (stretchs[i])
					{
						scale[i] = constraint[i] / size[i];
					}
				}
			}

			return scale;
        }

        public void UpdateControls(PublicPropertyChange change)
        {
			change.SetRowVisible(nameof(StretchX), () => !Advanced);
			change.SetRowVisible(nameof(StretchY), () => !Advanced);
			change.SetRowVisible(nameof(StretchZ), () => !Advanced);

            change.SetRowVisible(nameof(XAlign), () => Advanced);
            change.SetRowVisible(nameof(XStretchOption), () => Advanced);

			change.SetRowVisible(nameof(YAlign), () => Advanced);
            change.SetRowVisible(nameof(YStretchOption), () => Advanced);

            change.SetRowVisible(nameof(ZAlign), () => Advanced);
            change.SetRowVisible(nameof(ZStretchOption), () => Advanced);
        }

        private void UpdateBoundsItem()
		{
			if (Children.Count == 2)
			{
				var transformAabb = ItemWithTransform.GetAxisAlignedBoundingBox();
				var fitAabb = FitBounds.GetAxisAlignedBoundingBox();
				var fitSize = fitAabb.Size;
				var width = Width.Value(this);
				var depth = Depth.Value(this);
				var height = Height.Value(this);
				var boundsSize = new Vector3(width, depth, height);
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