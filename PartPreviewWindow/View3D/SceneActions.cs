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

using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
				view3DWidget.processingProgressControl.PercentComplete = 0;
				view3DWidget.processingProgressControl.Visible = true;
				view3DWidget.LockEditControls();
				view3DWidget.viewIsInEditModePreLock = true;

				await Task.Run(() =>
				{
					var selectedItem = Scene.SelectedItem;
					bool isGroupItemType = Scene.IsSelected(Object3DTypes.Group);

					// If not a Group ItemType, look for mesh volumes and split into distinct objects if found
					if (!isGroupItemType 
						&& !selectedItem.HasChildren
						&& selectedItem.Mesh != null)
					{
						var discreetMeshes = CreateDiscreteMeshes.SplitVolumesIntoMeshes(Scene.SelectedItem.Mesh, (double progress0To1, string processingState, out bool continueProcessing) =>
						{
							view3DWidget.ReportProgressChanged(progress0To1 * .5, processingState, out continueProcessing);
						});

						if (discreetMeshes.Count == 1)
						{
							// No further processing needed, nothing to ungroup
							return;
						}

						selectedItem.Children = discreetMeshes.Select(mesh => new Object3D()
						{
							ItemType = Object3DTypes.Model,
							Mesh = mesh
						}).ToList<IObject3D>();

						selectedItem.Mesh = null;
						selectedItem.MeshPath = null;
						selectedItem.ItemType = Object3DTypes.Group;

						isGroupItemType = true;
					}

					if (isGroupItemType)
					{
						// Create and perform the delete operation
						var operation = new UngroupCommand(view3DWidget, Scene.SelectedItem);
						operation.Do();

						// Store the operation for undo/redo
						view3DWidget.UndoBuffer.Add(operation);
					}
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				// our selection changed to the mesh we just added which is at the end
				Scene.SelectLastChild();

				view3DWidget.UnlockEditControls();

				view3DWidget.PartHasBeenChanged();

				view3DWidget.Invalidate();
			}
		}

		public static async void AlignToSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasChildren)
			{
				// set the progress label text
				view3DWidget.processingProgressControl.PercentComplete = 0;
				view3DWidget.processingProgressControl.Visible = true;
				string makingCopyLabel = "Aligning".Localize();
				string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
				view3DWidget.processingProgressControl.ProcessType = makingCopyLabelFull;

				view3DWidget.LockEditControls();
				view3DWidget.viewIsInEditModePreLock = true;

				await Task.Run(() =>
				{
					if (Scene.HasSelection)
					{
						Scene.SelectFirstChild();
					}

					// make sure our thread translates numbers correctly (always do this in a thread)
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					// try to move all the not selected meshes relative to the selected mesh
					AxisAlignedBoundingBox selectedOriginalBounds = Scene.SelectedItem.Mesh.GetAxisAlignedBoundingBox();
					Vector3 selectedOriginalCenter = selectedOriginalBounds.Center;
					AxisAlignedBoundingBox selectedCurrentBounds = Scene.SelectedItem.Mesh.GetAxisAlignedBoundingBox(Scene.SelectedItem.Matrix);
					Vector3 selctedCurrentCenter = selectedCurrentBounds.Center;
					for (int meshGroupToMoveIndex = 0; meshGroupToMoveIndex < Scene.Children.Count; meshGroupToMoveIndex++)
					{
						IObject3D item = Scene.Children[meshGroupToMoveIndex];
						if (item != Scene.SelectedItem)
						{
							AxisAlignedBoundingBox groupToMoveOriginalBounds = item.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
							Vector3 groupToMoveOriginalCenter = groupToMoveOriginalBounds.Center;
							AxisAlignedBoundingBox groupToMoveBounds = item.GetAxisAlignedBoundingBox(Scene.Children[meshGroupToMoveIndex].Matrix);
							Vector3 groupToMoveCenter = groupToMoveBounds.Center;

							Vector3 originalCoordinatesDelta = groupToMoveOriginalCenter - selectedOriginalCenter;
							Vector3 currentCoordinatesDelta = groupToMoveCenter - selctedCurrentCenter;

							Vector3 deltaRequired = originalCoordinatesDelta - currentCoordinatesDelta;

							if (deltaRequired.Length > .0001)
							{
								Scene.Children[meshGroupToMoveIndex].Matrix *= Matrix4X4.CreateTranslation(deltaRequired);
								view3DWidget.PartHasBeenChanged();
							}
						}
					}

					/* TODO: Align needs reconsidered
					// now put all the meshes into just one group
					IObject3D itemWeAreKeeping = Scene.SelectedItem;
					for (int meshGroupToMoveIndex = Scene.Children.Count - 1; meshGroupToMoveIndex >= 0; meshGroupToMoveIndex--)
					{
						IObject3D itemToMove = Scene.Children[meshGroupToMoveIndex];
						if (itemToMove != itemWeAreKeeping)
						{
							// move all the meshes into the new aligned mesh group
							for (int moveIndex = 0; moveIndex < itemToMove.Meshes.Count; moveIndex++)
							{
								Mesh mesh = itemToMove.Meshes[moveIndex];
								itemWeAreKeeping.Meshes.Add(mesh);
							}

							Scene.Children.RemoveAt(meshGroupToMoveIndex);

							// TODO: ******************** !!!!!!!!!!!!!!! ********************
							//asyncMeshGroupTransforms.RemoveAt(meshGroupToMoveIndex);
						}
					}
					*/

					// TODO: ******************** !!!!!!!!!!!!!!! ********************
					/*
					double ratioPerMeshGroup = 1.0 / MeshGroups.Count;
					double currentRatioDone = 0;
					for (int i = 0; i < MeshGroups.Count; i++)
					{
						// create the selection info
						PlatingHelper.CreateITraceableForMeshGroup(MeshGroups, i, (double progress0To1, string processingState, out bool continueProcessing) =>
						{
							ReportProgressChanged(progress0To1, processingState, out continueProcessing);
						});

						currentRatioDone += ratioPerMeshGroup;
					} */
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				// our selection changed to the mesh we just added which is at the end
				Scene.SelectLastChild();

				view3DWidget.UnlockEditControls();

				view3DWidget.Invalidate();
			}
		}

		public static async void GroupSelection(this InteractiveScene Scene, View3DWidget view3DWidget)
		{
			if (Scene.HasChildren)
			{
				view3DWidget.processingProgressControl.PercentComplete = 0;
				view3DWidget.processingProgressControl.Visible = true;
				view3DWidget.LockEditControls();
				view3DWidget.viewIsInEditModePreLock = true;

				var item = Scene.SelectedItem;

				await Task.Run(() =>
				{
					if (Scene.IsSelected(Object3DTypes.SelectionGroup))
					{
						// Create and perform the delete operation
						var operation = new GroupCommand(view3DWidget, Scene.SelectedItem);
						operation.Do();

						// Store the operation for undo/redo
						view3DWidget.UndoBuffer.Add(operation);
					}
				});

				if (view3DWidget.HasBeenClosed)
				{
					return;
				}

				view3DWidget.UnlockEditControls();

				view3DWidget.Invalidate();
			}
		}

	}
}