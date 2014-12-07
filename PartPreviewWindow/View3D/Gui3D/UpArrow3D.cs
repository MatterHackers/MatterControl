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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class UpArrow3D : InteractionVolume
    {
        Mesh upArrow;
        double zHitHeight;
        Vector3 lastMoveDelta;
        PlaneShape hitPlane;

        public UpArrow3D(MeshViewerWidget meshViewerToDrawWith)
            : base(null, meshViewerToDrawWith)
        {
            string arrowFile = Path.Combine("Icons", "3D Icons", "up_pointer.stl");
            if (StaticData.Instance.FileExists(arrowFile))
            {
                using(Stream arrowStream = StaticData.Instance.OpenSteam(arrowFile))
                { 
                    List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(arrowStream, Path.GetExtension(arrowFile));
                    upArrow = loadedMeshGroups[0].Meshes[0];

                    CollisionVolume = PlatingHelper.CreateTraceDataForMesh(upArrow);
                    AxisAlignedBoundingBox arrowBounds = upArrow.GetAxisAlignedBoundingBox();
                    //CollisionVolume = new CylinderShape(arrowBounds.XSize / 2, arrowBounds.ZSize, new SolidMaterial(RGBA_Floats.Red, .5, 0, .4));
                    //CollisionVolume = new CylinderShape(arrowBounds.XSize / 2 * 4, arrowBounds.ZSize * 4, new SolidMaterial(RGBA_Floats.Red, .5, 0, .4));
                }
            }
        }

        public override void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
        {
            zHitHeight = mouseEvent3D.info.hitPosition.z;
            lastMoveDelta= new Vector3();
            hitPlane = new PlaneShape(mouseEvent3D.MouseRay.direction, mouseEvent3D.info.distanceToHit, null);

            //Matrix4X4 lookAtZ = Matrix4X4.LookAt(mouseEvent3D.MouseRay.origin, mouseEvent3D.info.hitPosition, Vector3.UnitZ);
            //Vector3 direction = Vector3.TransformNormal(mouseEvent3D.MouseRay.direction, lookAtZ);
            //hitPlane = new PlaneShape(direction, mouseEvent3D.info.distanceToHit, null);

            IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);
            zHitHeight = info.hitPosition.z;
            
            base.OnMouseDown(mouseEvent3D);
        }

        public override void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
        {
            IntersectInfo info = hitPlane.GetClosestIntersection(mouseEvent3D.MouseRay);

            if (info != null && MeshViewerToDrawWith.SelectedMeshGroupIndex != -1)
            {
                Vector3 delta = new Vector3(0, 0, info.hitPosition.z - zHitHeight);

                // move it back to where it started
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(new Vector3(-lastMoveDelta));
                // and move it from there to where we are now
                totalTransfrom *= Matrix4X4.CreateTranslation(new Vector3(delta));
                lastMoveDelta = delta;

                ScaleRotateTranslate translated = MeshViewerToDrawWith.SelectedMeshGroupTransform;
                translated.translation *= totalTransfrom;
                MeshViewerToDrawWith.SelectedMeshGroupTransform = translated;

                Invalidate();
            }

            base.OnMouseMove(mouseEvent3D);
        }

        public void  SetPosition()
        {
            Matrix4X4 selectedMeshTransform = MeshViewerToDrawWith.SelectedMeshGroupTransform.TotalTransform;
            AxisAlignedBoundingBox selectedBounds = MeshViewerToDrawWith.SelectedMeshGroup.GetAxisAlignedBoundingBox(selectedMeshTransform);
            Vector3 boundsCenter = selectedBounds.Center;
            Vector3 centerTop = new Vector3(boundsCenter.x, boundsCenter.y, selectedBounds.maxXYZ.z);

            Vector2 centerTopScreenPosition = MeshViewerToDrawWith.TrackballTumbleWidget.GetScreenPosition(centerTop);
            //centerTopScreenPosition = meshViewerToDrawWith.TransformToParentSpace(this, centerTopScreenPosition);

            double distBetweenPixelsWorldSpace = MeshViewerToDrawWith.TrackballTumbleWidget.GetWorldUnitsPerScreenPixelAtPosition(centerTop);

            Matrix4X4 arrowTransform = Matrix4X4.CreateTranslation(new Vector3(centerTop.x, centerTop.y, centerTop.z + 20 * distBetweenPixelsWorldSpace));
            arrowTransform = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * arrowTransform;

            TotalTransform = arrowTransform;
        }

        public override void DrawGlContent(EventArgs e)
        {
            if (MeshViewerToDrawWith.SelectedMeshGroup != null)
            {
                if (MouseOver)
                {
                    RenderMeshToGl.Render(upArrow, RGBA_Bytes.Red, TotalTransform, RenderTypes.Shaded);
                }
                else
                {
                    RenderMeshToGl.Render(upArrow, RGBA_Bytes.Black, TotalTransform, RenderTypes.Shaded);
                }
            }

            base.DrawGlContent(e);
        }
    }
}
