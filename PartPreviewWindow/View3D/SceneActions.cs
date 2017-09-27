/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class SceneActions
	{
		public static async void UngroupSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasSelection)
			{
				view3DWidget.StartProgress("Ungroup");

				view3DWidget.viewIsInEditModePreLock = true;

				await Task.Run(() =>
				{
					var selectedItem = Scene.SelectedItem;
					bool isGroupItemType = Scene.HasSelection && selectedItem.Children.Count > 0;

					// If not a Group ItemType, look for mesh volumes and split into distinct objects if found
					if (!isGroupItemType 
						&& !selectedItem.HasChildren()
						&& selectedItem.Mesh != null)
					{
						var discreetMeshes = CreateDiscreteMeshes.SplitVolumesIntoMeshes(Scene.SelectedItem.Mesh, CancellationToken.None, (double progress0To1, string processingState) =>
						{
							view3DWidget.ReportProgressChanged(progress0To1 * .5, processingState);
						});

						if (discreetMeshes.Count == 1)
						{
							// No further processing needed, nothing to ungroup
							return;
						}

						selectedItem.Children.Modify(list =>
						{
							list.Clear();
							list.AddRange(
								discreetMeshes.Select(mesh => new Object3D()
								{
									ItemType = Object3DTypes.Model,
									Mesh = mesh
								}));
						});

						selectedItem.Mesh = null;
						selectedItem.MeshPath = null;
						selectedItem.ItemType = Object3DTypes.Group;

						isGroupItemType = true;
					}

					if (isGroupItemType)
					{
						// Create and perform the delete operation
						var operation = new UngroupCommand(view3DWidget, Scene, Scene.SelectedItem);
						operation.Do();

						// Store the operation for undo/redo
						Scene.UndoBuffer.Add(operation);
					}
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				// our selection changed to the mesh we just added which is at the end
				Scene.SelectLastChild();

				view3DWidget.EndProgress();
			}
		}

		public static async void GroupSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasChildren())
			{
				view3DWidget.StartProgress("Group Selection");
				view3DWidget.viewIsInEditModePreLock = true;

				var item = Scene.SelectedItem;

				await Task.Run(() =>
				{
					if (Scene.IsSelected(Object3DTypes.SelectionGroup))
					{
						// Create and perform the delete operation
						var operation = new GroupCommand(Scene, Scene.SelectedItem);

						// Store the operation for undo/redo
						Scene.UndoBuffer.AddAndDo(operation);
					}
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				view3DWidget.EndProgress();
			}
		}

		public static async void AutoArrangeChildren(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			await Task.Run(() =>
			{
				PlatingHelper.ArrangeOnBed(Scene.Children.ToList(), Scene, view3DWidget.BedCenter);
			});
		}

		public static async void DuplicateSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasSelection)
			{
				view3DWidget.StartProgress("Making Copy".Localize() + ":");

				// Copy selected item
				IObject3D newItem = await Task.Run(() =>
				{
					// new item can be null by the time this task kicks off
					var clonedItem = Scene.SelectedItem?.Clone();
					PlatingHelper.MoveToOpenPosition(clonedItem, Scene.Children);

					return clonedItem;
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				// it might come back null due to threading
				if (newItem != null)
				{
					Scene.InsertNewItem(view3DWidget, newItem);
				}

				view3DWidget.EndProgress();
			}
		}

		public static void InsertNewItem(this InteractiveScene Scene, View3DWidget view3DWidget, IObject3D newItem)
		{
			// Reposition first item to bed center
			if (Scene.Children.Count == 0)
			{
				var printer = ApplicationController.Instance.ActivePrinter;
				var aabb = newItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				var center = aabb.Center;

				newItem.Matrix *= Matrix4X4.CreateTranslation(
					(printer.Bed.BedCenter.x + center.x),
					(printer.Bed.BedCenter.y + center.y),
					 -aabb.minXYZ.z);
			}

			// Create and perform a new insert operation
			var insertOperation = new InsertCommand(view3DWidget, Scene, newItem);
			insertOperation.Do();

			// Store the operation for undo/redo
			Scene.UndoBuffer.Add(insertOperation);
		}

		public static void DeleteSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasSelection)
			{
				// Create and perform the delete operation 
				var deleteOperation = new DeleteCommand(view3DWidget, Scene, Scene.SelectedItem);

				// Store the operation for undo/redo
				Scene.UndoBuffer.AddAndDo(deleteOperation);

				Scene.ClearSelection();
			}
		}

		internal class ArangeUndoCommand : IUndoRedoCommand
		{
			private List<TransformUndoCommand> allUndoTransforms = new List<TransformUndoCommand>();

			public ArangeUndoCommand(View3DWidget view3DWidget, List<Matrix4X4> preArrangeTarnsforms, List<Matrix4X4> postArrangeTarnsforms)
			{
				for (int i = 0; i < preArrangeTarnsforms.Count; i++)
				{
					//allUndoTransforms.Add(new TransformUndoCommand(view3DWidget, i, preArrangeTarnsforms[i], postArrangeTarnsforms[i]));
				}
			}

			public void Do()
			{
				for (int i = 0; i < allUndoTransforms.Count; i++)
				{
					allUndoTransforms[i].Do();
				}
			}

			public void Undo()
			{
				for (int i = 0; i < allUndoTransforms.Count; i++)
				{
					allUndoTransforms[i].Undo();
				}
			}
		}
	}
}
