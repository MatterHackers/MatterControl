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

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public partial class View3DWidget
    {
        void AlignToSelectedMeshGroup()
        {
            if (MeshGroups.Count > 0)
            {
                // set the progress lable text
                processingProgressControl.PercentComplete = 0;
                processingProgressControl.Visible = true;
                string makingCopyLabel = LocalizedString.Get("Aligning");
                string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
                processingProgressControl.ProcessType = makingCopyLabelFull;
                
                LockEditControls();
                viewIsInEditModePreLock = true;

                BackgroundWorker createDiscreteMeshesBackgroundWorker = null;
                createDiscreteMeshesBackgroundWorker = new BackgroundWorker();

                createDiscreteMeshesBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(alignSelectedBackgroundWorker_RunWorkerCompleted);
                createDiscreteMeshesBackgroundWorker.DoWork += new DoWorkEventHandler(alignSelectedBackgroundWorker_DoWork);

                createDiscreteMeshesBackgroundWorker.RunWorkerAsync();
            }
        }

        void alignSelectedBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (SelectedMeshGroupIndex == -1)
            {
                SelectedMeshGroupIndex = 0;
            }
            // make sure our thread traslates numbmers correctly (always do this in a thread)
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            // save our data so we don't mess up the display while doing work
            PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

            // try to move all the not selected meshes relative to the selected mesh
            AxisAlignedBoundingBox selectedOriginalBounds = asynchMeshGroups[SelectedMeshGroupIndex].GetAxisAlignedBoundingBox();
            Vector3 selectedOriginalCenter = selectedOriginalBounds.Center;
            AxisAlignedBoundingBox selectedCurrentBounds = asynchMeshGroups[SelectedMeshGroupIndex].GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[SelectedMeshGroupIndex].TotalTransform);
            Vector3 selctedCurrentCenter = selectedCurrentBounds.Center;
            for(int meshGroupToMoveIndex = 0; meshGroupToMoveIndex < asynchMeshGroups.Count; meshGroupToMoveIndex++)
            {
                MeshGroup meshGroupToMove = asynchMeshGroups[meshGroupToMoveIndex];
                if (meshGroupToMove != asynchMeshGroups[SelectedMeshGroupIndex])
                {
                    AxisAlignedBoundingBox groupToMoveOriginalBounds = meshGroupToMove.GetAxisAlignedBoundingBox();
                    Vector3 groupToMoveOriginalCenter = groupToMoveOriginalBounds.Center;
                    AxisAlignedBoundingBox groupToMoveBounds = meshGroupToMove.GetAxisAlignedBoundingBox(asynchMeshGroupTransforms[meshGroupToMoveIndex].TotalTransform);
                    Vector3 groupToMoveCenter = groupToMoveBounds.Center;

                    Vector3 originalCoordinatesDelta = groupToMoveOriginalCenter - selectedOriginalCenter;
                    Vector3 currentCoordinatesDelta = groupToMoveCenter - selctedCurrentCenter;

                    Vector3 deltaRequired = originalCoordinatesDelta - currentCoordinatesDelta;

                    if (deltaRequired.Length > .0001)
                    {
                        ScaleRotateTranslate translated = asynchMeshGroupTransforms[meshGroupToMoveIndex];
                        translated.translation *= Matrix4X4.CreateTranslation(deltaRequired);
                        asynchMeshGroupTransforms[meshGroupToMoveIndex] = translated;
                        PartHasBeenChanged();
                    }
                }
            }

            // now put all the meshes into just one group
            MeshGroup meshGroupWeAreKeeping = asynchMeshGroups[SelectedMeshGroupIndex];
            for (int meshGroupToMoveIndex = asynchMeshGroups.Count - 1; meshGroupToMoveIndex >= 0; meshGroupToMoveIndex--)
            {
                MeshGroup meshGroupToMove = asynchMeshGroups[meshGroupToMoveIndex];
                if (meshGroupToMove != meshGroupWeAreKeeping)
                {
                    // move all the meshes into the new aligned mesh group
                    for(int moveIndex = 0; moveIndex < meshGroupToMove.Meshes.Count; moveIndex++)
                    {
                        Mesh mesh = meshGroupToMove.Meshes[moveIndex];
                        meshGroupWeAreKeeping.Meshes.Add(mesh);
                    }

                    asynchMeshGroups.RemoveAt(meshGroupToMoveIndex);
                    asynchMeshGroupTransforms.RemoveAt(meshGroupToMoveIndex);
                }
            }

            asynchPlatingDatas.Clear();
            double ratioPerMeshGroup = 1.0 / asynchMeshGroups.Count;
            double currentRatioDone = 0;
            for (int i = 0; i < asynchMeshGroups.Count; i++)
            {
                PlatingMeshGroupData newInfo = new PlatingMeshGroupData();
                asynchPlatingDatas.Add(newInfo);

                MeshGroup meshGroup = asynchMeshGroups[i];

                // create the selection info
                PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, i, (double progress0To1, string processingState, out bool continueProcessing) =>
                {
                    BackgroundWorker_ProgressChanged(progress0To1, processingState, out continueProcessing);
                });

                currentRatioDone += ratioPerMeshGroup;
            }
        }

        void alignSelectedBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (WidgetHasBeenClosed)
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
