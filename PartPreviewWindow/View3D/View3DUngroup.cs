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

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public partial class View3DTransformPart
    {
        void UngroupSelectedMeshGroup()
        {
            if (MeshGroups.Count > 0)
            {
                processingProgressControl.PercentComplete = 0;
                processingProgressControl.Visible = true;
                LockEditControls();
                viewIsInEditModePreLock = true;

                BackgroundWorker createDiscreteMeshesBackgroundWorker = null;
                createDiscreteMeshesBackgroundWorker = new BackgroundWorker();
                createDiscreteMeshesBackgroundWorker.WorkerReportsProgress = true;

                createDiscreteMeshesBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                createDiscreteMeshesBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ungroupSelectedBackgroundWorker_RunWorkerCompleted);
                createDiscreteMeshesBackgroundWorker.DoWork += new DoWorkEventHandler(ungroupSelectedBackgroundWorker_DoWork);

                createDiscreteMeshesBackgroundWorker.RunWorkerAsync();
            }
        }

        void ungroupSelectedBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string makingCopyLabel = LocalizedString.Get("Finding Meshes");
            string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
            processingProgressControl.textWidget.Text = makingCopyLabelFull;

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            PushMeshGroupDataToAsynchLists(TranceInfoOpperation.DO_COPY);

            int indexBeingReplaced = MeshGroups.IndexOf(SelectedMeshGroup);
            asynchMeshGroups[indexBeingReplaced].Transform(asynchMeshGroupTransforms[indexBeingReplaced].TotalTransform);
            List<Mesh> discreetMeshes = CreateDiscreteMeshes.SplitConnectedIntoMeshes(asynchMeshGroups[indexBeingReplaced], (double progress0To1, string processingState) =>
            {
                int nextPercent = (int)(progress0To1 * 50);
                backgroundWorker.ReportProgress(nextPercent);
                return true;
            });

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

                PlatingHelper.PlaceMeshGroupOnBed(asynchMeshGroups, asynchMeshGroupTransforms, addedMeshIndex, false);

                // and create selection info
                PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, addedMeshIndex, (double progress0To1, string processingState) =>
                {
                    int nextPercent = (int)((currentRatioDone + ratioPerDiscreetMesh * progress0To1) * 50) + 50;
                    backgroundWorker.ReportProgress(nextPercent);
                    return true;
                });
                currentRatioDone += ratioPerDiscreetMesh;
            }
        }

        void ungroupSelectedBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
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
