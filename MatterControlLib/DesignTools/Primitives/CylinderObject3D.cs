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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CylinderObject3D : Object3D, IPropertyGridModifier
	{
		public CylinderObject3D()
		{
			Name = "Cylinder".Localize();
			Color = Operations.Object3DExtensions.PrimitiveColors["Cylinder"];
		}

		public CylinderObject3D(double diameter, double height, int sides)
			: this()
		{
			Diameter = diameter;
			Height = height;
			Sides = sides;

			Rebuild();
		}

		public static async Task<CylinderObject3D> Create(double diameter, double height, int sides, Alignment alignment = Alignment.Z)
		{
			if (alignment == Alignment.Z)
			{
				return new CylinderObject3D(diameter, height, sides);
			}

			return await Create(diameter, diameter, height, sides, alignment);
		}

		public static async Task<CylinderObject3D> Create(double diameterBottom, double diameterTop, double height, int sides, Alignment alignment = Alignment.Z)
		{
			var item = new CylinderObject3D()
			{
				Advanced = true,
				Diameter = diameterBottom,
				DiameterTop = diameterTop,
				Height = height,
				Sides = sides,
			};

			await item.Rebuild();
			switch (alignment)
			{
				case Alignment.X:
					item.Matrix = Matrix4X4.CreateRotationY(MathHelper.Tau / 4);
					break;
				case Alignment.Y:
					item.Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 4);
					break;
				case Alignment.Z:
					// This is the natural case (how it was modeled)
					break;
				case Alignment.negX:
					item.Matrix = Matrix4X4.CreateRotationY(-MathHelper.Tau / 4);
					break;
				case Alignment.negY:
					item.Matrix = Matrix4X4.CreateRotationX(-MathHelper.Tau / 4);
					break;
				case Alignment.negZ:
					item.Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
					break;
			}

			return item;
		}

		public static async Task<CylinderObject3D> Create()
		{
			var item = new CylinderObject3D();

			await item.Rebuild();
			return item;
		}

		public double Diameter { get; set; } = 20;

		public double Height { get; set; } = 20;

		public int Sides { get; set; } = 40;

		public bool Advanced { get; set; } = false;

		public double StartingAngle { get; set; } = 0;

		public double EndingAngle { get; set; } = 360;

		public double DiameterTop { get; set; } = 20;

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
				Sides = agg_basics.Clamp(Sides, 3, 360, ref valuesChanged);
				Height = Math.Max(Height, .001);
				Diameter = Math.Max(Diameter, .1);

				using (new CenterAndHeightMaintainer(this))
				{
					if (!Advanced)
					{
						var path = new VertexStorage();
						path.MoveTo(0, -Height / 2);
						path.LineTo(Diameter / 2, -Height / 2);
						path.LineTo(Diameter / 2, Height / 2);
						path.LineTo(0, Height / 2);

						Mesh = VertexSourceToMesh.Revolve(path, Sides);
					}
					else
					{
						var path = new VertexStorage();
						path.MoveTo(0, -Height / 2);
						path.LineTo(Diameter / 2, -Height / 2);
						path.LineTo(DiameterTop / 2, Height / 2);
						path.LineTo(0, Height / 2);

						Mesh = VertexSourceToMesh.Revolve(path, Sides, MathHelper.DegreesToRadians(StartingAngle), MathHelper.DegreesToRadians(EndingAngle));
					}
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
			if (change.Context.GetEditRow(nameof(DiameterTop)) is GuiWidget diameterWidget)
			{
				diameterWidget.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(StartingAngle)) is GuiWidget startingAngleWidget)
			{
				startingAngleWidget.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(EndingAngle)) is GuiWidget endingAngleWidget)
			{
				endingAngleWidget.Visible = Advanced;
			}
		}
	}
}