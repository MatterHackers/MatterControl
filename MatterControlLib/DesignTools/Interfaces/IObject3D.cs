/*
Copyright (c) 2024, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Matter_CAD_Lib.DesignTools.Interfaces
{
    [Flags]
    public enum InvalidateType
    {
        None = 0,
        Children = 1 << 0,
        Color = 1 << 1,
        Image = 1 << 2,
        Matrix = 1 << 4,
        Mesh = 1 << 5,
        Name = 1 << 6,
        OutputType = 1 << 7,
        Path = 1 << 8,
        Properties = 1 << 9,
        DisplayValues = 1 << 10,
        SheetUpdated = 1 << 11,
    }

    [Flags]
    public enum Object3DPropertyFlags
    {
        Matrix = 0x01,
        Color = 0x02,
        Name = 0x8,
        OutputType = 0x10,
        Visible = 0x20,
        All = 0xFF,
    }

    public enum PrintOutputTypes
    {
        Default,
        Solid,
        Hole,
    }

    public interface IObject3D : IAscendable<IObject3D>
    {
        event EventHandler<InvalidateArgs> Invalidated;

        [JsonIgnore]
        /// <summary>
        /// Allow this object to be drilled into and edited
        /// </summary>
        bool CanEdit { get; }

        [JsonIgnore]
        bool CanApply { get; }

        [JsonConverter(typeof(JsonIObject3DConverter))]
        AscendableSafeList<IObject3D> Children { get; set; }

        Color Color { get; set; }

        /// <summary>
        /// Describes the expanded state in the scene tree view
        /// </summary>
        bool Expanded { get; }

        string ID { get; set; }

        [JsonConverter(typeof(MatrixConverter))]
        Matrix4X4 Matrix { get; set; }

        /// <summary>
        /// The associated mesh for this content. Setting to a new value invalidates the MeshPath, TraceData and notifies all active listeners
        /// </summary>
        [JsonIgnore]
        Mesh Mesh { get; set; }

        string MeshPath { get; set; }

        string Name { get; set; }

        PrintOutputTypes OutputType { get; set; }

        string OwnerID { get; set; }

        /// <summary>
        /// Every object that is of the same type and has the same CloneID is considered a clone.
        /// If any of the clones are changed, all of the clones are changed to match.
        /// </summary>
        string CloneID { get; set; }

        int CloneUpdateCount { get; set; }

        [JsonIgnore]
        new IObject3D Parent { get; set; }

        /// <summary>
        /// Identifies if this object and its children should save their meshes
        /// </summary>
        bool Persistable { get; }

        [JsonIgnore]
        bool RebuildLocked { get; }

        string TypeName { get; }

        bool Visible { get; set; }

        /// <summary>
        /// Create a deep copy of the IObject3D objects
        /// </summary>
        /// <returns></returns>
        IObject3D DeepCopy();

        /// <summary>
        /// Remove the IObject3D from the tree and keep whatever functionality it was adding.
        /// This may require removing many child objects from the tree depending on implementation.
        /// </summary>
        void Apply(UndoBuffer undoBuffer);

        /// <summary>
        /// Get the Axis Aligned Bounding Box transformed by the given offset
        /// </summary>
        /// <param name="matrix">The Matrix4X4 to use for the bounds</param>
        /// <returns></returns>
        AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix);

        /// <summary>
        /// Return ray tracing data for the current data. This is used
        /// for intersections (mouse hit) and possibly rendering.
        /// </summary>
        /// <returns></returns>
        ITraceable GetBVHData();

        /// <summary>
        /// return a 64 bit hash code of the state of the object including transforms and children
        /// </summary>
        /// <returns></returns>
        ulong GetLongHashCode(ulong hash = 14695981039346656037);

        /// <summary>
        /// Mark that this object has changed (and notify its parent)
        /// </summary>
        void Invalidate(InvalidateArgs invalidateType);

        Task Rebuild();

        RebuildLock RebuildLock();

        /// <summary>
        /// Remove the IObject3D from the tree and undo whatever functionality it was adding (if appropriate).
        /// This may require removing many child objects from the tree depending on implementation.
        /// </summary>
        void Cancel(UndoBuffer undoBuffer);

        /// <summary>
        /// Directly assigns a mesh without firing events or invalidating
        /// </summary>
        /// <param name="mesh"></param>
        void SetMeshDirect(Mesh mesh);

        /// <summary>
        /// Serialize the current instance to Json
        /// </summary>
        /// <returns></returns>
        Task<string> ToJson(Action<double, string> progress = null);
    }
}