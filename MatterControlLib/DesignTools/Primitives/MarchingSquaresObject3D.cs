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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class MarchingSquaresObject3D : Object3D
	{
		public MarchingSquaresObject3D()
		{
			Name = "MarchingSquares".Localize();
			Color = Agg.Color.Cyan;
		}

		public static async Task<MarchingSquaresObject3D> Create()
		{
			var item = new MarchingSquaresObject3D();
			await item.Rebuild();
			return item;
		}

		public int Iterations { get; set; } = 5;

		public double Size { get; set; } = 15;

		public double Threshold { get; set; } = .001;

		public enum Shapes
		{
			Box,
			Sphere
		}

		public Shapes Shape { get; set; } = Shapes.Box;


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

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
#if true
					ISdf shape = new Sphere()
					{
						Radius = Size
					};

					if (Shape == Shapes.Box)
					{
						shape = new Box()
						{
							Size = new Vector3(Size, Size * .8, Size * .6)
						};
					}

					var bounds = shape.Bounds;
					var root = Octree.BuildOctree(shape.Sdf, bounds.MinXYZ, bounds.Size, Iterations, Threshold);

					Mesh = Octree.GenerateMeshFromOctree(root);
#else
					var c = new MarchingCubes();
					c.Generate();
					MeshNormals.QuickCompute(c.Mesh); // generate normals
					Mesh = c.Mesh.ToMesh();
#endif
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}
	}
}