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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	public class ImageCoinObject3D : Object3D
	{
		private readonly double innerDiameter = 35;
		private readonly double outerDiameter = 40;

		public ImageCoinObject3D()
		{
		}

		[JsonIgnore]
		public ImageObject3D ImageObject { get { return this.Children.OfType<ImageObject3D>().FirstOrDefault(); } set { } }

		[Description("Create a hook so that the coin can be hung from a chain.")]
		public bool CreateHook { get; set; } = true;

		[Description("Subtract the image from a disk so that the negative space will be printed.")]
		public bool NegativeSpace { get; set; } = false;

		[Description("Change the scale of the image within the coin.")]
		public double ScalePercent { get; set; } = 90;

		[Description("Normally the image is expanded to the edge. This will try to center the weight of the image visually.")]
		public bool AlternateCentering { get; set; } = false;

		[Description("Change the width of the image lines.")]
		public double Inflate { get; set; }

		public static async Task<ImageCoinObject3D> Create()
		{
			var imageCoin = new ImageCoinObject3D();
			await imageCoin.Rebuild();
			return imageCoin;
		}

		public override bool Persistable => ApplicationController.Instance.UserHasPermission(this);

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override async Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				var currentAssetPath = ImageObject == null ? AggContext.StaticData.ToAssetPath(Path.Combine("Images", "mh-logo.png")) : ImageObject.AssetPath;

				this.Children.Modify((list) =>
				{
					list.Clear();
				});

				var imageObject = new ImageObject3D()
				{
					AssetPath = currentAssetPath,
				};

				await imageObject.Rebuild();

				this.Children.Add(imageObject);

				IObject3D logoBase = new CylinderObject3D(outerDiameter, 3, 60);
				IObject3D logoRing = new AlignObject3D(new RingObject3D(outerDiameter, innerDiameter, 2, 60), FaceAlign.Bottom, logoBase, FaceAlign.Top);

				IObject3D coinBlank = logoBase.Plus(logoRing);
				if (CreateHook)
				{
					var cube = await CubeObject3D.Create(4, 2, 4);
					IObject3D connect = logoBase.Plus(new AlignObject3D(cube, FaceAlign.Front | FaceAlign.Bottom, logoBase, FaceAlign.Back | FaceAlign.Bottom, 0, -.5));
					IObject3D hook = logoBase.Plus(new AlignObject3D(new RingObject3D(10, 7, 5, 30), FaceAlign.Front | FaceAlign.Bottom, connect, FaceAlign.Back | FaceAlign.Bottom, 0, -.5));

					coinBlank = coinBlank.Plus(connect);
					coinBlank = coinBlank.Plus(hook);
				}

				var imageToPath = new ImageToPathObject3D();

				imageToPath.Children.Add(imageObject);

				await imageToPath.Rebuild();

				var inputShape = imageToPath.VertexSource;

				if (Inflate != 0)
				{
					var bounds = inputShape.GetBounds();
					var scale = Math.Max(bounds.Width, bounds.Height) / (17 * 4);
					inputShape = inputShape.Offset(Inflate * scale);
				}

				if (AlternateCentering)
				{
					inputShape = new VertexSourceApplyTransform(inputShape, GetCenteringTransformVisualCenter(inputShape, innerDiameter / 2));
				}
				else
				{
					inputShape = new VertexSourceApplyTransform(inputShape, GetCenteringTransformExpandedToRadius(inputShape, innerDiameter / 2));
				}

				if (ScalePercent != 100
					&& ScalePercent != 0)
				{
					inputShape = new VertexSourceApplyTransform(inputShape, Affine.NewScaling(ScalePercent / 100.0));
				}

				if (NegativeSpace)
				{
					var disk = new Ellipse(0, 0, innerDiameter / 2 + .2, innerDiameter / 2 + .2)
					{
						ResolutionScale = 1000
					};
					inputShape = disk.Minus(inputShape);
				}

				imageToPath.VertexSource = inputShape;

				var pathExtrusion = new LinearExtrudeObject3D();
				pathExtrusion.Children.Add(imageToPath);
				await pathExtrusion.Rebuild();

				IObject3D extrusionObject = imageObject;

				var loadingScale = 32 / extrusionObject.XSize();
				extrusionObject = new ScaleObject3D(extrusionObject, loadingScale, loadingScale, 1 / extrusionObject.ZSize());
				extrusionObject = PlaceOnBase(logoBase, extrusionObject);

				this.Children.Add(coinBlank);
				this.Children.Add(extrusionObject);
			}

			Invalidate(InvalidateType.Mesh);
		}

		private static Affine GetCenteringTransformExpandedToRadius(IVertexSource vertexSource, double radius)
		{
			var circle = SmallestEnclosingCircle.MakeCircle(vertexSource.Vertices().Select((v) => new Vector2(v.position.X, v.position.Y)));

			// move the circle center to the origin
			var centering = Affine.NewTranslation(-circle.Center);
			// scale to the fit size in x y
			double scale = radius / circle.Radius;
			var scalling = Affine.NewScaling(scale);

			return centering * scalling;
		}

		private static Affine GetCenteringTransformVisualCenter(IVertexSource vertexSource, double goalRadius)
		{
			var outsidePolygons = new List<List<IntPoint>>();
			// remove all holes from the polygons so we only center the major outlines
			var polygons = vertexSource.CreatePolygons();

			foreach (var polygon in polygons)
			{
				if (polygon.GetWindingDirection() == 1)
				{
					outsidePolygons.Add(polygon);
				}
			}

			IVertexSource outsideSource = outsidePolygons.CreateVertexStorage();

			Vector2 center = outsideSource.GetWeightedCenter();

			outsideSource = new VertexSourceApplyTransform(outsideSource, Affine.NewTranslation(-center));

			double radius = MaxXyDistFromCenter(outsideSource);

			double scale = goalRadius / radius;
			var scalling = Affine.NewScaling(scale);

			var centering = Affine.NewTranslation(-center);

			return centering * scalling;
		}

		private static double MaxXyDistFromCenter(IObject3D imageMesh)
		{
			double maxDistSqrd = 0.000001;
			var center = imageMesh.GetAxisAlignedBoundingBox().Center;
			var itemWithMesh = imageMesh.VisibleMeshes().First();
			var matrix = itemWithMesh.WorldMatrix(imageMesh);
			foreach (var vertex in itemWithMesh.Mesh.Vertices)
			{
				throw new NotImplementedException();
				//var position = vertex.Position;
				//var distSqrd = (new Vector2(position.X, position.Y) - new Vector2(center.X, center.Y)).LengthSquared;
				//if (distSqrd > maxDistSqrd)
				//{
				//	maxDistSqrd = distSqrd;
				//}
			}

			return Math.Sqrt(maxDistSqrd);
		}

		private static double MaxXyDistFromCenter(IVertexSource vertexSource)
		{
			double maxDistSqrd = 0.000001;
			var center = vertexSource.GetBounds().Center;
			foreach (var vertex in vertexSource.Vertices())
			{
				var position = vertex.position;
				var distSqrd = (new Vector2(position.X, position.Y) - new Vector2(center.X, center.Y)).LengthSquared;
				if (distSqrd > maxDistSqrd)
				{
					maxDistSqrd = distSqrd;
				}
			}

			return Math.Sqrt(maxDistSqrd);
		}

		private static IObject3D PlaceOnBase(IObject3D logoBase, IObject3D imageObject)
		{
			if (imageObject != null)
			{
				// put it at the right height
				imageObject = new AlignObject3D(imageObject, FaceAlign.Bottom, logoBase, FaceAlign.Top);
				// move it to the base center
				imageObject = new TranslateObject3D(imageObject, -new Vector3(logoBase.GetCenter().X, logoBase.GetCenter().Y, 0));
			}
			return imageObject;
		}
	}
}