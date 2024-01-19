/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using IxMilia.ThreeMf;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace Matter_CAD_Lib.DesignTools._Object3D
{
    public class Object3D : IObject3D
    {
        private RebuildLock deserializeLock = null;

        public event EventHandler<InvalidateArgs> Invalidated;

        public static string AssetsPath { get; set; }

        public static Mesh FileMissingMesh { get; set; }

        public Object3D()
            : this(null)
        {
        }

        public Object3D(IEnumerable<IObject3D> children)
        {
            var type = GetType();
            if (type != typeof(Object3D)
                && type.Name != "InteractiveScene")
            {
                TypeName = type.Name;
            }

            if (children != null)
            {
                Children = new AscendableSafeList<IObject3D>(children, this);
            }
            else
            {
                Children = new AscendableSafeList<IObject3D>(this);
            }
        }

        private void Children_ItemsModified(object sender, EventArgs e)
        {
            if (!RebuildLocked)
            {
                Invalidate(InvalidateType.Children);
            }
        }

        public string ID { get; set; } = Guid.NewGuid().ToString();

        public string OwnerID { get; set; }

        public string CloneID { get; set; }

        public int CloneUpdateCount { get; set; }

        public AscendableSafeList<IObject3D> Children
        {
            get => _children;
            set
            {
                if (value != _children)
                {
                    if (_children != null)
                    {
                        _children.ItemsModified -= Children_ItemsModified;
                    }

                    _children = value;
                    _children.ItemsModified += Children_ItemsModified;
                }
            }
        }

        public string TypeName { get; }

        public IObject3D Parent { get; set; }

        private Color _color = Color.Transparent;

        public Color Color
        {
            get
            {
                return _color;
            }

            set
            {
                if (_color != value)
                {
                    _color = value;
                    Invalidate(InvalidateType.Color);
                }
            }
        }

        public void EnsureTransparentSorting()
        {
            var localMesh = Mesh;
            if (localMesh != null
                && localMesh.FaceBspTree == null
                && localMesh.Faces.Count < 2000
                && !buildingFaceBsp)
            {
                buildingFaceBsp = true;
                Task.Run(() =>
                {
                    // TODO: make a SHA1 based cache for the sorting on this mesh and use them from memory or disk
                    var bspTree = FaceBspTree.Create(localMesh);
                    UiThread.RunOnIdle(() => localMesh.FaceBspTree = bspTree);
                    buildingFaceBsp = false;
                });
            }
        }

        private PrintOutputTypes _outputType = PrintOutputTypes.Default;

        public PrintOutputTypes OutputType
        {
            get
            {
                return _outputType;
            }

            set
            {
                if (_outputType != value)
                {
                    // prevent recursion errors by holding a local pointer
                    var localMesh = Mesh;
                    _outputType = value;
                    Invalidate(InvalidateType.OutputType);
                }
            }
        }

        private Matrix4X4 _matrix = Matrix4X4.Identity;

        public virtual Matrix4X4 Matrix
        {
            get => _matrix;
            set
            {
                if (value != _matrix)
                {
                    foreach (var element in value.GetAsDoubleArray())
                    {
                        if (double.IsNaN(element)
                            || double.IsInfinity(element))
                        {
                            value = Matrix4X4.Identity;
                        }
                    }

                    _matrix = value;
                    Invalidate(InvalidateType.Matrix);
                }
            }
        }

        private object locker = new object();

        [JsonIgnore]
        private Mesh _mesh;

        public virtual Mesh Mesh
        {
            get => _mesh;
            set
            {
                lock (locker)
                {
                    if (_mesh != value)
                    {
                        _mesh = value;
                        traceData = null;
                        MeshPath = null;

                        if (!string.IsNullOrEmpty(CloneID))
                        {
                            CloneUpdateCount++;
                        }

                        Invalidate(InvalidateType.Mesh);
                    }
                }
            }
        }

        [JsonIgnore]
        public bool RebuildLocked
        {
            get
            {
                return this.DescendantsAndSelf().Where((i) =>
                {
                    if (i is Object3D object3D)
                    {
                        return object3D.RebuildLockCount > 0;
                    }

                    return false;
                }).Any();
            }
        }

        public string MeshPath { get; set; }

        /// <summary>
        /// Gets or set if the name has been override by the user.
        /// </summary>
        public bool NameOverriden { get; set; } = true;

        private string _name = "";

        public virtual string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    NameOverriden = true;
                    Invalidate(InvalidateType.Name);
                }
            }
        }

        [JsonIgnore]
        public virtual bool Persistable { get; set; } = true;

        [JsonIgnore]
        public virtual bool Printable
        {
            get
            {
                if (this is IPathProvider pathObject)
                {
                    if (pathObject.MeshIsSolidObject)
                    {
                        return true;
                    }
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Is this object visible in the scene
        /// </summary>
        public virtual bool Visible { get; set; } = true;

        public virtual bool CanApply
        {
            get
            {
                if (Children.Count == 0 && GetType() != typeof(Object3D))
                {
                    // we can apply anything that is not an object 3d into an object 3d
                    return true;
                }

                return false;
            }
        }

        public virtual bool CanEdit => this.HasChildren();

        [JsonIgnore]
        internal int RebuildLockCount { get; set; }

        /// <summary>
        /// Is this object expanded in the tree view
        /// </summary>
        public bool Expanded { get; set; }
        public static int MaxJsonDepth => 256;

        private class Object3DRebuildLock : RebuildLock
        {
            public Object3DRebuildLock(IObject3D item)
                : base(item)
            {
                if (item is Object3D object3D)
                {
                    object3D.RebuildLockCount++;
                }
            }

            public override void Dispose()
            {
                if (item is Object3D object3D)
                {
                    object3D.RebuildLockCount--;
#if DEBUG
                    if (object3D.RebuildLockCount < 0)
                    {
                        throw new Exception("Dispose is likely being called more than once");
                    }
#endif
                    // item.DebugDepth($"Decrease Lock Count {object3D.RebuildLockCount}");
                }
            }
        }

        public RebuildLock RebuildLock()
        {
            // this.DebugDepth($"Increase Lock Count {RebuildLockCount}");
            return new Object3DRebuildLock(this);
        }

        public static IObject3D Load(string filePath, CancellationToken cancellationToken, CacheContext cacheContext = null, Action<double, string> progress = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            if (cacheContext == null)
            {
                cacheContext = new CacheContext();
            }

            // Try to pull the item from cache
            if (!cacheContext.Items.TryGetValue(filePath, out IObject3D loadedItem) || loadedItem == null)
            {
                using (var stream = File.OpenRead(filePath))
                {
                    string extension = Path.GetExtension(filePath).ToLower();

                    loadedItem = Load(stream, extension, cancellationToken, cacheContext, progress);

                    // Cache loaded assets
                    if (cacheContext != null
                        && extension != ".mcx"
                        && loadedItem != null)
                    {
                        cacheContext.Items[filePath] = loadedItem;
                    }
                }
            }
            else
            {
                // Clone required for instancing
                loadedItem = loadedItem?.DeepCopy();
            }

            return loadedItem;
        }

        [OnDeserializing]
        internal void OnDeserializing(StreamingContext context)
        {
            deserializeLock = RebuildLock();
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            if (!Matrix.IsValid())
            {
                Matrix = Matrix4X4.Identity;
            }

            deserializeLock?.Dispose();
        }

        private static Matrix4X4 ToMatrix4X4(ThreeMfMatrix threeMfMatrix)
        {
            return new Matrix4X4(threeMfMatrix.M00, threeMfMatrix.M01, threeMfMatrix.M02, 0,
                threeMfMatrix.M10, threeMfMatrix.M11, threeMfMatrix.M12, 0,
                threeMfMatrix.M20, threeMfMatrix.M21, threeMfMatrix.M22, 0,
                threeMfMatrix.M30, threeMfMatrix.M31, threeMfMatrix.M32, 1);
        }

        private static void AddObject(Object3D object3D, ThreeMfObject threeMfObject, Matrix4X4 matrix)
        {
            var mesh3mf = threeMfObject.Mesh;
            if (mesh3mf.Triangles.Count > 0)
            {
                var mesh = new Mesh();

                foreach (var vertex in mesh3mf.Triangles)
                {
                    mesh.CreateFace(new Vector3[] {
                                            new Vector3(vertex.V1.X,vertex.V1.Y,vertex.V1.Z),
                                            new Vector3(vertex.V2.X,vertex.V2.Y,vertex.V2.Z),
                                            new Vector3(vertex.V3.X,vertex.V3.Y,vertex.V3.Z),
                                        });
                }

                object3D.Children.Add(new Object3D()
                {
                    Mesh = mesh,
                    Matrix = matrix,
                });
            }

            foreach (var component in threeMfObject.Components)
            {
                if (component.Object is ThreeMfObject subObject)
                {
                    AddObject(object3D, subObject, matrix * ToMatrix4X4(component.Transform));
                }
            }
        }

        public static bool IsBinaryMCX(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new char[4096];
            var readCount = new StreamReader(stream).ReadBlock(buffer, 0, buffer.Length);
            stream.Seek(0, SeekOrigin.Begin);
            if (readCount > 4
                && buffer[0] == 80 // 'P'
                && buffer[1] == 75 // 'K'
                && buffer[2] == 3 // 3
                && buffer[3] == 4)
            {
                return true;
            }

            return false;
        }

        public static IObject3D Load(Stream stream, string extension, CancellationToken cancellationToken, CacheContext cacheContext = null, Action<double, string> progress = null)
        {
            if (cacheContext == null)
            {
                cacheContext = new CacheContext();
            }

            try
            {
                switch (extension.ToUpper())
                {
                    case ".MCX":
                        {
                            string json;
                            // check if the file is binary
                            if (IsBinaryMCX(stream))
                            {
                                // it looks like a binary mcx file (which is a zip file). Load the contents
                                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                                {
                                    var sceneEntryName = archive.Entries.Where(e => e.Name.Contains("scene.mcx")).First().FullName;
                                    json = new StreamReader(archive.GetEntry(sceneEntryName).Open()).ReadToEnd();
                                }
                            }
                            else
                            {
                                json = new StreamReader(stream).ReadToEnd();
                            }

                            // Load the meta file and convert MeshPath links into objects
                            var loadedItem = JsonConvert.DeserializeObject<Object3D>(
                                json,
                                new JsonSerializerSettings
                                {
                                    // we need the JsonIObject3DContractResolver.CreateObjectContract to set the parent on the children
                                    ContractResolver = new JsonIObject3DContractResolver(),
                                    NullValueHandling = NullValueHandling.Ignore,
                                    MaxDepth = MaxJsonDepth,
                                    Converters = new List<JsonConverter> { new JsonIObject3DConverter() }
                                });

                            loadedItem?.LoadMeshLinks(cancellationToken, cacheContext, progress);

                            return loadedItem;
                        }

                    case ".STL":
                        var result = new Object3D();
                        result.SetMeshDirect(StlProcessing.Load(stream, cancellationToken, progress));
                        return result;

                    case ".AMF":
                        return AmfDocument.Load(stream, cancellationToken, progress);

                    case ".3MF":
                        {
                            var file = ThreeMfFile.Load(stream);
                            var object3D = new Object3D();
                            foreach (var model in file.Models)
                            {
                                foreach (var item in model.Items)
                                {
                                    if (item.Object is ThreeMfObject itemObject)
                                    {
                                        var transform = item.Transform;
                                        AddObject(object3D, itemObject, ToMatrix4X4(transform));
                                    }
                                }
                            }

                            if (object3D?.Children.Count > 0)
                            {
                                return object3D;
                            }

                            return null;
                        }

                    case ".OBJ":
                        return ObjSupport.Load(stream, progress);

                    default:
                        return null;
                }
            }
            catch { }

            return null;
        }

        private static Random rng = new Random();

        public static string ValidateAndFixFilename(string filename)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string validFilename = new string(filename.Where(ch => !invalidChars.Contains(ch)).ToArray());

            if (validFilename.Length > 40) // check if filename is too long
            {
                string randomKey = rng.Next(10000, 99999).ToString(); // 5 digit random number
                validFilename = validFilename.Substring(0, 40 - randomKey.Length) + randomKey; // truncate and append random key
            }

            return validFilename;
        }

        public static bool Save(IObject3D item,
            string meshPathAndFileName,
            bool mergeMeshes,
            CancellationToken cancellationToken,
            MeshOutputSettings outputInfo = null,
            Action<double, string> reportProgress = null,
            bool saveMultipleStls = false)
        {
            try
            {
                if (outputInfo == null)
                {
                    outputInfo = new MeshOutputSettings();
                }

                switch (Path.GetExtension(meshPathAndFileName).ToUpper())
                {
                    // TODO: Consider if save to MCX is needed or if existing patterns already cover that case
                    // case ".MCX":
                    // using (var outstream = File.OpenWrite(meshPathAndFileName))
                    // {
                    // item.SaveTo(outstream, reportProgress);
                    // }
                    // return true;

                    case ".STL":
                        if (saveMultipleStls)
                        {
                            bool success = true;
                            foreach (var child in item.VisibleMeshes())
                            {
                                var firstValidName = child.Name;
                                if (string.IsNullOrEmpty(firstValidName))
                                {
                                    firstValidName = child.Parents().Where(i => !string.IsNullOrEmpty(i.Name)).FirstOrDefault().Name;
                                    if (firstValidName == null)
                                    {
                                        Path.GetFileName(meshPathAndFileName);
                                    }
                                }

                                // remove any extension
                                firstValidName = Path.GetFileNameWithoutExtension(firstValidName);
                                // make sure the name is valid
                                firstValidName = ValidateAndFixFilename(firstValidName);

                                var childMeshPathAndFileName = Path.Combine(Path.GetDirectoryName(meshPathAndFileName), firstValidName + ".stl");

                                childMeshPathAndFileName = Util.GetNonCollidingFileName(childMeshPathAndFileName);

                                if (mergeMeshes)
                                {
                                    outputInfo.CsgOptionState = MeshOutputSettings.CsgOption.DoCsgMerge;
                                }
                                Mesh mesh = DoMergeAndTransform(child, outputInfo, cancellationToken, reportProgress);
                                success &= mesh.Save(childMeshPathAndFileName, cancellationToken, outputInfo);
                            }
                            return success;
                        }
                        else
                        {
                            if (mergeMeshes)
                            {
                                outputInfo.CsgOptionState = MeshOutputSettings.CsgOption.DoCsgMerge;
                            }
                            var mesh = DoMergeAndTransform(item, outputInfo, cancellationToken, reportProgress);
                            if (mesh != null)
                            {
                                return mesh.Save(meshPathAndFileName, cancellationToken, outputInfo);
                            }

                            return false;
                        }

                    case ".AMF":
                        outputInfo.ReportProgress = reportProgress;
                        return AmfDocument.Save(item, meshPathAndFileName, outputInfo);

                    case ".3MF":
#if DEBUG
                        // create the file
                        var threeMfFile = new ThreeMfFile();
                        // add the thumnail
                        // add ThreeMfModel
                        foreach (var child in item.DescendantsAndSelf())
                        {
                            // save all the meshes and 
                        }

                        // save the mcx file into the archive
                        threeMfFile.Save(meshPathAndFileName);
#endif
                        return false;

                    case ".OBJ":
                        outputInfo.ReportProgress = reportProgress;
                        return ObjSupport.Save(item, meshPathAndFileName, outputInfo);

                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// This is used exclusively while exporting STLs and needs to account for holes , solids, support, wipe towers and fuzzy objects
        /// </summary>
        /// <param name="item"></param>
        /// <param name="outputInfo"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="reportProgress"></param>
        /// <returns></returns>
        private static Mesh DoMergeAndTransform(IObject3D item,
            MeshOutputSettings outputInfo,
            CancellationToken cancellationToken,
            Action<double, string> reportProgress = null)
        {
            var persistable = item.VisibleMeshes().Where((i) => i.WorldPersistable());

            if (outputInfo.CsgOptionState == MeshOutputSettings.CsgOption.DoCsgMerge)
            {
                var solidsToUnion = persistable.Where((i) => i.WorldOutputType() == PrintOutputTypes.Default || i.WorldOutputType() == PrintOutputTypes.Solid);

                var holesToSubtract = persistable.Where((i) => i.WorldOutputType() == PrintOutputTypes.Hole);

                if (solidsToUnion.Any()
                    && holesToSubtract.Any())
                {
                    // union every solid (non-hole, not support structures)
                    var solidsObject = new Object3D()
                    {
                        Mesh = CombineParticipants(item, solidsToUnion, cancellationToken, (ratio, message) =>
                        {
                            reportProgress?.Invoke(Util.GetRatio(0, .33, ratio), null);
                        })
                    };

                    // union every hole
                    var holesObject = new Object3D()
                    {
                        Mesh = CombineParticipants(item, holesToSubtract, cancellationToken, (ratio, message) =>
                        {
                            reportProgress?.Invoke(Util.GetRatio(.33, .66, ratio), null);
                        })
                    };

                    // subtract all holes from all solids

                    var result = DoSubtract(item, new IObject3D[] { solidsObject },
                        new IObject3D[] { holesObject },
                        (ratio, message) =>
                        {
                            reportProgress?.Invoke(Util.GetRatio(.66, 1, ratio), null);
                        }, cancellationToken);

                    return result.First().Mesh;
                }
                else // we only have meshes to union
                {
                    // union every solid (non-hole, not support structures)
                    return CombineParticipants(item, solidsToUnion, cancellationToken, (ratio, message) =>
                    {
                        reportProgress?.Invoke(ratio, null);
                    });
                }
            }
            else
            {
                var allPolygons = new Mesh();
                foreach (var rawItem in persistable)
                {
                    var mesh = rawItem.Mesh.Copy(cancellationToken);
                    mesh.Transform(rawItem.WorldMatrix());
                    allPolygons.CopyFaces(mesh);
                }

                return allPolygons;
            }
        }

        /// <summary>
        /// Called when loading existing content and needing to bypass the clearing of MeshPath that normally occurs in the this.Mesh setter
        /// </summary>
        /// <param name="mesh">The loaded mesh to assign this instance</param>
        public void SetMeshDirect(Mesh mesh)
        {
            lock (locker)
            {
                if (_mesh != mesh)
                {
                    _mesh = mesh;
                    OnMeshAssigned();
                }
            }
        }

        protected virtual void OnMeshAssigned()
        {
        }

        public virtual void OnInvalidate(InvalidateArgs invalidateType)
        {
            Invalidated?.Invoke(this, invalidateType);
            Parent?.Invalidate(invalidateType);
        }

        /// <summary>
        /// This wil cancel any ansync re-builds happening in our parents
        /// </summary>
        /// <returns></returns>
        public virtual Task Rebuild()
        {
            return Task.CompletedTask;
        }

        public void Invalidate(InvalidateType invalidateType)
        {
            Invalidate(new InvalidateArgs(this, invalidateType));
        }

        private static HashSet<IObject3D> pendingUpdates = new HashSet<IObject3D>();

        public void Invalidate(InvalidateArgs invalidateArgs)
        {
            if (!RebuildLocked)
            {
                OnInvalidate(invalidateArgs);
            }
            else
            {
                RunningInterval runningInterval = null;
                void RebuildWhenUnlocked()
                {
                    lock (pendingUpdates)
                    {
                        if (RebuildLocked)
                        {
                            if (this is IBuildsOnThread buildsOnThread
                                && buildsOnThread.IsBuilding)
                            {
                                buildsOnThread.CancelBuild();

                                // and cancel the current building of any parent that can be canceled
                                foreach (var parent in this.Parents())
                                {
                                    if (parent is IBuildsOnThread buildsOnThread2)
                                    {
                                        buildsOnThread2.CancelBuild();
                                    }
                                }
                            }
                        }
                        else
                        {
                            UiThread.ClearInterval(runningInterval);
                            OnInvalidate(invalidateArgs);
                            pendingUpdates.Remove(this);
                        }
                    }
                }

                lock (pendingUpdates)
                {
                    if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
                        && invalidateArgs.Source == this
                        && !pendingUpdates.Contains(this))
                    {
                        pendingUpdates.Add(this);
                        // we need to get back to the user requested change when not locked
                        runningInterval = UiThread.SetInterval(RebuildWhenUnlocked, .05);
                    }
                }
            }
        }

        public const BindingFlags OwnedPropertiesOnly = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public static IEnumerable<PropertyInfo> GetChildSelectorPropreties(IObject3D item)
        {
            return item.GetType().GetProperties(OwnedPropertiesOnly)
                .Where((pi) =>
                {
                    return pi.PropertyType == typeof(SelectedChildren);
                });
        }

        // Deep copy via json serialization
        public IObject3D DeepCopy()
        {
            IObject3D clonedItem;

            using (this.RebuilLockAll())
            {
                var originalParent = Parent;

                var allItemsByID = new Dictionary<string, Mesh>();

                try
                {
                    // Index items by ID
                    this.FixIdsRecursive();
                    foreach (var item in this.DescendantsAndSelf())
                    {
                        if (!allItemsByID.ContainsKey(item.ID))
                        {
                            allItemsByID.Add(item.ID, item.Mesh);
                        }
                    }
                }
                catch
                {
                    throw new Exception("Error cloning item due to duplicate identifiers");
                }

                using (var memoryStream = new MemoryStream())
                using (var writer = new StreamWriter(memoryStream))
                {
                    // Wrap with a temporary container
                    var wrapper = new Object3D();
                    wrapper.Children.Add(this);

                    // Push json into stream and reset to start
                    writer.Write(JsonConvert.SerializeObject(wrapper, Formatting.Indented));
                    writer.Flush();
                    memoryStream.Position = 0;

                    // Load serialized content
                    var roundTripped = Load(memoryStream, ".mcx", CancellationToken.None);

                    // Remove temp container
                    clonedItem = roundTripped.Children.First();
                }

                using (clonedItem.RebuilLockAll())
                {
                    var idRemaping = new Dictionary<string, string>();
                    // Copy mesh instances to cloned tree
                    foreach (var descendant in clonedItem.DescendantsAndSelf())
                    {
                        if (allItemsByID.ContainsKey(descendant.ID))
                        {
                            descendant.SetMeshDirect(allItemsByID[descendant.ID]);
                        }
                        else
                        {
                            descendant.SetMeshDirect(PlatonicSolids.CreateCube(10, 10, 10));
                        }

                        // store the original id
                        string originalId = descendant.ID;
                        // update it to a new ID
                        descendant.ID = Guid.NewGuid().ToString();
                        // Now OwnerID must be reprocessed after changing ID to ensure consistency
                        foreach (var child in descendant.DescendantsAndSelf().Where((c) => c.OwnerID == originalId))
                        {
                            child.OwnerID = descendant.ID;
                        }

                        if (!idRemaping.ContainsKey(originalId))
                        {
                            idRemaping.Add(originalId, descendant.ID);
                        }
                    }

                    // Clean up any child references in the objects
                    foreach (var descendant in clonedItem.DescendantsAndSelf())
                    {
                        // find all ObjecIdListAttributes and update them
                        foreach (var property in GetChildSelectorPropreties(descendant))
                        {
                            var newChildrenSelector = new SelectedChildren();
                            bool foundReplacement = false;

                            // sync ids
                            foreach (var id in (SelectedChildren)property.GetGetMethod().Invoke(descendant, null))
                            {
                                // update old id to new id
                                if (idRemaping.ContainsKey(id))
                                {
                                    newChildrenSelector.Add(idRemaping[id]);
                                    foundReplacement = true;
                                }
                                else
                                {
                                    // this really should never happen
                                    newChildrenSelector.Add(id);
                                }
                            }

                            if (foundReplacement)
                            {
                                property.GetSetMethod().Invoke(descendant, new[] { newChildrenSelector });
                            }
                        }
                    }

                    // the cloned item does not have a parent
                    clonedItem.Parent = null;
                }

                // restore the parent
                Parent = originalParent;
            }

            return clonedItem;
        }

        public override string ToString()
        {
            var name = string.IsNullOrEmpty(Name) ? "" : $", '{Name}'";
            if (Parent != null)
            {
                return $"{GetType().Name}{name}, ID = {ID}, Parent = {Parent.ID}";
            }

            return $"{GetType().Name}{name}, ID = {ID}";
        }

        public virtual AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix, Func<IObject3D, bool> considerItem)
        {
            var totalTransorm = Matrix * matrix;

            var totalBounds = AxisAlignedBoundingBox.Empty();
            // Set the initial bounding box to empty or the bounds of the objects MeshGroup
            if (Mesh != null)
            {
                totalBounds = Mesh.GetAxisAlignedBoundingBox(totalTransorm);
            }
            else if (Children.Count > 0)
            {
                foreach (IObject3D child in Children)
                {
                    if (child.Visible
                        && considerItem(child))
                    {
                        AxisAlignedBoundingBox childBounds;
                        // Add the bounds of each child object
                        if (child is Object3D object3D)
                        {
                            childBounds = object3D.GetAxisAlignedBoundingBox(totalTransorm, considerItem);
                        }
                        else
                        {
                            childBounds = child.GetAxisAlignedBoundingBox(totalTransorm);
                        }

                        // Check if the child actually has any bounds
                        if (childBounds.XSize > 0)
                        {
                            totalBounds += childBounds;
                        }
                    }
                }
            }

            // Make sure we have some data. Else return 0 bounds.
            if (totalBounds.MinXYZ.X == double.PositiveInfinity)
            {
                totalBounds = AxisAlignedBoundingBox.Zero();
            }

            return totalBounds;
        }

        public virtual AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
        {
            var totalTransorm = Matrix * matrix;

            var totalBounds = AxisAlignedBoundingBox.Empty();
            // Set the initial bounding box to empty or the bounds of the objects MeshGroup
            if (Mesh != null)
            {
                totalBounds = Mesh.GetAxisAlignedBoundingBox(totalTransorm);
            }
            else if (Children.Count > 0)
            {
                foreach (IObject3D child in Children)
                {
                    if (child.Visible)
                    {
                        // Add the bounds of each child object
                        var childBounds = child.GetAxisAlignedBoundingBox(totalTransorm);
                        // Check if the child actually has any bounds
                        if (childBounds.XSize > 0)
                        {
                            totalBounds += childBounds;
                        }
                    }
                }
            }

            // Make sure we have some data. Else return 0 bounds.
            if (totalBounds.MinXYZ.X == double.PositiveInfinity)
            {
                totalBounds = AxisAlignedBoundingBox.Zero();
            }

            return totalBounds;
        }

        private ITraceable traceData;

        // Cache busting on child nodes
        private ulong tracedHashCode = ulong.MinValue;
        private bool buildingFaceBsp;
        private AscendableSafeList<IObject3D> _children;

        /// <summary>
        /// Create or return a Bounding Volume Hierarchy for this mesh. Is created add it to the property bag.
        /// </summary>
        /// <returns>The root of the BVH</returns>
        public ITraceable GetBVHData()
        {
            var processingMesh = Mesh;
            // Cache busting on child nodes
            ulong hashCode = GetLongHashCode();

            if (traceData == null || tracedHashCode != hashCode)
            {
                var traceables = new List<ITraceable>();
                // Check if we have a mesh at this level
                if (processingMesh != null)
                {
                    // we have a mesh so don't recurse into children
                    processingMesh.PropertyBag.TryGetValue("MeshTraceData", out object objectData);
                    var meshTraceData = objectData as ITraceable;
                    if (meshTraceData == null
                        && processingMesh.Faces.Count > 0)
                    {
                        // Get the trace data for the local mesh
                        // First create trace data that builds fast but traces slow
                        var simpleTraceData = processingMesh.CreateBVHData(BvhCreationOptions.SingleUnboundCollection);
                        if (simpleTraceData != null)
                        {
                            try
                            {
                                processingMesh.PropertyBag.Add("MeshTraceData", simpleTraceData);
                            }
                            catch
                            {
                            }
                        }

                        traceables.Add(simpleTraceData);
                        // Then create trace data that traces fast but builds slow
                        // var completeTraceData = processingMesh.CreateTraceData(0);
                        // processingMesh.PropertyBag["MeshTraceData"] = completeTraceData;
                    }
                    else
                    {
                        traceables.Add(meshTraceData);
                    }
                }
                else // No mesh, so get the trace data for all children
                {
                    foreach (Object3D child in Children)
                    {
                        if (child.Visible)
                        {
                            traceables.Add(child.GetBVHData());
                        }
                    }
                }

                // Wrap with a BVH
                traceData = BoundingVolumeHierarchy.CreateNewHierachy(traceables, BvhCreationOptions.SingleUnboundCollection);
                tracedHashCode = hashCode;
            }

            // Wrap with the local transform
            return new Transform(traceData, Matrix);
        }

        // Hashcode for lists as proposed by Jon Skeet
        // http://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order
        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            hash = Matrix.GetLongHashCode(hash);

            if (Mesh != null)
            {
                hash = Mesh.GetLongHashCode(hash);
            }

            foreach (var child in Children)
            {
                // The children need to include their transforms
                hash = child.GetLongHashCode(hash);
            }

            return hash;
        }

        public Task<string> ToJson(Action<double, string> progress = null)
        {
            return Task.FromResult(JsonConvert.SerializeObject(
                this,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ContractResolver = new JsonIObject3DContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                    MaxDepth = MaxJsonDepth
                }));
        }

        public virtual void Apply(UndoBuffer undoBuffer)
        {
            using (RebuildLock())
            {
                var newChildren = new List<IObject3D>();
                // push our matrix into a copy of our children
                foreach (var child in Children)
                {
                    var newChild = child.DeepCopy();
                    newChildren.Add(newChild);
                    newChild.Matrix *= Matrix;
                    var flags = Object3DPropertyFlags.Visible;
                    if (Color.alpha != 0)
                    {
                        flags |= Object3DPropertyFlags.Color;
                    }

                    if (OutputType != PrintOutputTypes.Default)
                    {
                        flags |= Object3DPropertyFlags.OutputType;
                    }

                    newChild.CopyProperties(this, flags);
                }

                if (Children.Count == 0 && GetType() != typeof(Object3D))
                {
                    var newChild = new Object3D();
                    newChildren.Add(newChild);
                    newChild.CopyProperties(this, Object3DPropertyFlags.All);
                    newChild.Mesh = Mesh;
                }

                // and replace us with the children
                var replaceCommand = new ReplaceCommand(new[] { this }, newChildren, false);
                if (undoBuffer != null)
                {
                    undoBuffer.AddAndDo(replaceCommand);
                }
                else
                {
                    replaceCommand.Do();
                }

                foreach (var child in newChildren)
                {
                    child.MakeNameNonColliding();
                }
            }

            Invalidate(InvalidateType.Children);
        }

        public virtual void Cancel(UndoBuffer undoBuffer)
        {
            var parent = Parent;

            using (RebuildLock())
            {
                if (undoBuffer != null)
                {
                    var newTree = DeepCopy();
                    using (newTree.RebuildLock())
                    {
                        // push our matrix into a copy of our children (so they don't jump away)
                        foreach (var child in newTree.Children)
                        {
                            using (child.RebuildLock())
                            {
                                child.Matrix *= Matrix;
                            }
                        }
                    }

                    // and replace us with the children
                    undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, newTree.Children.ToList(), false));
                }
                else
                {
                    // push our matrix into a copy of our children (so they don't jump away)
                    foreach (var child in Children)
                    {
                        child.Matrix *= Matrix;
                    }

                    parent.Children.Modify(list =>
                    {
                        list.Remove(this);
                        list.AddRange(Children);
                    });
                }
            }

            parent.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
        }

        public bool Equals(IObject3D other)
        {
            return base.Equals(other);
        }

        public static string CalculateName(IEnumerable<IObject3D> setA, string aSeprator)
        {
            var setACount = setA?.Count() ?? 0;
            var name = "";

            if (setACount > 0)
            {
                foreach (var item in setA.OrderBy(i => i.Name))
                {
                    if (name == "")
                    {
                        name = item.Name;
                    }
                    else
                    {
                        name += aSeprator + item.Name;
                    }
                }
            }

            return name;
        }

        public static string CalculateName(IEnumerable<IObject3D> setA,
            string aSeparator,
            string setSeparator,
            IEnumerable<IObject3D> setB,
            string bSeparator)
        {
            var setACount = setA?.Count() ?? 0;
            var setBCount = setB?.Count() ?? 0;
            if (setACount == 0 && setBCount == 0)
            {
                return "Empty".Localize();
            }

            var name = CalculateName(setA, aSeparator);

            if (setACount > 0 && setBCount > 0)
            {
                name += setSeparator;
            }

            name += CalculateName(setB, bSeparator);

            return name;
        }

        public static List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>> GetTouchingMeshes(IObject3D rootObject, IEnumerable<IObject3D> participants)
        {
            void AddAllTouching(List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)> touching,
                List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)> available)
            {
                // add the frirst item
                touching.Add(available[available.Count - 1]);
                available.RemoveAt(available.Count - 1);

                var indexBeingChecked = 0;

                // keep adding items until we have checked evry item in the the touching list
                while (indexBeingChecked < touching.Count
                    && available.Count > 0)
                {
                    // look for a aabb that intersects any aabb in the set
                    for (int i = available.Count - 1; i >= 0; i--)
                    {
                        if (touching[indexBeingChecked].aabb.Intersects(available[i].aabb))
                        {
                            touching.Add(available[i]);
                            available.RemoveAt(i);
                        }
                    }

                    indexBeingChecked++;
                }
            }

            var allItems = participants.Select(i =>
            {
                var mesh = i.Mesh.Copy(CancellationToken.None);
                var matrix = i.WorldMatrix(rootObject);
                var aabb = mesh.GetAxisAlignedBoundingBox(matrix);
                return (mesh, matrix, aabb);
            }).ToList();

            var touchingSets = new List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>>();

            while (allItems.Count > 0)
            {
                var touchingSet = new List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>();
                touchingSets.Add(touchingSet);
                AddAllTouching(touchingSet, allItems);
            }

            return touchingSets;
        }

        public static IEnumerable<IObject3D> DoSubtract(IObject3D rootObject,
            IEnumerable<IObject3D> keepItems,
            IEnumerable<IObject3D> removeItems,
            Action<double, string> reporter,
            CancellationToken cancellationToken,
            ProcessingModes processingMode = ProcessingModes.Polygons,
            ProcessingResolution inputResolution = ProcessingResolution._64,
            ProcessingResolution outputResolution = ProcessingResolution._64)
        {
            var results = new List<IObject3D>();
            if (keepItems?.Any() == true)
            {
                if (removeItems?.Any() == true)
                {
                    var totalOperations = removeItems.Count() * keepItems.Count();
                    double amountPerOperation = 1.0 / totalOperations;
                    double ratioCompleted = 0;

                    foreach (var keep in keepItems)
                    {
#if false
						var items = removeItems.Select(i => (i.Mesh, i.WorldMatrix(rootObject))).ToList();
						items.Insert(0, (keep.Mesh, keep.Matrix));
						var resultsMesh = BooleanProcessing.DoArray(items,
							CsgModes.Subtract,
							processingMode,
							inputResolution,
							outputResolution,
							reporter,
							cancellationToken);
#else
                        var resultsMesh = keep.Mesh;
                        var keepWorldMatrix = keep.Matrix;
                        if (rootObject != null)
                        {
                            keepWorldMatrix = keep.WorldMatrix(rootObject);
                        }

                        foreach (var remove in removeItems)
                        {
                            var removeWorldMatrix = remove.Matrix;
                            if (rootObject != null)
                            {
                                removeWorldMatrix = remove.WorldMatrix(rootObject);
                            }

                            resultsMesh = BooleanProcessing.Do(resultsMesh,
                                keepWorldMatrix,
                                // other mesh
                                remove.Mesh,
                                removeWorldMatrix,
                                // operation type
                                CsgModes.Subtract,
                                processingMode,
                                inputResolution,
                                outputResolution,
                                // reporting
                                reporter,
                                amountPerOperation,
                                ratioCompleted,
                                cancellationToken);

                            // after the first time we get a result the results mesh is in the right coordinate space
                            keepWorldMatrix = Matrix4X4.Identity;

                            // report our progress
                            ratioCompleted += amountPerOperation;
                            reporter?.Invoke(ratioCompleted, null);
                        }

#endif
                        // store our results mesh
                        var resultsItem = new Object3D()
                        {
                            Mesh = resultsMesh,
                            Visible = false,
                            OwnerID = keep.ID
                        };

                        // copy all the properties but the matrix
                        if (rootObject != null)
                        {
                            resultsItem.CopyWorldProperties(keep, rootObject, Object3DPropertyFlags.All & ~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible));
                        }
                        else
                        {
                            resultsItem.CopyProperties(keep, Object3DPropertyFlags.All & ~(Object3DPropertyFlags.Matrix | Object3DPropertyFlags.Visible));
                        }

                        // and add it to this
                        results.Add(resultsItem);
                    }
                }
            }

            return results;
        }

        public static Mesh CombineParticipants(IObject3D rootObject,
            IEnumerable<IObject3D> participants,
            CancellationToken cancellationToken,
            Action<double, string> reporter = null,
            ProcessingModes processingMode = ProcessingModes.Polygons,
            ProcessingResolution inputResolution = ProcessingResolution._64,
            ProcessingResolution outputResolution = ProcessingResolution._64)
        {
            List<List<(Mesh mesh, Matrix4X4 matrix, AxisAlignedBoundingBox aabb)>> touchingSets = GetTouchingMeshes(rootObject, participants);

            var totalOperations = touchingSets.Sum(t => t.Count);

            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var setMeshes = new List<Mesh>();
            foreach (var set in touchingSets)
            {
                var setMesh = set.First().Item1;
                var keepWorldMatrix = set.First().matrix;

                if (set.Count > 1)
                {
#if true
                    setMesh = BooleanProcessing.DoArray(set.Select(i => (i.mesh, i.matrix)),
                        CsgModes.Union,
                        processingMode,
                        inputResolution,
                        outputResolution,
                        reporter,
                        cancellationToken);
#else

                    bool first = true;
                    foreach (var next in set)
                    {
                        if (first)
                        {
                            first = false;
                            continue;
                        }

                        setMesh = BooleanProcessing.Do(setMesh,
                            keepWorldMatrix,
                            // other mesh
                            next.mesh,
                            next.matrix,
                            // operation type
                            CsgModes.Union,
                            processingMode,
                            inputResolution,
                            outputResolution,
                            // reporting
                            reporter,
                            amountPerOperation,
                            ratioCompleted,
                            progressStatus,
                            cancellationToken);

                        // after the first time we get a result the results mesh is in the right coordinate space
                        keepWorldMatrix = Matrix4X4.Identity;

                        // report our progress
                        ratioCompleted += amountPerOperation;
                        progressStatus.Progress0To1 = ratioCompleted;
                        reporter?.Report(progressStatus);
                    }
#endif

                    setMeshes.Add(setMesh);
                }
                else
                {
                    setMesh.Transform(keepWorldMatrix);
                    // report our progress
                    ratioCompleted += amountPerOperation;
                    reporter?.Invoke(ratioCompleted, null);
                    setMeshes.Add(setMesh);
                }
            }

            Mesh resultsMesh = null;
            foreach (var setMesh in setMeshes)
            {
                if (resultsMesh == null)
                {
                    resultsMesh = setMesh;
                }
                else
                {
                    resultsMesh.CopyAllFaces(setMesh, Matrix4X4.Identity);
                }
            }

            return resultsMesh;
        }
    }

}