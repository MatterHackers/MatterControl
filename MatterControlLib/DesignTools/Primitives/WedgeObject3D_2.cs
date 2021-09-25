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
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public enum RoundTypes
	{
		None,
		Up,
		Down,
	}

	public class WedgeObject3D_2 : PrimitiveObject3D, IPropertyGridModifier, IObject3DControlsProvider
	{
		public WedgeObject3D_2()
		{
			Name = "Wedge".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Wedge"];
		}

		public override string ThumbnailName => "Wedge";

		public static async Task<WedgeObject3D_2> Create()
		{
			var item = new WedgeObject3D_2();

			await item.Rebuild();
			return item;
		}

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression Width { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, Easing.EaseType.Quadratic, snapDistance: 1)]
		public DoubleOrExpression Depth { get; set; } = 20;

		[MaxDecimalPlaces(2)]
		[Slider(1, 400, VectorMath.Easing.EaseType.Quadratic, useSnappingGrid: true)]
		public DoubleOrExpression Height { get; set; } = 20;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public RoundTypes Round { get; set; } = RoundTypes.None;

		[Slider(2, 90, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression RoundSegments { get; set; } = 15;

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

			var roundSegments = RoundSegments.ClampIfNotCalculated(this, 2, 90, ref valuesChanged);

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			using (RebuildLock())
			{
				var height = Height.Value(this);
				var width = Width.Value(this);
				using (new CenterAndHeightMaintainer(this))
				{
					var path = new VertexStorage();
					path.MoveTo(0, 0);
					path.LineTo(width, 0);

					var range = 360 / 4.0;
					switch (Round)
					{
						case RoundTypes.Up:
							for (int i = 1; i < roundSegments - 1; i++)
							{
								var angle = range / (roundSegments - 1) * i;
								var rad = MathHelper.DegreesToRadians(angle);
								path.LineTo(Math.Cos(rad) * width, Math.Sin(rad) * height);
							}
							break;

						case RoundTypes.Down:
							for (int i = 1; i < roundSegments - 1; i++)
							{
								var angle = range / (roundSegments - 1) * i;
								var rad = MathHelper.DegreesToRadians(angle);
								path.LineTo(width - Math.Sin(rad) * width, height - Math.Cos(rad) * height);
							}
							break;
					}

					path.LineTo(0, height);

					Mesh = VertexSourceToMesh.Extrude(path, Depth.Value(this));
					Mesh.Transform(Matrix4X4.CreateRotationX(MathHelper.Tau / 4));
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));

			return Task.CompletedTask;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(RoundSegments), () => Round != RoundTypes.None);
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddHeightControl(this, Width, Depth, Height);
			object3DControlsLayer.AddWidthDepthControls(this, Width, Depth, Height);

			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}
	}
}