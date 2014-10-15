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

using System.ComponentModel;
using System.Globalization;
using System.Threading;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public partial class View3DTransformPart
    {
        private void MakeCopyOfGroup()
        {
            if (MeshGroups.Count > 0)
            {
                string makingCopyLabel = LocalizedString.Get("Making Copy");
                string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
                processingProgressControl.textWidget.Text = makingCopyLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker copyGroupBackgroundWorker = null;
                copyGroupBackgroundWorker = new BackgroundWorker();
                copyGroupBackgroundWorker.WorkerReportsProgress = true;

                copyGroupBackgroundWorker.DoWork += new DoWorkEventHandler(copyGroupBackgroundWorker_DoWork);
                copyGroupBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                copyGroupBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(copyGroupBackgroundWorker_RunWorkerCompleted);

                copyGroupBackgroundWorker.RunWorkerAsync();
            }
        }

        void copyGroupBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            PushMeshGroupDataToAsynchLists(TranceInfoOpperation.DO_COPY);

            MeshGroup meshGroupToCopy = asynchMeshGroups[SelectedMeshGroupIndex];
            MeshGroup copyMeshGroup = new MeshGroup();
            double meshCount = meshGroupToCopy.Meshes.Count;
            for (int i = 0; i < meshCount; i++)
            {
                Mesh mesh = asynchMeshGroups[SelectedMeshGroupIndex].Meshes[i];
                copyMeshGroup.Meshes.Add(Mesh.Copy(mesh, (progress0To1, processingState) =>
                {
                    int nextPercent = (int)(100 * (progress0To1 * .8 * i / meshCount));
                    backgroundWorker.ReportProgress(nextPercent);
                    return true;
                }));
            }

            PlatingHelper.FindPositionForGroupAndAddToPlate(copyMeshGroup, SelectedMeshGroupTransform, asynchPlatingDatas, asynchMeshGroups, asynchMeshGroupTransforms);
            PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, asynchMeshGroups.Count - 1, null);

            backgroundWorker.ReportProgress(95);
        }

        void copyGroupBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (WidgetHasBeenClosed)
            {
                return;
            }

            UnlockEditControls();
            PullMeshGroupDataFromAsynchLists();
            saveButtons.Visible = true;

            // now set the selection to the new copy
            MeshGroupExtraData[MeshGroups.Count - 1].currentScale = MeshGroupExtraData[SelectedMeshGroupIndex].currentScale;
            SelectedMeshGroupIndex = MeshGroups.Count - 1;
        }
    }
}
