/*
Copyright (c) 2017, John Lewin
All rights reserved.
*/

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.Plugins.VelocityPainting
{
	public class VelocityPaintObject3D : Object3D, IGCodePostProcessor
	{
		public VelocityPaintObject3D()
		{
			Name = "Velocity Paint".Localize();
			Color = new Agg.Color(Agg.Color.Yellow, 40);
		}

		public static async Task<VelocityPaintObject3D> Create()
		{
			var item = new VelocityPaintObject3D();
			item.Children.Add(new ImageObject3D()
			{
				AssetPath = AggContext.StaticData.ToAssetPath(Path.Combine("Images", "mh-logo.png"))
			});

			await item.Rebuild();
			return item;
		}

		public int HighSpeed { get; set; } = 135;

		public int LowSpeed { get; set; } = 20;


		public int OutsideSpeed { get; set; } = 35;

		public Stream ProcessOutput(Stream sourceStream)
		{
			var memoryStream = new MemoryStream();
			var outputStream = new StreamWriter(memoryStream);

			var imageObject = this.Children.OfType<ImageObject3D>().FirstOrDefault();

			// Mode	printCentreX	printCentreY	imageWidth	imageHeight	zOffset	targetSpeed	lowSpeed	highSpeed	imageFile	sourceGcodeFile
			// projectY centreX          centreY           72           142      20     3600        800          3600    ClimbingRose2.jpg CurveacousVase5_m_20.gcode

			var aabb = this.GetAxisAlignedBoundingBox();
			var center = aabb.Center;

			var painter = new VelocityPainter(outputStream, sourceStream)
			{
				HighSpeed = this.HighSpeed,
				LowSpeed = this.LowSpeed,
				OutsideSpeed = this.OutsideSpeed,
				printCentreX = center.X,
				printCentreY = center.Y,
				projectedImageWidth = aabb.XSize,
				projectedImageHeight = aabb.ZSize,
				zOffset = aabb.MinXYZ.Z,
			};

			painter.Generate(imageObject.Image, VelocityPainter.ProjectionMode.ProjectY);

			memoryStream.Position = 0;

			return memoryStream;
		}

		public override Task Rebuild()
		{
			using (new CenterAndHeightMaintainer(this))
			{
				this.Mesh = PlatonicSolids.CreateCube(20, 20, 20);
			}

			return Task.CompletedTask;
		}
	}
}