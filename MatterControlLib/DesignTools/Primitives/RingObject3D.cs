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
	public class RingObject3D : Object3D, IPropertyGridModifier
	{
		public RingObject3D()
		{
			Name = "Ring".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Ring"];
		}

		public RingObject3D(double outerDiameter, double innerDiameter, double height, int sides)
			: this()
		{
			this.OuterDiameter = outerDiameter;
			this.InnerDiameter = innerDiameter;
			this.Height = height;
			this.Sides = sides;

			Rebuild();
		}

		public static async Task<RingObject3D> Create()
		{
			var item = new RingObject3D();

			await item.Rebuild();
			return item;
		}

		public double OuterDiameter { get; set; } = 20;
		public double InnerDiameter { get; set; } = 15;
		public double Height { get; set; } = 5;
		public int Sides { get; set; } = 40;

		public bool Advanced { get; set; } = false;
		public double StartingAngle { get; set; } = 0;
		public double EndingAngle { get; set; } = 360;

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
				InnerDiameter = agg_basics.Clamp(InnerDiameter, 0, OuterDiameter - .1, ref valuesChanged);
				Sides = agg_basics.Clamp(Sides, 3, 360, ref valuesChanged);

				using (new CenterAndHeightMaintainer(this))
				{
					var startingAngle = StartingAngle;
					var endingAngle = EndingAngle;
					if (!Advanced)
					{
						startingAngle = 0;
						endingAngle = 360;
					}

					var innerDiameter = Math.Min(OuterDiameter - .1, InnerDiameter);

					var path = new VertexStorage();
					path.MoveTo(OuterDiameter / 2, -Height / 2);
					path.LineTo(OuterDiameter / 2, Height / 2);
					path.LineTo(innerDiameter / 2, Height / 2);
					path.LineTo(innerDiameter / 2, -Height / 2);
					path.LineTo(OuterDiameter / 2, -Height / 2);

					var startAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(startingAngle));
					var endAngle = MathHelper.Range0ToTau(MathHelper.DegreesToRadians(endingAngle));
					Mesh = VertexSourceToMesh.Revolve(path, Sides, startAngle, endAngle);
				}
			}

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(StartingAngle), () => Advanced);
			change.SetRowVisible(nameof(EndingAngle), () => Advanced);
		}
	}
}