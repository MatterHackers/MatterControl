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
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class HalfSphereObject3D : PrimitiveObject3D, IObject3DControlsProvider
	{
		private int lastLongitudeSides;
		private int lastLatitudeSides;
		private double lastDiameter;

		public HalfSphereObject3D()
		{
			Name = "Half Sphere".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["HalfSphere"];
		}

		public override string ThumbnailName => "Half Sphere";
	
		public HalfSphereObject3D(double diametar, int sides)
		{
			this.Diameter = diametar;
			this.LatitudeSides = sides;
			this.LongitudeSides = sides;
			Rebuild();
		}

		public static async Task<HalfSphereObject3D> Create()
		{
			var item = new HalfSphereObject3D();

			await item.Rebuild();
			return item;
		}

		public DoubleOrExpression Diameter { get; set; } = 20;
		public IntOrExpression LongitudeSides { get; set; } = 40;
		public IntOrExpression LatitudeSides { get; set; } = 10;

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
			using (RebuildLock())
			{
				var latitudeSides = LatitudeSides.ClampIfNotCalculated(this, 3, 180, ref valuesChanged);
				var longitudeSides = LongitudeSides.ClampIfNotCalculated(this, 3, 360, ref valuesChanged);
				var diameter = Diameter.Value(this);

				using (new CenterAndHeightMaintainer(this))
				{
					if (longitudeSides != lastLongitudeSides
						|| latitudeSides != lastLatitudeSides
						|| diameter != lastDiameter)
					{
						var radius = diameter / 2;
						var angleDelta = MathHelper.Tau / 4 / latitudeSides;
						var angle = 0.0;
						var path = new VertexStorage();
						path.MoveTo(0, 0);
						path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
						for (int i = 0; i < latitudeSides; i++)
						{
							angle += angleDelta;
							path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
						}

						Mesh = VertexSourceToMesh.Revolve(path, longitudeSides);
					}

					lastDiameter = diameter;
					lastLongitudeSides = longitudeSides;
					lastLatitudeSides = latitudeSides;
				}
			}

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.Object3DControls.Add(new ScaleDiameterControl(object3DControlsLayer,
				null,
				null,
				new List<Func<double>>() { () => Diameter.Value(this) },
				new List<Action<double>>() { (diameter) => Diameter = diameter },
				0));
			object3DControlsLayer.AddControls(ControlTypes.MoveInZ);
			object3DControlsLayer.AddControls(ControlTypes.RotateXYZ);
		}
	}
}