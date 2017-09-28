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
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class InsertionGroup : Object3D
	{
		private View3DWidget view3DWidget;

		// TODO: Figure out how best to collapse the InsertionGroup after the load task completes
		public InsertionGroup(IEnumerable<ILibraryItem> items, View3DWidget view3DWidget, InteractiveScene scene, Func<bool> dragOperationActive)
		{
			Task.Run(async () =>
			{
				var newItemOffset = Vector2.Zero;

				// Filter to content file types only
				foreach (var item in items.Where(item => item.IsContentFileType()).ToList())
				{
					// Acquire
					var progressControl = new DragDropLoadProgress(view3DWidget, null);

					var contentResult = item.CreateContent(progressControl.ProgressReporter);
					if (contentResult != null)
					{
						// Add the placeholder
						var object3D = contentResult.Object3D;

						// HACK: set Parent ourselves so it can be used in the progress control
						object3D.Parent = this;
						this.Children.Add(object3D);

						// Position at accumulating offset
						object3D.Matrix *= Matrix4X4.CreateTranslation(newItemOffset.x, newItemOffset.y, 0);

						progressControl.TrackingObject = object3D;

						// Wait for content to load
						await contentResult.MeshLoaded;

						// Adjust next item position
						// TODO: do something more interesting than increment in x
						newItemOffset.x += contentResult.Object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity).XSize;
					}
				}

				if (dragOperationActive())
				{
					// Setting the selection group ensures that on lose focus this object will be collapsed
					this.ItemType = Object3DTypes.SelectionGroup;
				}
				else
				{
					// Drag operation has finished, we need to perform the collapse
					var loadedItems = this.Children;

					// Collapse our contents into the root of the scene
					// of the scene when it loses focus
					scene.Children.Modify(list =>
					{
						this.CollapseInto(list, Object3DTypes.Any);
					});

					if (scene.SelectedItem == this
						&& loadedItems.Count > 0)
					{
						scene.ClearSelection();

						foreach (var item in loadedItems)
						{
							scene.AddToSelection(item);
						}
					}
				}

				view3DWidget.PartHasBeenChanged();
			});
		}
	}
}
