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
using MatterHackers.PolygonMesh;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void CopyGroup()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

			MeshGroup meshGroupToCopy = asyncMeshGroups[SelectedMeshGroupIndex];
			MeshGroup copyMeshGroup = new MeshGroup();
			double meshCount = meshGroupToCopy.Meshes.Count;
			for (int i = 0; i < meshCount; i++)
			{
				Mesh mesh = asyncMeshGroups[SelectedMeshGroupIndex].Meshes[i];
				copyMeshGroup.Meshes.Add(Mesh.Copy(mesh, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					ReportProgressChanged(progress0To1, processingState, out continueProcessing);
				}));
			}

			PlatingHelper.FindPositionForGroupAndAddToPlate(copyMeshGroup, SelectedMeshGroupTransform, asyncPlatingDatas, asyncMeshGroups, asyncMeshGroupTransforms);
			PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, asyncMeshGroups.Count - 1, null);

			bool continueProcessing2;
			ReportProgressChanged(.95, "", out continueProcessing2);
		}

		private async void MakeCopyOfGroup()
		{
			if (MeshGroups.Count > 0
				&& SelectedMeshGroupIndex != -1)
			{
				string makingCopyLabel = LocalizedString.Get("Making Copy");
				string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
				processingProgressControl.ProcessType = makingCopyLabelFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				await Task.Run((System.Action)CopyGroup);

				if (HasBeenClosed)
				{
					return;
				}

				UnlockEditControls();
				PullMeshGroupDataFromAsynchLists();
				PartHasBeenChanged();

				// now set the selection to the new copy
				SelectedMeshGroupIndex = MeshGroups.Count - 1;
				UndoBuffer.Add(new CopyUndoCommand(this, SelectedMeshGroupIndex));
			}
		}
	}
}