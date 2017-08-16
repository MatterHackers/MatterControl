/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GroupCommand : IUndoRedoCommand
	{
		private IObject3D item;
		private View3DWidget view3DWidget;

		public GroupCommand(View3DWidget view3DWidget, IObject3D selectedItem)
		{
			this.view3DWidget = view3DWidget;
			this.item = selectedItem;

			if(view3DWidget.Scene.SelectedItem == selectedItem)
			{
				view3DWidget.Scene.ClearSelection();
			}
		}

		public async void Do()
		{
			if (view3DWidget.Scene.Children.Contains(item))
			{
				// This is the original do() case. The selection group exists in the scene and must be flattened into a new group
				var flattenedGroup = new Object3D
				{
					ItemType = Object3DTypes.Group
				};

				item.CollapseInto(flattenedGroup.Children, Object3DTypes.SelectionGroup);

				view3DWidget.Scene.ModifyChildren(children =>
				{
					children.Remove(item);
					children.Add(flattenedGroup);
				});

				// Update the local reference after flattening to make the redo pattern work
				item = flattenedGroup;

				view3DWidget.Scene.Select(flattenedGroup);
			}
			else
			{
				// This the undo -> redo() case. The original Selection group has been collapsed and we need to rebuild it
				view3DWidget.Scene.ModifyChildren(children =>
				{
					// Remove all children from the scene
					foreach (var child in item.Children)
					{
						children.Remove(child);
					}

					// Add the item
					children.Add(item);
				});

				view3DWidget.Scene.Select(item);
			}

			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var holes = item.Children.Where(obj => obj.OutputType == PrintOutputTypes.Hole).ToList();
				if (holes.Any())
				{
					var itemsToReplace = new List<(IObject3D object3D, Mesh newMesh)>();
					foreach (var hole in holes)
					{
						var transformedHole = Mesh.Copy(hole.Mesh, CancellationToken.None);
						transformedHole.Transform(hole.Matrix);

						var stuffToModify = item.Children.Where(obj => obj.OutputType != PrintOutputTypes.Hole && obj.Mesh != null).ToList();
						foreach (var object3D in stuffToModify)
						{
							var transformedObject = Mesh.Copy(object3D.Mesh, CancellationToken.None);
							transformedObject.Transform(object3D.Matrix);

							var newMesh = PolygonMesh.Csg.CsgOperations.Subtract(transformedObject, transformedHole);
							if (newMesh != object3D.Mesh)
							{
								itemsToReplace.Add((object3D, newMesh));
							}
						}

						foreach (var x in itemsToReplace)
						{
							item.Children.Remove(x.object3D);

							var newItem = new Object3D()
							{
								Mesh = x.newMesh,

								// Copy over child properties...
								OutputType = x.object3D.OutputType,
								Color = x.object3D.Color,
								MaterialIndex = x.object3D.MaterialIndex
							};
							newItem.Children.Add(x.object3D);

							item.Children.Add(newItem);
						}
					}
				}
			});

			view3DWidget.PartHasBeenChanged();
		}

		public void Undo()
		{
			if (!view3DWidget.Scene.Children.Contains(item))
			{
				return;
			}

			view3DWidget.Scene.ModifyChildren(children =>
			{
				// Remove the group
				children.Remove(item);

				// Add all children from the group
				children.AddRange(item.Children);
			});

			view3DWidget.Scene.SelectLastChild();

			view3DWidget.PartHasBeenChanged();
		}
	}
}