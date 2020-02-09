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
		public double NozzleWidth { get; set; } = .4;

		public XyCalibrationTabObject3D()
		{
			Name = "Calibration Tab".Localize();
		}

		[DisplayName("Material")]
		public int CalibrationMaterialIndex { get; set; } = 1;

		public override bool CanFlatten => true;

		public double ChangeHeight { get; set; } = .4;

		public double Offset { get; set; } = .5;

		public double WipeTowerSize { get; set; } = 10;

		private double TabDepth => NozzleWidth * tabScale * 5;

		private double tabScale = 3;

		private double TabWidth => NozzleWidth * tabScale * 3;

		public static async Task<XyCalibrationTabObject3D> Create(int calibrationMaterialIndex = 1,
			double changeHeight = .4,
			double offset = .5,
			double nozzleWidth = .4,
			double wipeTowerSize = 10)
		{
			var item = new XyCalibrationTabObject3D()
			{
				CalibrationMaterialIndex = calibrationMaterialIndex,
				ChangeHeight = changeHeight,
				Offset = offset,
				NozzleWidth = nozzleWidth,
				WipeTowerSize = wipeTowerSize
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

			tabScale = 3;

			// by default we don't want tab with to be greater than 10 mm
			if (TabWidth > 10)
			{
				tabScale = 1;
			}
			else if (TabWidth > 5)
			{
				tabScale = 2;
			}

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					this.Children.Modify((list) =>
					{
						list.Clear();
					});

					var calibrateX = GetTab(true);
					this.Children.Add(calibrateX);
					var calibrateY = GetTab(false);
					this.Children.Add(calibrateY);
					// add in the corner connecter
					this.Children.Add(new Object3D()
					{
						Mesh = PlatonicSolids.CreateCube(),
						Matrix = Matrix4X4.CreateTranslation(-1 / 2.0, 1 / 2.0, 1 / 2.0) * Matrix4X4.CreateScale(TabDepth, TabDepth, ChangeHeight),
						Color = Color.LightBlue
					});

					if (WipeTowerSize > 0)
					{
						// add in the wipe tower
						this.Children.Add(new Object3D()
						{
							Mesh = PlatonicSolids.CreateCube(),
							Matrix = Matrix4X4.CreateTranslation(1 / 2.0, 1 / 2.0, 1 / 2.0)
								* Matrix4X4.CreateScale(WipeTowerSize, WipeTowerSize, ChangeHeight * 2)
								* Matrix4X4.CreateTranslation(TabDepth * 1, TabDepth * 2, 0),
							OutputType = PrintOutputTypes.WipeTower
						});
					}
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		private Object3D GetTab(bool calibrateX)
		{
			var content = new Object3D();

			var spaceBetween = NozzleWidth * tabScale;

			var shape = new VertexStorage();
			shape.MoveTo(0, 0);
			// left + spaces + blocks + right
			var sampleCount = 7;
			var baseWidth = (2 * spaceBetween) + ((sampleCount - 1) * spaceBetween) + (sampleCount * TabWidth) + (2 * spaceBetween);
			shape.LineTo(baseWidth, 0);
			if (calibrateX)
			{
				var origin = new Vector2(baseWidth, TabDepth / 2);
				var delta = new Vector2(0, -TabDepth / 2);
				var count = 15;
				for (int i = 0; i < count; i++)
				{
					delta.Rotate(MathHelper.Tau / 2 / count);
					shape.LineTo(origin + delta);
				}
			}
			else
			{
				shape.LineTo(baseWidth + TabDepth, TabDepth / 2); // a point on the left
			}
			shape.LineTo(baseWidth, TabDepth);
			shape.LineTo(0, TabDepth);

			content.Children.Add(new Object3D()
			{
				Mesh = shape.Extrude(ChangeHeight),
				Color = Color.LightBlue
			});

			var position = new Vector2(TabWidth / 2 + 2 * spaceBetween, TabDepth / 2 - Offset * ((sampleCount - 1) / 2));
			var step = new Vector2(spaceBetween + TabWidth, Offset);
			for (int i = 0; i < sampleCount; i++)
			{
				var cube = PlatonicSolids.CreateCube();
				content.Children.Add(new Object3D()
				{
					Mesh = cube,
					Color = Color.Yellow,
					Matrix = Matrix4X4.CreateScale(TabWidth, TabDepth, ChangeHeight)
						// translate by 1.5 as it is a centered cube (.5) plus the base (1) = 1.5
						* Matrix4X4.CreateTranslation(position.X, position.Y, ChangeHeight * 1.5),
					MaterialIndex = CalibrationMaterialIndex
				});
				position += step;
			}

			if (calibrateX)
			{
				content.Matrix = Matrix4X4.CreateRotationZ(MathHelper.Tau / 4) * Matrix4X4.CreateTranslation(0, TabDepth, 0);
			}

			return content;
		}
	}
}