/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Library
{
	public class InsertionGroupObject3D : Object3D
	{
		internal static Mesh placeHolderMesh;

		private InteractiveScene scene;
		private View3DWidget view3DWidget;

		private Action<IObject3D, IEnumerable<IObject3D>> layoutParts;

		public Task LoadingItemsTask { get; }

		static InsertionGroupObject3D()
		{
			// Create the placeholder mesh and position it at z0
			placeHolderMesh = PlatonicSolids.CreateCube(20, 20, 20);
			placeHolderMesh.Translate(new Vector3(0, 0, 10));
		}

		// TODO: Figure out how best to collapse the InsertionGroup after the load task completes
		public InsertionGroupObject3D(IEnumerable<ILibraryItem> items,
			View3DWidget view3DWidget,
			InteractiveScene scene,
			Vector2 newItemOffset,
			Action<IObject3D, IEnumerable<IObject3D>> layoutParts,
			bool trackSourceFiles = false)
		{
			if (items == null)
			{
				return;
			}

			this.layoutParts = layoutParts;

			// Add a temporary placeholder to give us some bounds
			this.scene = scene;
			this.view3DWidget = view3DWidget;

			this.LoadingItemsTask = Task.Run(async () =>
			{
				var offset = Matrix4X4.Identity;

				// Add the placeholder 'Loading...' object
				var placeholderItem = new Object3D()
				{
					Mesh = placeHolderMesh,
					Matrix = Matrix4X4.Identity,
					Parent = this
				};

				this.Children.Add(placeholderItem);

				// Filter to content file types only
				foreach (var item in items.Where(item => item.IsContentFileType()).ToList())
				{
					// Acquire
					var progressControl = new DragDropLoadProgress(view3DWidget, null, ApplicationController.Instance.Theme);

					// Position at accumulating offset
					placeholderItem.Matrix *= Matrix4X4.CreateTranslation(newItemOffset.X, (double)newItemOffset.Y, 0);
					placeholderItem.Visible = true;
					progressControl.TrackingObject = placeholderItem;

					var loadedItem = await item.CreateContent(progressControl.ProgressReporter);
					if (loadedItem != null)
					{
						var aabb = loadedItem.GetAxisAlignedBoundingBox();

						// lets move the cube to the center of the loaded thing
						placeholderItem.Matrix *= Matrix4X4.CreateTranslation(-10 + aabb.XSize / 2, 0, 0);

						placeholderItem.Visible = false;

						// Copy scale/rotation/translation from the source and Center
						loadedItem.Matrix = loadedItem.Matrix * Matrix4X4.CreateTranslation((double)-aabb.Center.X, (double)-aabb.Center.Y, (double)-aabb.MinXYZ.Z) * placeholderItem.Matrix;

						// check if the item has 0 height (it is probably an image)
						if (loadedItem.ZSize() == 0)
						{
							// raise it up a bit so it is not z fighting with the bed
							loadedItem.Matrix *= Matrix4X4.CreateTranslation(0, 0, .1);
						}

						loadedItem.Color = loadedItem.Color;

						// Set mesh path if tracking requested
						if (trackSourceFiles
							&& item is FileSystemFileItem fileItem
							&& item.IsMeshFileType())
						{
							loadedItem.MeshPath = fileItem.Path;
						}

						this.Children.Add(loadedItem);

						loadedItem.MakeNameNonColliding();

						// Adjust next item position
						newItemOffset.X = loadedItem.GetAxisAlignedBoundingBox().XSize / 2 + 10;
					}

					// the 1.3 is so the progress bar will collapse after going past 1
					progressControl.ProgressReporter(1.3, "");
				}

				this.Children.Remove(placeholderItem);
				this.Collapse();

				this.Invalidate(InvalidateType.Children);
			});
		}

		/// <summary>
		/// Collapse the InsertionGroup into the scene
		/// </summary>
		public void Collapse()
		{
			// Drag operation has finished, we need to perform the collapse
			var loadedItems = this.Children;

			// If we only have one item it may be a mcx wrapper, collapse that first.
			if (loadedItems.Count == 1)
			{
				var first = loadedItems.First();
				if (first.GetType() == typeof(Object3D)
					&& first.Mesh == null
					&& first.Children.Count == 1)
				{
					// collapse our first child into this
					this.Children.Modify(list =>
					{
						first.CollapseInto(list, false);
					});
				}

				loadedItems = this.Children;
			}

			foreach (var item in loadedItems)
			{
				item.Matrix *= this.Matrix;
			}

			view3DWidget.Scene.Children.Remove(this);

			if (layoutParts != null)
			{
				var allBedItems = new List<IObject3D>(view3DWidget.Scene.Children);

				foreach (var item in loadedItems)
				{
					layoutParts?.Invoke(item, allBedItems);
					allBedItems.Add(item);
				}
			}

			view3DWidget.Scene.UndoBuffer.AddAndDo(new InsertCommand(view3DWidget.Scene, loadedItems));
		}
	}
}
