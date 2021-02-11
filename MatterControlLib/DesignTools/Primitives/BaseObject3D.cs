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
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools
{
	public enum BaseTypes
	{
		None,
		Rectangle,
		Circle,
		/* Oval, Frame,*/
		Outline
	}

	public class BaseObject3D : Object3D, IPropertyGridModifier
	{
		public enum CenteringTypes
		{
			Bounds,
			Weighted
		}

		private readonly double scalingForClipper = 1000;

		public BaseObject3D()
		{
			Name = "Base".Localize();
		}

		public override bool CanFlatten => true;

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Tabs)]
		public BaseTypes BaseType { get; set; } = BaseTypes.Circle;

		[DisplayName("Expand")]
		public double BaseSize { get; set; } = 3;

		public double InfillAmount { get; set; } = 3;

		[DisplayName("Height")]
		public double ExtrusionHeight { get; set; } = 5;

		[ReadOnly(true)]
		public string NoBaseMessage { get; set; } = "No base is added under your part. Switch to a different base option to create a base.";

		[DisplayName("")]
		[ReadOnly(true)]
		public string SpaceHolder1 { get; set; } = "";

		[DisplayName("")]
		[ReadOnly(true)]
		public string SpaceHolder2 { get; set; } = "";

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public CenteringTypes Centering { get; set; } = CenteringTypes.Weighted;

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					var firstChild = this.Children.FirstOrDefault();

					// only keep the first object
					this.Children.Modify(list =>
					{
						list.Clear();
						// add back in the sourceContainer
						list.Add(firstChild);
					});
				}
			}

			base.Remove(undoBuffer);
		}

		private IVertexSource meshVertexSource;

		[JsonIgnore]
		public IVertexSource VertexSource
		{
			get
			{
				var vertexSource = (IPathObject)this.Descendants<IObject3D>().FirstOrDefault((i) => i is IPathObject);
				var meshSource = this.Descendants<IObject3D>().FirstOrDefault((i) => i.Mesh != null);
				var mesh = meshSource?.Mesh;
				if (vertexSource?.VertexSource == null
					&& mesh != null)
				{
					if (meshVertexSource == null)
					{
						// return the vertex source of the bottom of the mesh
						var aabb = this.GetAxisAlignedBoundingBox();
						var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, aabb.MinXYZ.Z + .1));
						cutPlane = Plane.Transform(cutPlane, this.Matrix.Inverted);
						var slice = SliceLayer.CreateSlice(mesh, cutPlane);
						meshVertexSource = slice.CreateVertexStorage();
					}

					return meshVertexSource;
				}

				return vertexSource?.VertexSource;
			}

			set
			{
				var vertexSource = this.Children.OfType<IPathObject>().FirstOrDefault();
				if (vertexSource != null)
				{
					vertexSource.VertexSource = value;
				}
			}
		}

		public static async Task<BaseObject3D> Create()
		{
			var item = new BaseObject3D();
			await item.Rebuild();
			return item;
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Path)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				// make sure we clear our cache
				meshVertexSource = null;
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

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = this.RebuildLock();

			return ApplicationController.Instance.Tasks.Execute(
				"Base".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					using (new CenterAndHeightMaintainer(this, CenterAndHeightMaintainer.MaintainFlags.Height))
					{
						var firstChild = this.Children.FirstOrDefault();

						// remove the base mesh we added
						this.Children.Modify(list =>
						{
							list.Clear();
							// add back in the sourceContainer
							list.Add(firstChild);
						});

						// and create the base
						var vertexSource = this.VertexSource;

						// Convert VertexSource into expected Polygons
						Polygons polygonShape = (vertexSource == null) ? null : vertexSource.CreatePolygons();
						GenerateBase(polygonShape, firstChild.GetAxisAlignedBoundingBox().MinXYZ.Z);
					}

					rebuildLock.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					return Task.CompletedTask;
				});
		}

		private static Polygon GetBoundingPolygon(Polygons basePolygons)
		{
			var min = new IntPoint(long.MaxValue, long.MaxValue);
			var max = new IntPoint(long.MinValue, long.MinValue);

			foreach (Polygon polygon in basePolygons)
			{
				foreach (IntPoint point in polygon)
				{
					min.X = Math.Min(point.X - 10, min.X);
					min.Y = Math.Min(point.Y - 10, min.Y);
					max.X = Math.Max(point.X + 10, max.X);
					max.Y = Math.Max(point.Y + 10, max.Y);
				}
			}

			var boundingPoly = new Polygon();
			boundingPoly.Add(min);
			boundingPoly.Add(new IntPoint(min.X, max.Y));
			boundingPoly.Add(max);
			boundingPoly.Add(new IntPoint(max.X, min.Y));

			return boundingPoly;
		}

		private Polygon GetBoundingCircle(Polygons basePolygons)
		{
			IntPoint center;
			double radius;

			if (Centering == CenteringTypes.Bounds)
			{
				IEnumerable<Vector2> GetVertices()
				{
					foreach (var polygon in basePolygons)
					{
						foreach (var positon in polygon)
						{
							yield return new Vector2(positon.X, positon.Y);
						}
					}
				}

				var circle = SmallestEnclosingCircle.MakeCircle(GetVertices());

				center = new IntPoint(circle.Center.X, circle.Center.Y);
				radius = (long)circle.Radius;
			}
			else
			{
				var outsidePolygons = new List<List<IntPoint>>();
				// remove all holes from the polygons so we only center the major outlines
				var polygons = VertexSource.CreatePolygons();

				foreach (var polygon in polygons)
				{
					if (polygon.GetWindingDirection() == 1)
					{
						outsidePolygons.Add(polygon);
					}
				}

				IVertexSource outsideSource = outsidePolygons.CreateVertexStorage();

				var polyCenter = outsideSource.GetWeightedCenter();

				center = new IntPoint(polyCenter.X * 1000, polyCenter.Y * 1000);
				radius = 0;

				foreach (Polygon polygon in basePolygons)
				{
					foreach (IntPoint point in polygon)
					{
						long length = (point - center).Length();
						if (length > radius)
						{
							radius = length;
						}
					}
				}
			}

			var boundingCircle = new Polygon();
			int numPoints = 100;

			for (int i = 0; i < numPoints; i++)
			{
				double angle = i / 100.0 * Math.PI * 2.0;
				IntPoint newPointOnCircle = new IntPoint(Math.Cos(angle) * radius, Math.Sin(angle) * radius) + center;
				boundingCircle.Add(newPointOnCircle);
			}

			return boundingCircle;
		}

		private static PolyTree GetPolyTree(Polygons basePolygons)
		{
			// create a bounding polygon to clip against
			Polygon boundingPoly = GetBoundingPolygon(basePolygons);

			var polyTreeForTrace = new PolyTree();

			var clipper = new Clipper();
			clipper.AddPaths(basePolygons, PolyType.ptSubject, true);
			clipper.AddPath(boundingPoly, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctIntersection, polyTreeForTrace);

			return polyTreeForTrace;
		}

		public void GenerateBase(Polygons polygonShape, double bottomWithoutBase)
		{
			if (polygonShape != null
				&& polygonShape.Select(p => p.Count).Sum() > 3)
			{
				Polygons polysToOffset = new Polygons();

				switch (BaseType)
				{
					case BaseTypes.Rectangle:
						polysToOffset.Add(GetBoundingPolygon(polygonShape));
						break;

					case BaseTypes.Circle:
						polysToOffset.Add(GetBoundingCircle(polygonShape));
						break;

					case BaseTypes.Outline:
						PolyTree polyTreeForBase = GetPolyTree(polygonShape);
						foreach (PolyNode polyToOffset in polyTreeForBase.Childs)
						{
							polysToOffset.Add(polyToOffset.Contour);
						}
						break;
				}

				if (polysToOffset.Count > 0)
				{
					Polygons basePolygons;

					if (BaseType == BaseTypes.Outline
						&& InfillAmount > 0)
					{
						basePolygons = polysToOffset.Offset((BaseSize + InfillAmount) * scalingForClipper);
						basePolygons = basePolygons.Offset(-InfillAmount * scalingForClipper);
					}
					else
					{
						basePolygons = polysToOffset.Offset(BaseSize * scalingForClipper);
					}

					basePolygons = ClipperLib.Clipper.CleanPolygons(basePolygons, 10);

					VertexStorage rawVectorShape = basePolygons.PolygonToPathStorage();
					var vectorShape = new VertexSourceApplyTransform(rawVectorShape, Affine.NewScaling(1.0 / scalingForClipper));

					var mesh = VertexSourceToMesh.Extrude(vectorShape, zHeightTop: ExtrusionHeight);
					mesh.Translate(new Vector3(0, 0, -ExtrusionHeight + bottomWithoutBase));

					var baseObject = new Object3D()
					{
						Mesh = mesh
					};
					Children.Add(baseObject);
				}
				else
				{
					// clear the mesh
					Mesh = null;
				}
			}
		}

		private Dictionary<string, bool> changeSet = new Dictionary<string, bool>();

		public void UpdateControls(PublicPropertyChange change)
		{
			changeSet.Clear();

			changeSet.Add(nameof(NoBaseMessage), BaseType == BaseTypes.None);
			changeSet.Add(nameof(SpaceHolder1), BaseType == BaseTypes.None || BaseType == BaseTypes.Rectangle);
			changeSet.Add(nameof(SpaceHolder2), BaseType == BaseTypes.None);
			changeSet.Add(nameof(BaseSize), BaseType != BaseTypes.None);
			changeSet.Add(nameof(InfillAmount), BaseType == BaseTypes.Outline);
			changeSet.Add(nameof(Centering), BaseType == BaseTypes.Circle);
			changeSet.Add(nameof(ExtrusionHeight), BaseType != BaseTypes.None);

			// first turn on all the settings we want to see
			foreach (var kvp in changeSet.Where(c => c.Value))
			{
				change.SetRowVisible(kvp.Key, () => kvp.Value);
			}

			// then turn off all the settings we want to hide
			foreach (var kvp in changeSet.Where(c => !c.Value))
			{
				change.SetRowVisible(kvp.Key, () => kvp.Value);
			}
		}
	}
}