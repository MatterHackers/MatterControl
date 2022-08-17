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

using System.Threading.Tasks;
using DualContouring;
using g3;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    public class DualContouringObject3D : Object3D
	{
		public enum Shapes
		{
			Box,
			[EnumName("2 Boxes")]
			Box_And_Sphere,
			[EnumName("3 Boxes")]
			Boxes_3,
			Sphere,
			Cylinder,
		}

		public DualContouringObject3D()
		{
			Name = "Dual Contouring".Localize();
			Color = Agg.Color.Cyan;
		}

		public static async Task<DualContouringObject3D> Create()
		{
			var item = new DualContouringObject3D();
			await item.Rebuild();
			return item;
		}

		public Shapes Shape { get; set; } = Shapes.Box;

		public OuptutTypes Ouptput { get; set; }

		public int Iterations { get; set; } = 5;

		public double Size { get; set; } = 15;

		public double Threshold { get; set; } = .001;
        
        public enum OuptutTypes
        {
            DualContouring,
            MarchingCubes,
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

			return Task.Run(() =>
			{
				using (RebuildLock())
				{
					using (new CenterAndHeightMaintainer(this))
					{
						ISdf shape = null;
						switch (Shape)
						{
							case Shapes.Box:
								shape = new Box()
								{
									Size = new Vector3(Size, Size, Size)
								};
								break;

							case Shapes.Boxes_3:
								shape = new Union()
								{
									Items = new ISdf[]
									{
										new Transform(new Box()
										{
											Size = new Vector3(Size, Size, Size)
										}, Matrix4X4.CreateRotationZ(MathHelper.Tau * .2) * Matrix4X4.CreateRotationX(MathHelper.Tau * .2)),
										new Box()
										{
											Size = new Vector3(Size, Size, Size)
										},
										new Transform(new Box()
										{
											Size = new Vector3(Size, Size, Size)
										}, Matrix4X4.CreateRotationY(MathHelper.Tau * .2) * Matrix4X4.CreateRotationX(MathHelper.Tau * .3)),
									}
								};
								break;

							case Shapes.Box_And_Sphere:
								shape = new Union()
								{
									Items = new ISdf[]
									{
										new Transform(new Sphere()
										{
											Radius = Size / 2
										}, Matrix4X4.CreateTranslation(-3, -2, 0)),
										new Transform(new Box()
										{
											Size = new Vector3(Size, Size, Size)
										}, Matrix4X4.CreateRotationZ(MathHelper.Tau * .2)),
									}
								};
								break;

							case Shapes.Sphere:
								shape = new Sphere()
								{
									Radius = Size
								};
								break;

							case Shapes.Cylinder:
								shape = new Cylinder()
								{
									Radius = Size,
									Height = Size
								};
								break;
						}

						var bounds = shape.Bounds;
						bounds.Expand(.1);
						if (Iterations > 7)
						{
							Iterations = 7;
						}

						if (Ouptput == OuptutTypes.DualContouring)
						{
							var root = Octree.BuildOctree(shape.Sdf, bounds.MinXYZ, bounds.Size, Iterations, Threshold);

							Mesh = Octree.GenerateMeshFromOctree(root);
						}
						else
						{
							var min = shape.Bounds.MinXYZ;
							var max = shape.Bounds.MaxXYZ;
							var c = new MarchingCubes()
							{
								Implicit = new SdfToImplicit(shape),
								Bounds = new AxisAlignedBox3d(min.X, min.Y, min.Z, max.X, max.Y, max.Z),
							};
							c.Generate();
							MeshNormals.QuickCompute(c.Mesh); // generate normals
							Mesh = c.Mesh.ToMesh();
						}
					}
				}

				Invalidate(InvalidateType.DisplayValues);

				this.CancelAllParentBuilding();
				Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
				return Task.CompletedTask;
			});
		}
        
        public class SdfToImplicit : ImplicitFunction3d
        {
            public ISdf Sdf { get; set; }
            public SdfToImplicit(ISdf sdf)
            {
                Sdf = sdf;
            }
            
            public double Value(ref Vector3d pt)
            {
				return Sdf.Sdf(new Vector3(pt.x, pt.y, pt.z));
			}
		}
	}
}