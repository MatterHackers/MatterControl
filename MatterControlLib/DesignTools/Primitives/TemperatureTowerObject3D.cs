/*
Copyright (c) 2019, Lars Brubaker
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

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TemperatureTowerObject3D : Object3D
	{
		private static Mesh shape = null;

		public override bool CanFlatten => true;

		public TemperatureTowerObject3D()
		{
			Name = "Temperature Tower".Localize();
			Color = Color.White;

			if (shape == null)
			{
				using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "CC - gaaZolee - AS.amf")))
				{
					var amfObject = AmfDocument.Load(measureAmfStream, CancellationToken.None);
					shape = amfObject.Children.First().Mesh;
				}
			}
		}

		public static async Task<TemperatureTowerObject3D> Create(double startingTemp)
		{
			var item = new TemperatureTowerObject3D()
			{
				MaxTemperature = startingTemp
			};

			await item.Rebuild();
			return item;
		}

		private double BaseHeight => 1;

		private double SectionHeight => 10;

		[Description("The maximum temperature to test. This will be the temperature of the first section and it will decrease from here.")]
		public double MaxTemperature { get; set; } = 210;

		[Description("The amount to decrease the temperature for each section of the tower.")]
		public double ChangeAmount { get; set; } = 5;

		[Description("The number of tower section to create.")]
		public int Sections { get; set; } = 5;

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
			// Point Size 10
			// Height 1
			// Font Fredoka

			// Align
			// X: Right Right -11
			// Y: Front -.3
			// Z: Bottom .8

			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

			using (RebuildLock())
			{
				MaxTemperature = agg_basics.Clamp(MaxTemperature, 140, 400, ref valuesChanged);
				Sections = agg_basics.Clamp(Sections, 2, 20, ref valuesChanged);
				ChangeAmount = agg_basics.Clamp(ChangeAmount, 1, 30, ref valuesChanged);

				using (new CenterAndHeightMaintainer(this))
				{
					Children.Modify(async (children) =>
					{
						children.Clear();

						// add the base
						var towerBase = new Object3D()
						{
							Mesh = new RoundedRect(-25, -15, 25, 15, 3)
							{
								ResolutionScale = 10
							}.Extrude(BaseHeight),
							Name = "Base"
						};

						children.Add(towerBase);

						// Add each section
						for (int i = 0; i < Sections; i++)
						{
							var temp = MaxTemperature - i * ChangeAmount;
							var section = new Object3D()
							{
								Matrix = Matrix4X4.CreateTranslation(0, 0, BaseHeight + i * SectionHeight),
								Name = $"{temp:0.##}"
							};
							children.Add(section);
							// Add base mesh
							section.Children.Add(new Object3D()
							{
								Mesh = shape,
								Name = "CC - gaaZolee - AS"
							});
							// Add temp changer
							section.Children.Add(new SetTemperatureObject3D()
							{
								Temperature = temp,
								Name = $"Set to {temp:0.##}",
								Matrix = Matrix4X4.CreateScale(.2, .1, 1)
							});
							// Add temperature text
							var text = new TextObject3D()
							{
								Font = NamedTypeFace.Fredoka,
								Height = 1,
								Name = $"{temp:0.##}",
								PointSize = 10,
								NameToWrite = $"{temp:0.##}",
								Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 4) * Matrix4X4.CreateTranslation(0, -4.3, .8),
							};
							text.Rebuild().Wait();
							var textBounds = text.GetAxisAlignedBoundingBox();
							text.Matrix *= Matrix4X4.CreateTranslation(11 - textBounds.MaxXYZ.X, 0, 0);
							section.Children.Add(text);
						}
					});
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