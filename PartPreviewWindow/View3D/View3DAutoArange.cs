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
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void AutoArrangePartsInBackground()
		{
			if (MeshGroups.Count > 0)
			{
				string progressArrangeParts = LocalizedString.Get("Arranging Parts");
				string progressArrangePartsFull = string.Format("{0}:", progressArrangeParts);
				processingProgressControl.ProcessType = progressArrangePartsFull;
				processingProgressControl.Visible = true;
				processingProgressControl.PercentComplete = 0;
				LockEditControls();

				BackgroundWorker arrangeMeshGroupsBackgroundWorker = new BackgroundWorker();

				arrangeMeshGroupsBackgroundWorker.DoWork += new DoWorkEventHandler(arrangeMeshGroupsBackgroundWorker_DoWork);
				arrangeMeshGroupsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(arrangeMeshGroupsBackgroundWorker_RunWorkerCompleted);

				arrangeMeshGroupsBackgroundWorker.RunWorkerAsync();
			}
		}

		private void arrangeMeshGroupsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DONT_COPY);

			BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

			// move them all out of the way
			for (int i = 0; i < asynchMeshGroups.Count; i++)
			{
				ScaleRotateTranslate translate = asynchMeshGroupTransforms[i];
				translate.translation *= Matrix4X4.CreateTranslation(1000, 1000, 0);
				asynchMeshGroupTransforms[i] = translate;
			}

			// sort them by size
			for (int i = 0; i < asynchMeshGroups.Count; i++)
			{
				AxisAlignedBoundingBox iAABB = asynchMeshGroups[i].GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[i].TotalTransform);
				for (int j = i + 1; j < asynchMeshGroups.Count; j++)
				{
					AxisAlignedBoundingBox jAABB = asynchMeshGroups[j].GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[j].TotalTransform);
					if (Math.Max(iAABB.XSize, iAABB.YSize) < Math.Max(jAABB.XSize, jAABB.YSize))
					{
						PlatingMeshGroupData tempData = asynchPlatingDatas[i];
						asynchPlatingDatas[i] = asynchPlatingDatas[j];
						asynchPlatingDatas[j] = tempData;

						MeshGroup tempMeshGroup = asynchMeshGroups[i];
						asynchMeshGroups[i] = asynchMeshGroups[j];
						asynchMeshGroups[j] = tempMeshGroup;

						ScaleRotateTranslate iTransform = asynchMeshGroupTransforms[i];
						ScaleRotateTranslate jTransform = asynchMeshGroupTransforms[j];
						Matrix4X4 tempTransform = iTransform.translation;
						iTransform.translation = jTransform.translation;
						jTransform.translation = tempTransform;

						asynchMeshGroupTransforms[i] = jTransform;
						asynchMeshGroupTransforms[j] = iTransform;

						iAABB = jAABB;
					}
				}
			}

			double ratioPerMeshGroup = 1.0 / asynchMeshGroups.Count;
			double currentRatioDone = 0;
			// put them onto the plate (try the center) starting with the biggest and moving down
			for (int meshGroupIndex = 0; meshGroupIndex < asynchMeshGroups.Count; meshGroupIndex++)
			{
				MeshGroup meshGroup = asynchMeshGroups[meshGroupIndex];
				Vector3 meshCenter = meshGroup.GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[meshGroupIndex].translation).Center;
				ScaleRotateTranslate atZero = asynchMeshGroupTransforms[meshGroupIndex];
				atZero.translation = Matrix4X4.Identity;
				asynchMeshGroupTransforms[meshGroupIndex] = atZero;
				PlatingHelper.MoveMeshGroupToOpenPosition(meshGroupIndex, asynchPlatingDatas, asynchMeshGroups, asynchMeshGroupTransforms);

				// and create the trace info so we can select it
				PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, meshGroupIndex, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					BackgroundWorker_ProgressChanged(progress0To1, processingState, out continueProcessing);
				});

				currentRatioDone += ratioPerMeshGroup;

				// and put it on the bed
				PlatingHelper.PlaceMeshGroupOnBed(asynchMeshGroups, asynchMeshGroupTransforms, meshGroupIndex);
			}

			// and finally center whatever we have as a group
			{
				AxisAlignedBoundingBox bounds = asynchMeshGroups[0].GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[0].TotalTransform);
				for (int i = 1; i < asynchMeshGroups.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, asynchMeshGroups[i].GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[i].TotalTransform));
				}

				Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
				for (int i = 0; i < asynchMeshGroups.Count; i++)
				{
					ScaleRotateTranslate translate = asynchMeshGroupTransforms[i];
					translate.translation *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
					asynchMeshGroupTransforms[i] = translate;
				}
			}
		}

		private void arrangeMeshGroupsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (WidgetHasBeenClosed)
			{
				return;
			}
			UnlockEditControls();
			PartHasBeenChanged();

			PullMeshGroupDataFromAsynchLists();
		}
	}
}