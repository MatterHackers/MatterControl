/*
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
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void AlignSelected()
		{
			if(Scene.HasSelection)
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
						PartHasBeenChanged();
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
		}

		private async void AlignToSelectedMeshGroup()
		{
			if (Scene.HasChildren)
			{
				// set the progress label text
				processingProgressControl.PercentComplete = 0;
				processingProgressControl.Visible = true;
				string makingCopyLabel = "Aligning".Localize();
				string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
				processingProgressControl.ProcessType = makingCopyLabelFull;

				LockEditControls();
				viewIsInEditModePreLock = true;

				await Task.Run((System.Action)AlignSelected);

				if (HasBeenClosed)
				{
					return;
				}

				// our selection changed to the mesh we just added which is at the end
				Scene.SelectLastChild();

				UnlockEditControls();

				Invalidate();
			}
		}
	}
}