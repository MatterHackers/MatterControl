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
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public static class Object3DExtensions
	{
		public static IObject3D Translate(this IObject3D objectToTranslate, double x = 0, double y = 0, double z = 0, string name = "")
		{
			return objectToTranslate.Translate(new Vector3(x, y, z), name);
		}

		public static IObject3D Translate(this IObject3D objectToTranslate, Vector3 translation, string name = "")
		{
			objectToTranslate.Matrix *= Matrix4X4.CreateTranslation(translation);
			return objectToTranslate;
		}

		public static IObject3D Minus(this IObject3D a, IObject3D b)
		{
			var resultsA = a.Clone();
			SubtractEditor.Subtract(resultsA.VisibleMeshes().ToList(), b.VisibleMeshes().ToList());
			return resultsA;
		}

		public static void AddSelectionAsChildren(this InteractiveScene scene, IObject3D newParent)
		{
			if (scene.HasSelection)
			{
				IObject3D item;

				List<IObject3D> itemsToReplace;

				if (scene.SelectedItem is SelectionGroup)
				{
					Object3D container = new Object3D();
					itemsToReplace = scene.SelectedItem.Children.ToList();
					foreach (var child in itemsToReplace)
					{
						container.Children.Add(child.Clone());
					}
					item = container;
				}
				else
				{
					itemsToReplace = new List<IObject3D> { scene.SelectedItem };
					item = scene.SelectedItem.Clone();
				}

				scene.SelectedItem = null;

				newParent.Children.Add(item);

				newParent.MakeNameNonColliding();

				scene.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						itemsToReplace,
						new List<IObject3D> { newParent }));

				if (newParent is HoldChildProportional pe)
				{
					item.Matrix = Matrix4X4.Identity;

					// Make the object have an identity matrix and keep its position in our new object
					newParent.Matrix = item.Matrix;
					pe.InitialChildBounds = item.GetAxisAlignedBoundingBox();
				}

				scene.SelectedItem = newParent;
			}
		}

		public static void WrapWith(this IObject3D originalItem, IObject3D wrapper, BedConfig sceneContext)
		{
			originalItem.Parent.Children.Modify(list =>
			{
				list.Remove(originalItem);

				wrapper.Matrix = originalItem.Matrix;

				originalItem.Matrix = Matrix4X4.Identity;
				wrapper.Children.Add(originalItem);

				list.Add(wrapper);
			});

			sceneContext.Scene.SelectedItem = wrapper;
		}

		public static void Unwrap(this IObject3D item, BedConfig sceneContext)
		{
			foreach (var child in item.Children)
			{
				child.Matrix *= item.Matrix;
			}

			item.Parent.Children.Modify(list =>
			{
				list.Remove(item);
				list.AddRange(item.Children);
			});

			sceneContext.Scene.SelectedItem = null;
		}

		public static void ApplyAtBoundsCenter(this IObject3D object3DToApplayTo, Matrix4X4 transformToApply)
		{
			object3DToApplayTo.Matrix = ApplyAtCenter(object3DToApplayTo.GetAxisAlignedBoundingBox(Matrix4X4.Identity), object3DToApplayTo.Matrix, transformToApply);
		}

		public static Matrix4X4 ApplyAtCenter(AxisAlignedBoundingBox boundsToApplyTo, Matrix4X4 currentTransform, Matrix4X4 transformToApply)
		{
			return ApplyAtPosition(currentTransform, transformToApply, boundsToApplyTo.Center);
		}

		public static Matrix4X4 ApplyAtPosition(Matrix4X4 currentTransform, Matrix4X4 transformToApply, Vector3 postionToApplyAt)
		{
			currentTransform *= Matrix4X4.CreateTranslation(-postionToApplyAt);
			currentTransform *= transformToApply;
			currentTransform *= Matrix4X4.CreateTranslation(postionToApplyAt);

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