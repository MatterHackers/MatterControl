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

using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.DesignTools
{
	public class ConeObject3D : Object3D
	{
		public ConeObject3D()
		{
			Name = "Cone".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cone"];
		}

		public static async Task<ConeObject3D> Create()
		{
			var item = new ConeObject3D();
			await item.Rebuild();

			return item;
		}

		public double Diameter { get; set; } = 20;
		//[DisplayName("Top")]
		//public double TopDiameter { get; set; } = 0;
		public double Height { get; set; } = 20;
		public int Sides { get; set; } = 40;

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
			bool changed = false;

			using (RebuildLock())
			{
				Sides = agg_basics.Clamp(Sides, 3, 360, ref changed);
				using (new CenterAndHeightMaintainer(this))
				{

					var path = new VertexStorage();
					path.MoveTo(0, 0);
					path.LineTo(Diameter / 2, 0);
					path.LineTo(0, Height);

					Mesh = VertexSourceToMesh.Revolve(path, Sides);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
			return Task.CompletedTask;
		}
	}
}