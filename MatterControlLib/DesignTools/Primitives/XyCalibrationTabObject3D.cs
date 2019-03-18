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

using System.ComponentModel;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class XyCalibrationTabObject3D : Object3D
	{
		public XyCalibrationTabObject3D()
		{
			Name = "Calibration Tab".Localize();
		}

		public enum Layout { Horizontal, Vertical }
		public Layout Direction { get; set; } = Layout.Horizontal;

		[DisplayName("Material")]
		public int CalibrationMaterialIndex { get; set; } = 1;
		public double ChangeHeight { get; set; } = .4;
		public double Offset { get; set; } = .5;
		public double NozzleWidth = .4;

		public static async Task<XyCalibrationTabObject3D> Create(int calibrationMaterialIndex = 1, 
			double changeHeight = .4, double offset = .5, double nozzleWidth = .4)
		{
			var item = new XyCalibrationTabObject3D()
			{
				CalibrationMaterialIndex = calibrationMaterialIndex,
				ChangeHeight = changeHeight,
				Offset = offset,
				NozzleWidth = nozzleWidth
			};

			await item.Rebuild();
			return item;
		}

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

			using (RebuildLock())
			{
				using (new CenterAndHeightMantainer(this))
				{
					this.Children.Modify((list) =>
					{
						list.Clear();
					});

					var content = new Object3D();

					var scale = 3.0;
					var width = NozzleWidth * scale * 3;
					var depth = NozzleWidth * scale * 5;
					var spaceBetween = NozzleWidth * scale;

					var shape = new VertexStorage();
					shape.MoveTo(0, 0);
					// left + spaces + blocks + right
					var baseWidth = (2 * spaceBetween) + (4 * spaceBetween) + (5 * width) + (2 * spaceBetween);
					shape.LineTo(baseWidth, 0);
					if (Direction == Layout.Vertical)
					{
						var origin = new Vector2(baseWidth, depth / 2);
						var delta = new Vector2(0, -depth / 2);
						var count = 15;
						for (int i = 0; i < count; i++)
						{
							delta.Rotate(MathHelper.Tau / 2 / count);
							shape.LineTo(origin + delta);
						}
					}
					else
					{
						shape.LineTo(baseWidth+depth, depth / 2); // a point on the left
					}
					shape.LineTo(baseWidth, depth);
					shape.LineTo(0, depth);

					content.Children.Add(new Object3D()
					{
						Mesh = shape.Extrude(ChangeHeight),
						Color = Color.LightBlue
					});

					var position = new Vector2(width / 2 + 2 * spaceBetween, depth / 2 - Offset * 2);
					var step = new Vector2(spaceBetween + width, Offset);
					for (int i=0; i<5; i++)
					{
						var cube = PlatonicSolids.CreateCube();
						content.Children.Add(new Object3D()
						{
							Mesh = cube,
							Color = Color.Yellow,
							Matrix = Matrix4X4.CreateScale(width, depth, ChangeHeight)
								// translate by 1.5 as it is a centered cube (.5) plus the base (1) = 1.5
								* Matrix4X4.CreateTranslation(position.X, position.Y, ChangeHeight * 1.5),
							MaterialIndex = CalibrationMaterialIndex
						});
						position += step;
					}

					if (Direction == Layout.Vertical)
					{
						content.Matrix = Matrix4X4.CreateRotationZ(MathHelper.Tau / 4);
					}

					this.Children.Add(content);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}
	}

	public class XyCalibrationFaceObject3D : Object3D
	{
		public XyCalibrationFaceObject3D()
		{
			Name = "Calibration Faces".Localize();
		}

		public enum Layout { Horizontal, Vertical }
		public Layout Direction { get; set; } = Layout.Horizontal;

		[DisplayName("Material")]
		public int CalibrationMaterialIndex { get; set; } = 1;
		public double ChangingHeight { get; set; } = .4;
		public double BaseHeight { get; set; } = .4;
		public double Offset { get; set; } = .5;
		public double NozzleWidth = .4;

		public static async Task<XyCalibrationFaceObject3D> Create(int calibrationMaterialIndex = 1,
			double baseHeight = 1, double changingHeight = .2, double offset = .5, double nozzleWidth = .4)
		{
			var item = new XyCalibrationFaceObject3D()
			{
				CalibrationMaterialIndex = calibrationMaterialIndex,
				BaseHeight = baseHeight,
				ChangingHeight = changingHeight,
				Offset = offset,
				NozzleWidth = nozzleWidth
			};

			await item.Rebuild();
			return item;
		}

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

			using (RebuildLock())
			{
				using (new CenterAndHeightMantainer(this))
				{
					this.Children.Modify((list) =>
					{
						list.Clear();
					});

					var content = new Object3D();

					var scale = 3.0;
					var width = NozzleWidth * scale * 3;
					var depth = NozzleWidth * scale * 5;
					var spaceBetween = NozzleWidth * scale;

					var shape = new VertexStorage();
					shape.MoveTo(0, 0);
					// left + spaces + blocks + right
					var baseWidth = (2 * spaceBetween) + (4 * spaceBetween) + (5 * width) + (2 * spaceBetween);
					shape.LineTo(baseWidth, 0);
					if (Direction == Layout.Vertical)
					{
						var origin = new Vector2(baseWidth, depth / 2);
						var delta = new Vector2(0, -depth / 2);
						var count = 15;
						for (int i = 0; i < count; i++)
						{
							delta.Rotate(MathHelper.Tau / 2 / count);
							shape.LineTo(origin + delta);
						}
					}
					else
					{
						shape.LineTo(baseWidth + depth, depth / 2); // a point on the left
					}
					shape.LineTo(baseWidth, depth);
					shape.LineTo(0, depth);

					content.Children.Add(new Object3D()
					{
						Mesh = shape.Extrude(BaseHeight),
						Color = Color.LightBlue
					});

					var position = new Vector2(width / 2 + 2 * spaceBetween, depth / 2 - Offset * 2);
					var step = new Vector2(spaceBetween + width, Offset);
					for (int i = 0; i < 5; i++)
					{
						for (int j = 0; j < 10; j++)
						{
							var calibrationMaterial = (j % 2 == 0);
							var cube = PlatonicSolids.CreateCube();
							var yOffset = calibrationMaterial ? position.Y : depth / 2;
							var offset = Matrix4X4.CreateTranslation(position.X, yOffset, BaseHeight + .5 * ChangingHeight + j * ChangingHeight);
							content.Children.Add(new Object3D()
							{
								Mesh = cube,
								Color = Color.Yellow,
								Matrix = Matrix4X4.CreateScale(width, depth, ChangingHeight) * offset,
								MaterialIndex = calibrationMaterial ? CalibrationMaterialIndex : 0
							});
						}
						position += step;
					}

					if (Direction == Layout.Vertical)
					{
						content.Matrix = Matrix4X4.CreateRotationZ(MathHelper.Tau / 4);
					}

					this.Children.Add(content);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}
	}
}