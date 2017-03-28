﻿/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.DataConverters3D;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private async void UngroupSelectedMeshGroup()
		{
			if (Scene.HasSelection)
			{
				processingProgressControl.PercentComplete = 0;
				processingProgressControl.Visible = true;
				LockEditControls();
				viewIsInEditModePreLock = true;

				await Task.Run(() =>
				{
					var selectedItem = Scene.SelectedItem;
					bool isGroupItemType = Scene.IsSelected(Object3DTypes.Group);

					// If not a Group ItemType, look for mesh volumes and split into disctinct objects if found
					if (!isGroupItemType 
						&& !selectedItem.HasChildren
						&& selectedItem.Mesh != null)
					{
						var discreetMeshes = CreateDiscreteMeshes.SplitVolumesIntoMeshes(Scene.SelectedItem.Mesh, (double progress0To1, string processingState, out bool continueProcessing) =>
						{
							ReportProgressChanged(progress0To1 * .5, processingState, out continueProcessing);
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
						var operation = new UngroupCommand(this, Scene.SelectedItem);
						operation.Do();

						// Store the operation for undo/redo
						UndoBuffer.Add(operation);
					}
				});

				if (HasBeenClosed)
				{
					return;
				}

				// our selection changed to the mesh we just added which is at the end
				Scene.SelectLastChild();

				UnlockEditControls();

				PartHasBeenChanged();

				Invalidate();
			}
		}
	}
}