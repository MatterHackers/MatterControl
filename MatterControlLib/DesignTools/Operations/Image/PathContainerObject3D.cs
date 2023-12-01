/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.DesignTools
{
    public abstract class PathContainerObject3D : Object3D, IEditorDraw, IPrimaryOperationsSpecifier, IPathObject3D
    {
        public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
        {
            this.DrawPath();
        }

        public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
        {
            return this.GetWorldspaceAabbOfDrawPath();
        }

        public VertexStorage VertexStorage { get; set; }

        public virtual IVertexSource GetVertexSource()
        {
            return VertexStorage;
        }

        public abstract bool MeshIsSolidObject { get; }

        public static IEnumerable<SceneOperation> GetOperations(Type type)
        {
            // path Ids
            var pathIds = new List<string>(new string[] {
                "LinearExtrude",
                "Revolve",
                "InflatePath",
                "OutlinePath",
                "SmoothPath"
            });

            foreach (var pathId in pathIds)
            {
                yield return SceneOperations.ById(pathId);
            }
        }

        public IEnumerable<SceneOperation> GetOperations()
        {
            return GetOperations(this.GetType());
        }
    }

    /// <summary>
    /// This is a class that is specifically holding a path and the mesh is a visualization of the path
    /// </summary>
    public class PathObject3D : PathContainerObject3D
    {
        // Report that the Mesh is a visual representation of the Path and not a solid object
        public override bool MeshIsSolidObject => false;
    }
}