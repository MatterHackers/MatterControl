/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh.Solids;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CubeObject3D : PrimitiveObject3D, IPropertyGridModifier, IObject3DControlsProvider
	{
		public CubeObject3D()
		{
			Name = "Cube".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cube"];
		}

		public static double MinEdgeSize = .001;

		public override string ThumbnailName => "Cube";

		/// <summary>
		/// This is the actual serialized with that can use expressions
		/// </summary>
		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Width { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Depth { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Height { get; set; } = 20;

		public bool Round { get; set; }

		[Slider(0, 30, Easing.EaseType.Quadratic, snapDistance: .1)]
		public DoubleOrExpression Radius { get; set; } = 3;

		[Slider(1, 20, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression RoundSegments { get; set; } = 9;

		public static async Task<CubeObject3D> Create()
		{
			var item = new CubeObject3D();
			await item.Rebuild();
			return item;
		}

		public static async Task<CubeObject3D> Create(double x, double y, double z)
		{
			var item = new CubeObject3D()
			{
				Width = x,
				Depth = y,
				Height = z,
			};

			await item.Rebuild();
			return item;
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddHeightControl(this, Width, Depth, Height);
			object3DControlsLayer.AddWidthDepthControls(this, Width, Depth, Height);

			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

			var width = Width.ClampIfNotCalculated(this, MinEdgeSize, 1000000, ref valuesChanged);
			var depth = Depth.ClampIfNotCalculated(this, MinEdgeSize, 1000000, ref valuesChanged);
			var height = Height.ClampIfNotCalculated(this, MinEdgeSize, 1000000, ref valuesChanged);
			var roundSegments = RoundSegments.ClampIfNotCalculated(this, 1, 90, ref valuesChanged);
			var roundRadius = Radius.ClampIfNotCalculated(this, 0, Math.Min(width, Math.Min(depth, height)) / 2, ref valuesChanged);

			Invalidate(InvalidateType.DisplayValues);
			
			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					if (Round)
					{
						Mesh = RoundedCornerBox.Create(roundSegments, new Vector3(width, depth, height), roundRadius);
					}
					else
					{
						Mesh = PlatonicSolids.CreateCube(Width.Value(this), Depth.Value(this), Height.Value(this));
					}
				}
			}

			// if any of our parest are re-bulding cancel it as we just changed and they need to start over
			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));

			return base.Rebuild();
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(RoundSegments), () => Round);
			change.SetRowVisible(nameof(Radius), () => Round);
		}
	}

    public class CubeHoleObject3D : CubeObject3D
    {
        public override string ThumbnailName => "CubeHole";

        public CubeHoleObject3D()
        {
			OutputType = PrintOutputTypes.Hole;
        }

		public static async Task<CubeHoleObject3D> Create()
		{
			var item = new CubeHoleObject3D();
			await item.Rebuild();
			return item;
		}

	}
}