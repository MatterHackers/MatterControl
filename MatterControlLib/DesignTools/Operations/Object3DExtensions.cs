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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public static class Object3DExtensions
	{

		public static Color BlendHsl(string a, string b, int index, int count)
		{
			return PrimitiveColors[a].BlendHsl(PrimitiveColors[b], 1.0 / (count + 1.0) * index);
		}

		static Dictionary<string, Color> _primitiveColors;
		public static Dictionary<string, Color> PrimitiveColors
		{
			get
			{
				if (_primitiveColors == null)
				{
					_primitiveColors = new Dictionary<string, Color>();
					// put in all the constant things before blending them
					_primitiveColors.Add("Cube", Color.FromHSL(.01, .98, .76)); // red
					_primitiveColors.Add("Text", Color.FromHSL(.175, .98, .76)); // yellow
					_primitiveColors.Add("HalfSphere", Color.FromHSL(.87, .98, .76)); // violet

					// first color
					_primitiveColors.Add("Pyramid", BlendHsl("Cube", "Text", 1, 3));
					_primitiveColors.Add("Wedge", BlendHsl("Cube", "Text", 2, 3));
					_primitiveColors.Add("HalfWedge", BlendHsl("Cube", "Text", 3, 3));
					// mid color
					_primitiveColors.Add("Cylinder", BlendHsl("Text", "HalfSphere", 1, 6));
					_primitiveColors.Add("Cone", BlendHsl("Text", "HalfSphere", 2, 6));
					_primitiveColors.Add("HalfCylinder", BlendHsl("Text", "HalfSphere", 3, 6));
					_primitiveColors.Add("Torus", BlendHsl("Text", "HalfSphere", 4, 6));
					_primitiveColors.Add("Ring", BlendHsl("Text", "HalfSphere", 5, 6));
					_primitiveColors.Add("Sphere", BlendHsl("Text", "HalfSphere", 6, 6));
					// end color
				}

				return _primitiveColors;
			}
		}

		public static int EstimatedMemory(this IObject3D object3D)
		{
			return 0;
		}

		public static bool IsRoot(this IObject3D object3D)
		{
			return object3D.Parent == null;
		}

		public static string ComputeSHA1(this IObject3D object3D)
		{
			// *******************************************************************************************************************************
			// TODO: We must ensure we always compute with a stream that marks for UTF encoding with BOM, irrelevant of in-memory or on disk
			// *******************************************************************************************************************************

			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(object3D.ToJson())))
			{
				return HashGenerator.ComputeSHA1(memoryStream);
			}
		}

		public static IObject3D Translate(this IObject3D objectToTranslate, double x = 0, double y = 0, double z = 0, string name = "")
		{
			return objectToTranslate.Translate(new Vector3(x, y, z), name);
		}

		public static IObject3D Translate(this IObject3D objectToTranslate, Vector3 translation, string name = "")
		{
			objectToTranslate.Matrix *= Matrix4X4.CreateTranslation(translation);
			return objectToTranslate;
		}


		public static IObject3D Scale(this IObject3D objectToTranslate, double x = 0, double y = 0, double z = 0, string name = "")
		{
			return objectToTranslate.Scale(new Vector3(x, y, z), name);
		}

		public static IObject3D Scale(this IObject3D objectToTranslate, Vector3 translation, string name = "")
		{
			objectToTranslate.Matrix *= Matrix4X4.CreateScale(translation);
			return objectToTranslate;
		}

		public static IObject3D Minus(this IObject3D a, IObject3D b)
		{
			var resultsA = a.Clone();
			SubtractObject3D.Subtract(resultsA.VisibleMeshes().ToList(), b.VisibleMeshes().ToList());
			return resultsA;
		}

		private static VertexStorage CombinePaths(IVertexSource a, IVertexSource b, ClipType clipType)
		{
			List<List<IntPoint>> aPolys = a.CreatePolygons();
			List<List<IntPoint>> bPolys = b.CreatePolygons();

			Clipper clipper = new Clipper();

			clipper.AddPaths(aPolys, PolyType.ptSubject, true);
			clipper.AddPaths(bPolys, PolyType.ptClip, true);

			List<List<IntPoint>> intersectedPolys = new List<List<IntPoint>>();
			clipper.Execute(clipType, intersectedPolys);

			Clipper.CleanPolygons(intersectedPolys);

			VertexStorage output = intersectedPolys.CreateVertexStorage();

			output.Add(0, 0, ShapePath.FlagsAndCommand.Stop);

			return output;
		}

		public static Affine GetCenteringTransformExpandedToRadius(this IVertexSource vertexSource, double radius)
		{
			var circle = SmallestEnclosingCircle.MakeCircle(vertexSource.Vertices().Select((v) => new Vector2(v.position.X, v.position.Y)));

			// move the circle center to the origin
			var centering = Affine.NewTranslation(-circle.Center);
			// scale to the fit size in x y
			double scale = radius / circle.Radius;
			var scalling = Affine.NewScaling(scale);

			return centering * scalling;
		}

		public static Affine GetCenteringTransformVisualCenter(this IVertexSource vertexSource, double goalRadius)
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

		public static VertexStorage Offset(this IVertexSource a, double distance)
		{
			List<List<IntPoint>> aPolys = a.CreatePolygons();

			ClipperOffset offseter = new ClipperOffset();
			offseter.AddPaths(aPolys, JoinType.jtMiter, EndType.etClosedPolygon);
			var solution = new List<List<IntPoint>>();
			offseter.Execute(ref solution, distance * 1000);

			Clipper.CleanPolygons(solution);

			VertexStorage output = solution.CreateVertexStorage();

			output.Add(0, 0, ShapePath.FlagsAndCommand.Stop);

			return output;
		}

		public static IObject3D Plus(this IObject3D a, IObject3D b)
		{
			var results = new Object3D();

			results.Children.Add(a.Clone());
			results.Children.Add(b.Clone());

			return results;
		}

		public static IVertexSource Minus(this IVertexSource a, IVertexSource b)
		{
			return CombinePaths(a, b, ClipType.ctDifference);
		}

		public static IVertexSource Plus(this IVertexSource a, IVertexSource b)
		{
			return CombinePaths(a, b, ClipType.ctUnion);
		}

		public static double XSize(this IObject3D item)
		{
			return item.GetAxisAlignedBoundingBox().XSize;
		}

		public static double YSize(this IObject3D item)
		{
			return item.GetAxisAlignedBoundingBox().YSize;
		}

		public static double ZSize(this IObject3D item)
		{
			return item.GetAxisAlignedBoundingBox().ZSize;
		}

		public static void AddSelectionAsChildren(this IObject3D newParent, InteractiveScene scene, IObject3D selectedItem)
		{
			if (selectedItem != null)
			{
				List<IObject3D> itemsToReplace;

				if (selectedItem is SelectionGroupObject3D)
				{
					itemsToReplace = selectedItem.Children.ToList();
					foreach (var child in itemsToReplace)
					{
						newParent.Children.Add(child.Clone());
					}
				}
				else
				{
					itemsToReplace = new List<IObject3D> { selectedItem };
					newParent.Children.Add(selectedItem.Clone());
				}

				scene.SelectedItem = null;

				newParent.MakeNameNonColliding();

				scene.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						itemsToReplace,
						new List<IObject3D> { newParent }));

				SelectModifiedItem(selectedItem, newParent, scene);
			}
		}

		public static void WrapWith(this IObject3D originalItem, IObject3D wrapper, InteractiveScene scene)
		{
			// make sure we walk to the top of a mesh wrapper stack before we wrap the item (all mesh wrappers should be under the add)
			while(originalItem.Parent is ModifiedMeshObject3D modifiedMesh)
			{
				originalItem = modifiedMesh;
			}

			using (originalItem.RebuildLock())
			{
				originalItem.Parent.Children.Modify(list =>
				{
					list.Remove(originalItem);

					wrapper.Matrix = originalItem.Matrix;

					originalItem.Matrix = Matrix4X4.Identity;
					wrapper.Children.Add(originalItem);

					list.Add(wrapper);
				});

				SelectModifiedItem(originalItem, wrapper, scene);
			}
		}

		private static void SelectModifiedItem(IObject3D originalItem, IObject3D wrapper, InteractiveScene scene)
		{
			if (scene != null)
			{
				var topParent = wrapper.Parents<IObject3D>().LastOrDefault((i) => i.Parent != null);
				UiThread.RunOnIdle(() =>
				{
					var sceneSelection = topParent != null ? topParent : wrapper;
					scene.SelectedItem = sceneSelection;

					if (sceneSelection != originalItem)
					{
						// select the sub item in the tree view
						//scene.SelectedItem =  originalItem;
					}
				});
			}
		}

		public static Matrix4X4 ApplyAtBoundsCenter(this IObject3D objectWithBounds, Matrix4X4 transformToApply)
		{
			return ApplyAtCenter(objectWithBounds.Matrix, objectWithBounds.GetAxisAlignedBoundingBox(Matrix4X4.Identity), transformToApply);
		}

		public static Matrix4X4 ApplyAtCenter(this Matrix4X4 currentTransform, AxisAlignedBoundingBox boundsToApplyTo, Matrix4X4 transformToApply)
		{
			return ApplyAtPosition(currentTransform, boundsToApplyTo.Center, transformToApply);
		}

		public static Matrix4X4 ApplyAtPosition(this IObject3D item, Vector3 positionToApplyAt, Matrix4X4 transformToApply)
		{
			return item.Matrix.ApplyAtPosition(positionToApplyAt, transformToApply);
		}

		public static Matrix4X4 ApplyAtPosition(this Matrix4X4 currentTransform, Vector3 positionToApplyAt, Matrix4X4 transformToApply)
		{
			currentTransform *= Matrix4X4.CreateTranslation(-positionToApplyAt);
			currentTransform *= transformToApply;
			currentTransform *= Matrix4X4.CreateTranslation(positionToApplyAt);

			return currentTransform;
		}

		public static Vector3 GetCenter(this IObject3D item)
		{
			return item.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;
		}

		public static IObject3D SetChildren(this IObject3D parent, IEnumerable<IObject3D> newChildren)
		{
			parent.Children.Modify((list) =>
			{
				list.Clear();
				list.AddRange(newChildren);
			});

			return parent;
		}

		public static void SetChildren(this IObject3D parent, IObject3D newChild)
		{
			parent.Children.Modify((list) =>
			{
				list.Clear();
				list.Add(newChild);
			});
		}
	}
}