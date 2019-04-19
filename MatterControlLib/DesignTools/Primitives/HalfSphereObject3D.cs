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
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class HalfSphereObject3D : Object3D
	{
		public HalfSphereObject3D()
		{
			Name = "Half Sphere".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["HalfSphere"];
		}

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

		public double Diameter { get; set; } = 20;
		public int LongitudeSides { get; set; } = 40;
		public int LatitudeSides { get; set; } = 10;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;
			using (RebuildLock())
			{
				LatitudeSides = agg_basics.Clamp(LatitudeSides, 3, 180, ref valuesChanged);
				LongitudeSides = agg_basics.Clamp(LongitudeSides, 3, 360, ref valuesChanged);

				using (new CenterAndHeightMaintainer(this))
				{
					var radius = Diameter / 2;
					var angleDelta = MathHelper.Tau / 4 / LatitudeSides;
					var angle = 0.0;
					var path = new VertexStorage();
					path.MoveTo(0, 0);
					path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
					for (int i = 0; i < LatitudeSides; i++)
					{
						angle += angleDelta;
						path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
					}

					Mesh = VertexSourceToMesh.Revolve(path, LongitudeSides);
				}
			}

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}
	}
}