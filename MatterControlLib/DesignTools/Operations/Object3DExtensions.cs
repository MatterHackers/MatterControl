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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.RenderOpenGl.OpenGl;
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
				var count = 6;
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
					_primitiveColors.Add("Cylinder", BlendHsl("Text", "HalfSphere", 1, count));
					_primitiveColors.Add("Cone", BlendHsl("Text", "HalfSphere", 2, count));
					_primitiveColors.Add("HalfCylinder", BlendHsl("Text", "HalfSphere", 3, count));
					_primitiveColors.Add("Torus", BlendHsl("Text", "HalfSphere", 4, count));
					_primitiveColors.Add("Ring", BlendHsl("Text", "HalfSphere", 5, count));
					_primitiveColors.Add("Sphere", BlendHsl("Text", "HalfSphere", 6, count));
					// end color
				}

				return _primitiveColors;
			}
		}

		public static void ShowRenameDialog(this IObject3D item, UndoBuffer undoBuffer)
		{
			DialogWindow.Show(
				new InputBoxPage(
					"Rename Item".Localize(),
					"Name".Localize(),
					item.Name,
					"Enter New Name Here".Localize(),
					"Rename".Localize(),
					(inputName) =>
					{
						var newName = inputName;
						var oldName = item.Name;
						if (newName != oldName)
						{
							undoBuffer.AddAndDo(new UndoRedoActions(() =>
							{
								item.Name = oldName;
							},
							() =>
							{
								item.Name = newName;
							}));
						}
					}));
		}

		public static PrinterConfig ContainingPrinter(this IObject3D object3D)
		{
			foreach (var printer in ApplicationController.Instance.ActivePrinters)
			{
				if (printer.Bed.Scene.Descendants().Contains(object3D))
				{
					return printer;
				}
			}

			return null;
		}

		public static InteractiveScene ContainingScene(this IObject3D object3D)
		{
			foreach (var workspace in ApplicationController.Instance.Workspaces)
			{
				if (workspace.SceneContext.Scene.Descendants().Contains(object3D))
				{
					return workspace.SceneContext.Scene;
				}
			}

			return null;
		}

		public static void ReloadEditorPannel(this IObject3D object3D)
		{
			// de-select and select this object
			var scene = object3D.ContainingScene();
			if (scene != null
				&& (object3D.Parents().Contains(scene.SelectedItem)
				|| object3D == scene.SelectedItem))
			{
				using(new SelectionMaintainer(scene))
                {
                }
			}
		}

		public static int EstimatedMemory(this IObject3D object3D)
		{
			return 0;
		}

		public static void FlattenToPathObject(this IObject3D item, UndoBuffer undoBuffer)
		{
			if (item.VertexSource != null)
			{
				using (item.RebuildLock())
				{
					var newPathObject = new PathObject3D();
					newPathObject.VertexSource = new VertexStorage(item.VertexSource);

					// and replace us with the children
					var replaceCommand = new ReplaceCommand(new[] { item }, new[] { newPathObject });

					if (undoBuffer != null)
					{
						undoBuffer.AddAndDo(replaceCommand);
					}
					else
					{
						replaceCommand.Do();
					}

					newPathObject.MakeNameNonColliding();
				}
			}
		}

		public static void DrawPath(this IObject3D item)
		{
			if (item.VertexSource != null)
			{
				bool first = true;
				var lastPosition = Vector2.Zero;
				var maxXYZ = item.GetAxisAlignedBoundingBox().MaxXYZ;
				maxXYZ = maxXYZ.Transform(item.Matrix.Inverted);
				var firstMove = Vector2.Zero;
				foreach (var vertex in item.VertexSource.Vertices())
				{
					var position = vertex.position;
					if (first)
					{
						GL.PushMatrix();
						GL.PushAttrib(AttribMask.EnableBit);
						GL.MultMatrix(item.WorldMatrix().GetAsFloatArray());

						GL.Disable(EnableCap.Texture2D);
						GL.Disable(EnableCap.Blend);

						GL.Begin(BeginMode.Lines);
						GL.Color4(255, 0, 0, 255);
					}

					if (vertex.IsMoveTo)
					{
						firstMove = position;
					}
					else if (vertex.IsLineTo)
					{
						GL.Vertex3(lastPosition.X, lastPosition.Y, maxXYZ.Z + 0.002);
						GL.Vertex3(position.X, position.Y, maxXYZ.Z + 0.002);
					}
					else if (vertex.IsClose)
					{
						GL.Vertex3(firstMove.X, firstMove.Y, maxXYZ.Z + 0.002);
						GL.Vertex3(lastPosition.X, lastPosition.Y, maxXYZ.Z + 0.002);
					}

					lastPosition = position;
					first = false;
				}

				// if we drew anything
				if (!first)
				{
					GL.End();
					GL.PopAttrib();
					GL.PopMatrix();
				}
			}
		}

		public static AxisAlignedBoundingBox GetWorldspaceAabbOfDrawPath(this IObject3D item)
		{
			AxisAlignedBoundingBox box = AxisAlignedBoundingBox.Empty();

			if (item.VertexSource != null)
			{
				var lastPosition = Vector2.Zero;
				var maxXYZ = item.GetAxisAlignedBoundingBox().MaxXYZ;
				maxXYZ = maxXYZ.Transform(item.Matrix.Inverted);

				foreach (var vertex in item.VertexSource.Vertices())
				{
					var position = vertex.position;

					if (vertex.IsLineTo)
					{
						box.ExpandToInclude(new Vector3(lastPosition, maxXYZ.Z + 0.002));
						box.ExpandToInclude(new Vector3(position, maxXYZ.Z + 0.002));
					}

					lastPosition = position;
				}

				return box.NewTransformed(item.WorldMatrix());
			}

			return box;
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
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(object3D.ToJson().Result)))
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

		/// <summary>
		/// Union a and b together. This can either return a single item with a mesh on it
		/// or a group item that has the a and b items as children
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="doMeshCombine"></param>
		/// <returns></returns>
		public static IObject3D Plus(this IObject3D a, IObject3D b, bool doMeshCombine = false)
		{
			if (doMeshCombine)
			{
				var combine = new CombineObject3D_2();
				combine.Children.Add(a.Clone());
				combine.Children.Add(b.Clone());

				combine.Combine();

				var finalMesh = combine.VisibleMeshes().First().Mesh;
				return new Object3D()
				{
					Mesh = finalMesh
				};
			}
			else
			{
				var group = new GroupObject3D();
				group.Children.Add(a);
				group.Children.Add(b);
				return group;
			}
		}

		public static IObject3D Minus(this IObject3D a, IObject3D b)
		{
			var subtract = new SubtractObject3D();
			subtract.Children.Add(a.Clone());
			var bClone = b.Clone();
			subtract.Children.Add(bClone);
			subtract.SelectedChildren.Add(bClone.ID);

			subtract.Subtract();

			var finalMesh = subtract.VisibleMeshes().First().Mesh;
			return new Object3D()
			{
				Mesh = finalMesh
			};
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

		public static async void AddSelectionAsChildren(this IObject3D newParent, InteractiveScene scene, IObject3D selectedItem)
		{
			if (selectedItem != null)
			{
				var selectedItems = scene.GetSelectedItems();

				scene.SelectedItem = null;
				using (selectedItem.Parent.RebuildLock())
				{
					var name = "";
					foreach (var item in selectedItems)
					{
						newParent.Children.Add(item.Clone());
						if (name != "")
						{
							name += ", ";
						}
						name += item.Name;
					}

					if (string.IsNullOrEmpty(newParent.Name))
					{
						newParent.Name = name;
					}

					newParent.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(
						new ReplaceCommand(
							selectedItems,
							new[] { newParent }));
				}

				scene.SelectedItem = newParent;

				await newParent.Rebuild();
			}
		}

		public static Matrix4X4 ApplyAtBoundsCenter(this IObject3D objectWithBounds, Matrix4X4 transformToApply)
		{
			return ApplyAtCenter(objectWithBounds.Matrix, objectWithBounds.GetAxisAlignedBoundingBox(), transformToApply);
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
			return item.GetAxisAlignedBoundingBox().Center;
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