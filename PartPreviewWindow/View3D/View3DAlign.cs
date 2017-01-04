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

using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void AlignSelected()
		{
			if (SelectedMeshGroupIndex == -1)
			{
				SelectedMeshGroupIndex = 0;
			}
			// make sure our thread translates numbers correctly (always do this in a thread)
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// save our data so we don't mess up the display while doing work
			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

			// try to move all the not selected meshes relative to the selected mesh
			AxisAlignedBoundingBox selectedOriginalBounds = asyncMeshGroups[SelectedMeshGroupIndex].GetAxisAlignedBoundingBox();
			Vector3 selectedOriginalCenter = selectedOriginalBounds.Center;
			AxisAlignedBoundingBox selectedCurrentBounds = asyncMeshGroups[SelectedMeshGroupIndex].GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[SelectedMeshGroupIndex]);
			Vector3 selctedCurrentCenter = selectedCurrentBounds.Center;
			for (int meshGroupToMoveIndex = 0; meshGroupToMoveIndex < asyncMeshGroups.Count; meshGroupToMoveIndex++)
			{
				MeshGroup meshGroupToMove = asyncMeshGroups[meshGroupToMoveIndex];
				if (meshGroupToMove != asyncMeshGroups[SelectedMeshGroupIndex])
				{
					AxisAlignedBoundingBox groupToMoveOriginalBounds = meshGroupToMove.GetAxisAlignedBoundingBox();
					Vector3 groupToMoveOriginalCenter = groupToMoveOriginalBounds.Center;
					AxisAlignedBoundingBox groupToMoveBounds = meshGroupToMove.GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[meshGroupToMoveIndex]);
					Vector3 groupToMoveCenter = groupToMoveBounds.Center;

					Vector3 originalCoordinatesDelta = groupToMoveOriginalCenter - selectedOriginalCenter;
					Vector3 currentCoordinatesDelta = groupToMoveCenter - selctedCurrentCenter;

					Vector3 deltaRequired = originalCoordinatesDelta - currentCoordinatesDelta;

					if (deltaRequired.Length > .0001)
					{
						asyncMeshGroupTransforms[meshGroupToMoveIndex] *= Matrix4X4.CreateTranslation(deltaRequired);
						PartHasBeenChanged();
					}
				}
			}

			// now put all the meshes into just one group
			MeshGroup meshGroupWeAreKeeping = asyncMeshGroups[SelectedMeshGroupIndex];
			for (int meshGroupToMoveIndex = asyncMeshGroups.Count - 1; meshGroupToMoveIndex >= 0; meshGroupToMoveIndex--)
			{
				MeshGroup meshGroupToMove = asyncMeshGroups[meshGroupToMoveIndex];
				if (meshGroupToMove != meshGroupWeAreKeeping)
				{
					// move all the meshes into the new aligned mesh group
					for (int moveIndex = 0; moveIndex < meshGroupToMove.Meshes.Count; moveIndex++)
					{
						Mesh mesh = meshGroupToMove.Meshes[moveIndex];
						meshGroupWeAreKeeping.Meshes.Add(mesh);
					}

					asyncMeshGroups.RemoveAt(meshGroupToMoveIndex);
					asyncMeshGroupTransforms.RemoveAt(meshGroupToMoveIndex);
				}
			}

			asyncPlatingDatas.Clear();
			double ratioPerMeshGroup = 1.0 / asyncMeshGroups.Count;
			double currentRatioDone = 0;
			for (int i = 0; i < asyncMeshGroups.Count; i++)
			{
				PlatingMeshGroupData newInfo = new PlatingMeshGroupData();
				asyncPlatingDatas.Add(newInfo);

				MeshGroup meshGroup = asyncMeshGroups[i];

				// create the selection info
				PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, i, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					ReportProgressChanged(progress0To1, processingState, out continueProcessing);
				});

				currentRatioDone += ratioPerMeshGroup;
			}
		}

		private async void AlignToSelectedMeshGroup()
		{
			if (MeshGroups.Count > 0)
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

				// remove the original mesh and replace it with these new meshes
				PullMeshGroupDataFromAsynchLists();

				// our selection changed to the mesh we just added which is at the end
				SelectedMeshGroupIndex = MeshGroups.Count - 1;

				UnlockEditControls();

				Invalidate();
			}
		}
	}
}