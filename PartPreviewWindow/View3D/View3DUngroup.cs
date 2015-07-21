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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void UngroupSelected()
		{
			if (SelectedMeshGroupIndex == -1)
			{
				SelectedMeshGroupIndex = 0;
			}
			string makingCopyLabel = LocalizedString.Get("Ungrouping");
			string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
			processingProgressControl.ProcessType = makingCopyLabelFull;

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

			int indexBeingReplaced = SelectedMeshGroupIndex;
			List<Mesh> discreetMeshes = new List<Mesh>();
			asynchMeshGroups[indexBeingReplaced].Transform(asynchMeshGroupTransforms[indexBeingReplaced].TotalTransform);
			// if there are multiple meshes than just make them separate groups
			if (asynchMeshGroups[indexBeingReplaced].Meshes.Count > 1)
			{
				foreach (Mesh mesh in asynchMeshGroups[indexBeingReplaced].Meshes)
				{
					discreetMeshes.Add(mesh);
				}
			}
			else // actually try and cut up the mesh into separate parts
			{
				discreetMeshes = CreateDiscreteMeshes.SplitConnectedIntoMeshes(asynchMeshGroups[indexBeingReplaced], (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					ReportProgressChanged(progress0To1 * .5, processingState, out continueProcessing);
				});
			}

			asynchMeshGroups.RemoveAt(indexBeingReplaced);
			asynchPlatingDatas.RemoveAt(indexBeingReplaced);
			asynchMeshGroupTransforms.RemoveAt(indexBeingReplaced);
			double ratioPerDiscreetMesh = 1.0 / discreetMeshes.Count;
			double currentRatioDone = 0;
			for (int discreetMeshIndex = 0; discreetMeshIndex < discreetMeshes.Count; discreetMeshIndex++)
			{
				PlatingMeshGroupData newInfo = new PlatingMeshGroupData();
				asynchPlatingDatas.Add(newInfo);
				asynchMeshGroups.Add(new MeshGroup(discreetMeshes[discreetMeshIndex]));
				int addedMeshIndex = asynchMeshGroups.Count - 1;
				MeshGroup addedMeshGroup = asynchMeshGroups[addedMeshIndex];

				ScaleRotateTranslate transform = ScaleRotateTranslate.Identity();
				transform.SetCenteringForMeshGroup(addedMeshGroup);
				asynchMeshGroupTransforms.Add(transform);

				//PlatingHelper.PlaceMeshGroupOnBed(asynchMeshGroups, asynchMeshGroupTransforms, addedMeshIndex, false);

				// and create selection info
				PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, addedMeshIndex, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					ReportProgressChanged(.5 + progress0To1 * .5 * currentRatioDone, processingState, out continueProcessing);
				});
				currentRatioDone += ratioPerDiscreetMesh;
			}
		}

		private async void UngroupSelectedMeshGroup()
		{
			if (MeshGroups.Count > 0)
			{
				processingProgressControl.PercentComplete = 0;
				processingProgressControl.Visible = true;
				LockEditControls();
				viewIsInEditModePreLock = true;

				await Task.Run(() => UngroupSelected());

				if (WidgetHasBeenClosed)
				{
					return;
				}

				// remove the original mesh and replace it with these new meshes
				PullMeshGroupDataFromAsynchLists();

				// our selection changed to the mesh we just added which is at the end
				SelectedMeshGroupIndex = MeshGroups.Count - 1;

				UnlockEditControls();

				PartHasBeenChanged();

				Invalidate();
			}
		}
	}
}