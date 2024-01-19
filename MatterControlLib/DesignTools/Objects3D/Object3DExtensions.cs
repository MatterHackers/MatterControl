/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace Matter_CAD_Lib.DesignTools._Object3D
{
    public static class Object3DExtensions
    {
        public static void LoadMeshLinks(this IObject3D tempScene, CancellationToken cancellationToken, CacheContext cacheContext, Action<double, string> progress)
        {
            var itemsToLoad = (from object3D in tempScene.DescendantsAndSelf()
                               where !string.IsNullOrEmpty(object3D.MeshPath)
                               select object3D).ToList();

            var ratioPerItem = 1.0 / itemsToLoad.Count;
            for (int i = 0; i < itemsToLoad.Count; i++)
            {
                var object3D = itemsToLoad[i];
                object3D.LoadLinkedMesh(cacheContext, cancellationToken, (ratio, title) =>
                {
                    var accumulatedRatio = i * ratioPerItem + ratio * ratioPerItem;
                    progress?.Invoke(accumulatedRatio, title);
                });
            }
        }

        /// <summary>
        /// This will cancele all parent building and update the clone count for any clon that has rebuilt.
        /// </summary>
        /// <param name="object3D"></param>
        public static void DoRebuildComplete(this IObject3D object3D)
        {
            if (!string.IsNullOrEmpty(object3D.CloneID))
            {
                object3D.CloneUpdateCount++;
            }

            // and cancel the current building of any parent that can be canceled
            foreach (var parent in object3D.Parents())
            {
                if (parent is IBuildsOnThread buildsOnThread)
                {
                    buildsOnThread.CancelBuild();
                }
            }
        }

        public static void FixIdsRecursive(this IObject3D input)
        {
            var ids = new HashSet<string>();
            foreach (var item in input.DescendantsAndSelf())
            {
                if (item is Object3D object3D)
                {
                    if (ids.Contains(object3D.ID))
                    {
#if DEBUG
                        throw new Exception("Bad Id");
#endif
                        object3D.ID = Guid.NewGuid().ToString();
                    }

                    ids.Add(object3D.ID);
                }
            }
        }

        public static IObject3D FirstWithMultipleChildrenDescendantsAndSelf(this IObject3D item)
        {
            var parentOfMultipleChildren = item.DescendantsAndSelf().Where(i => i.Children.Count > 1).FirstOrDefault() as Object3D;
            if (parentOfMultipleChildren == null)
            {
                return item;
            }

            return parentOfMultipleChildren;
        }

        public static int Depth(this IObject3D item)
        {
            return item.Parents().Count();
        }

        [Conditional("DEBUG")]
        public static void DebugDepth(this IObject3D item, string extra = "")
        {
            Debug.WriteLine(new string(' ', item.Depth()) + $"({item.Depth()}) {item.GetType().Name} " + extra);
        }

        private static void LoadLinkedMesh(this IObject3D item, CacheContext cacheContext, CancellationToken cancellationToken, Action<double, string> progress)
        {
            // Abort load if cancel requested
            cancellationToken.ThrowIfCancellationRequested();

            string filePath = item.ResolveFilePath(progress, cancellationToken);

            if (string.Equals(Path.GetExtension(filePath), ".mcx", StringComparison.OrdinalIgnoreCase))
            {
                var loadedItem = Object3D.Load(filePath, cancellationToken, cacheContext, progress);
                if (loadedItem != null)
                {
                    item.SetMeshDirect(loadedItem.Mesh);

                    // Copy loaded mcx children to source node
                    // TODO: potentially needed for leaf mcx nodes, review and tests required
                    item.Children = loadedItem.Children;
                }
            }
            else
            {
                try
                {
                    if (cacheContext.Meshes.TryGetValue(filePath, out Mesh mesh))
                    {
                        item.SetMeshDirect(mesh);
                    }
                    else
                    {
                        var loadedItem = Object3D.Load(filePath, cancellationToken);
                        if (loadedItem?.Children.Count() > 0)
                        {
                            loadedItem.Children.Modify(loadedChildren =>
                            {
                                // copy the children
                                item.Children.Modify(children =>
                                {
                                    children.AddRange(loadedChildren);
                                    loadedChildren.Clear();
                                });
                            });
                        }
                        else
                        {
                            // copy the mesh
                            var loadedMesh = loadedItem?.Mesh;
                            cacheContext.Meshes[filePath] = loadedMesh;
                            item.SetMeshDirect(loadedMesh);
                        }
                    }
                }
                catch
                {
                    // Fall back to Missing mesh if available
                    item.SetMeshDirect(Object3D.FileMissingMesh);
                }
            }
        }

        public static string ResolveFilePath(this IObject3D item, Action<double, string> progress, CancellationToken cancellationToken)
        {
            // Natural path
            return ResolveFilePath(item.MeshPath, progress, cancellationToken);
        }

        public static string ResolveFilePath(string filePath, Action<double, string> progress, CancellationToken cancellationToken)
        {
            // If relative/asset file name
            if (Path.GetDirectoryName(filePath) == "")
            {
                string sha1PlusExtension = filePath;

                filePath = Path.Combine(Object3D.AssetsPath, sha1PlusExtension);

                // If the asset is not in the local cache folder, acquire it
                if (!File.Exists(filePath))
                {
                    var endTime = UiThread.CurrentTimerMs + 5000;
                    var aquired = false;
                    Task.Run(async () =>
                    {
                        // Prime cache
                        await AssetObject3D.AssetManager.AcquireAsset(sha1PlusExtension, cancellationToken, progress);
                        aquired = true;
                    });

                    // wait up to 5 seconds to aqurie the asset
                    while (!aquired && UiThread.CurrentTimerMs < endTime)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            return filePath;
        }

        public static async void SaveTo(this IObject3D sourceItem, Stream outputStream, Action<double, string> progress = null)
        {
            await sourceItem.PersistAssets(progress);

            var streamWriter = new StreamWriter(outputStream);
            streamWriter.Write(await sourceItem.ToJson());
            streamWriter.Flush();
        }

        public static void Fit(this WorldView world, IObject3D itemToRender, RectangleDouble goalBounds)
        {
            world.Fit(itemToRender, goalBounds, Matrix4X4.Identity);
        }

        public static Matrix4X4 GetXYInViewRotation(this WorldView world, Vector3 center)
        {
            var positions = new Vector3[]
            {
                center + new Vector3(1, 0, 0),
                center + new Vector3(0, 1, 0),
                center + new Vector3(-1, 0, 0),
                center + new Vector3(0, -1, 0),
            };

            double bestX = double.NegativeInfinity;
            int indexX = 0;
            // get the closest z on the bottom in view space
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                Vector3 axisSide = positions[cornerIndex];
                Vector3 axisSideScreenSpace = world.WorldToScreenSpace(axisSide);

                if (axisSideScreenSpace.X > bestX)
                {
                    indexX = cornerIndex;
                    bestX = axisSideScreenSpace.X;
                }
            }

            var transform = Matrix4X4.Identity;
            switch (indexX)
            {
                case 0:
                    // transform = Matrix4X4.CreateRotationZ(0);
                    break;

                case 1:
                    transform = Matrix4X4.CreateRotationZ(MathHelper.Tau / 4);
                    break;

                case 2:
                    transform = Matrix4X4.CreateRotationZ(-MathHelper.Tau / 2);
                    break;

                case 3:
                    transform = Matrix4X4.CreateRotationZ(MathHelper.Tau * 3 / 4);
                    break;
            }

            return transform;
        }

        public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(this IEnumerable<IObject3D> items)
        {
            var aabb = AxisAlignedBoundingBox.Empty();
            foreach (var item in items)
            {
                if (item != null)
                {
                    aabb += item.GetAxisAlignedBoundingBox();
                }
            }

            return aabb;
        }

        public static void Translate(this IEnumerable<IObject3D> items, Vector3 translation)
        {
            var matrix = Matrix4X4.CreateTranslation(translation);
            foreach (var item in items)
            {
                item.Matrix *= matrix;
            }
        }

        public static void Fit(this WorldView world, IObject3D itemToRender, RectangleDouble goalBounds, Matrix4X4 offset)
        {
            AxisAlignedBoundingBox meshBounds = itemToRender.GetAxisAlignedBoundingBox(offset);

            bool done = false;
            double scaleFraction = .1;
            // make the target size a portion of the total size
            goalBounds.Inflate(-goalBounds.Width * .1);

            int rescaleAttempts = 0;
            while (!done && rescaleAttempts++ < 500)
            {
                RectangleDouble partScreenBounds = world.GetScreenBounds(meshBounds);

                if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
                {
                    world.Scale *= 1 + scaleFraction;
                    partScreenBounds = world.GetScreenBounds(meshBounds);

                    // If it crossed over the goal reduct the amount we are adjusting by.
                    if (NeedsToBeSmaller(partScreenBounds, goalBounds))
                    {
                        scaleFraction /= 2;
                    }
                }
                else
                {
                    world.Scale *= 1 - scaleFraction;
                    partScreenBounds = world.GetScreenBounds(meshBounds);

                    // If it crossed over the goal reduct the amount we are adjusting by.
                    if (!NeedsToBeSmaller(partScreenBounds, goalBounds))
                    {
                        scaleFraction /= 2;
                        if (scaleFraction < .001)
                        {
                            done = true;
                        }
                    }
                }
            }
        }

        private static bool NeedsToBeSmaller(RectangleDouble partScreenBounds, RectangleDouble goalBounds)
        {
            if (partScreenBounds.Bottom < goalBounds.Bottom
                || partScreenBounds.Top > goalBounds.Top
                || partScreenBounds.Left < goalBounds.Left
                || partScreenBounds.Right > goalBounds.Right)
            {
                return true;
            }

            return false;
        }

        public static RectangleDouble GetScreenBounds(this WorldView world, AxisAlignedBoundingBox meshBounds)
        {
            RectangleDouble screenBounds = RectangleDouble.ZeroIntersection;

            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MinXYZ.X, meshBounds.MinXYZ.Y, meshBounds.MinXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MaxXYZ.X, meshBounds.MinXYZ.Y, meshBounds.MinXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MaxXYZ.X, meshBounds.MaxXYZ.Y, meshBounds.MinXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MinXYZ.X, meshBounds.MaxXYZ.Y, meshBounds.MinXYZ.Z)));

            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MinXYZ.X, meshBounds.MinXYZ.Y, meshBounds.MaxXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MaxXYZ.X, meshBounds.MinXYZ.Y, meshBounds.MaxXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MaxXYZ.X, meshBounds.MaxXYZ.Y, meshBounds.MaxXYZ.Z)));
            screenBounds.ExpandToInclude(world.GetScreenPosition(new Vector3(meshBounds.MinXYZ.X, meshBounds.MaxXYZ.Y, meshBounds.MaxXYZ.Z)));
            return screenBounds;
        }

        public static IEnumerable<IObject3D> GetPersistable(this IObject3D sourceItem, bool forceIntoCache = false)
        {
            return sourceItem.DescendantsAndSelf().Where(object3D =>
            {
                var needSave = object3D.MeshPath == null || forceIntoCache;
                var needSaveAndHasMesh = needSave && object3D.Mesh != null;
                var assetObjectAndForceCache = object3D is IAssetObject && forceIntoCache;
                var meshOrAssetNeedingSave = needSaveAndHasMesh || assetObjectAndForceCache;
                // Ignore items assigned the FileMissing mesh
                var invalidMesh = object3D.Mesh == Object3D.FileMissingMesh;
                return object3D.WorldPersistable()
                    && meshOrAssetNeedingSave
                    && !invalidMesh;
            });
        }

        public static async Task PersistAssets(this IObject3D sourceItem, Action<double, string> progress = null, bool forceIntoCache = false, bool publishAfterSave = false)
        {
            // Must use DescendantsAndSelf so that leaf nodes save their meshes
            var persistableItems = sourceItem.GetPersistable(forceIntoCache);

            Directory.CreateDirectory(Object3D.AssetsPath);

            var assetFiles = new Dictionary<ulong, string>();

            try
            {
                var persistCount = persistableItems.Count();
                var savedCount = 0;

                // Write unsaved content to disk
                foreach (IObject3D item in persistableItems)
                {
                    // If forceIntoCache is specified, persist any unsaved IAssetObject items to disk
                    if (item is IAssetObject assetObject && forceIntoCache)
                    {
                        await AssetObject3D.AssetManager.StoreAsset(assetObject, forceIntoCache, CancellationToken.None, progress);

                        if (string.IsNullOrWhiteSpace(item.MeshPath))
                        {
                            continue;
                        }
                    }

                    // Calculate the fast mesh hash
                    ulong hashCode = item.Mesh.GetLongHashCode();

                    // Index into dictionary using fast hash
                    if (!assetFiles.TryGetValue(hashCode, out string assetPath))
                    {
                        // Store and update cache if missing
                        await AssetObject3D.AssetManager.StoreMesh(item, publishAfterSave, CancellationToken.None, progress);
                        assetFiles.Add(hashCode, item.MeshPath);
                    }
                    else
                    {
                        // If the Mesh is in the assetFile cache, set .MeshPath to the existing asset name
                        item.MeshPath = assetPath;
                    }

                    savedCount++;
                    progress?.Invoke(savedCount / (double)persistCount, "");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error saving file: ", ex.Message);
            }
        }

        public static AxisAlignedBoundingBox GetUnionedAxisAlignedBoundingBox(this IEnumerable<IObject3D> items)
        {
            // first find the bounds of what is already here.
            AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty();
            foreach (var object3D in items)
            {
                totalBounds = AxisAlignedBoundingBox.Union(totalBounds, object3D.GetAxisAlignedBoundingBox());
            }

            return totalBounds;
        }

        public static Matrix4X4 WorldMatrix(this IObject3D child, IObject3D rootOverride = null, bool includingRoot = true)
        {
            var matrix = Matrix4X4.Identity;
            foreach (var item in child.AncestorsAndSelf())
            {
                if (!includingRoot
                    && item == rootOverride)
                {
                    // exit before the root item is included
                    break;
                }

                matrix *= item.Matrix;
                if (item == rootOverride)
                {
                    break;
                }
            }

            return matrix;
        }

        public static AxisAlignedBoundingBox WorldAxisAlignedBoundingBox(this IObject3D child)
        {
            return child.GetAxisAlignedBoundingBox(child.Parent.WorldMatrix());
        }

        public static IEnumerable<IObject3D> AncestorsAndSelf(this IObject3D item)
        {
            yield return item;
            var parent = item.Parent;
            while (parent != null)
            {
                yield return parent;
                parent = parent.Parent;
            }
        }

        public static Color WorldColor(this IObject3D child, IObject3D rootOverride = null, bool includingRoot = true, bool checkOutputType = false)
        {
            var lastColorFound = Color.White;
            foreach (var item in child.AncestorsAndSelf())
            {
                if (!includingRoot
                    && item == rootOverride)
                {
                    // exit before the root item is included
                    break;
                }

                // if we find a color it overrides our current color so set it
                if (item.Color.Alpha0To255 != 0)
                {
                    lastColorFound = item.Color;
                }

                if (checkOutputType)
                {
                    if (item.WorldOutputType() == PrintOutputTypes.Hole)
                    {
                        lastColorFound = Color.DarkGray.WithAlpha(120);
                    }
                }

                // If the root override has been matched, break and return latest
                if (item == rootOverride)
                {
                    break;
                }
            }

            return lastColorFound;
        }

        public static bool WorldPersistable(this IObject3D child, IObject3D rootOverride = null)
        {
            foreach (var item in child.AncestorsAndSelf())
            {
                if (!item.Persistable)
                {
                    return false;
                }

                // If the root override has been matched, break and return latest
                if (item == rootOverride)
                {
                    break;
                }
            }

            return true;
        }

        public static PrintOutputTypes WorldOutputType(this IObject3D child, IObject3D rootOverride = null, bool includingRoot = true)
        {
            var lastOutputTypeFound = PrintOutputTypes.Default;
            foreach (var item in child.AncestorsAndSelf())
            {
                if (!includingRoot
                    && item == rootOverride)
                {
                    // exit before the root item is included
                    break;
                }

                if (item.OutputType != PrintOutputTypes.Default)
                {
                    // use collection as the color for all recursive children
                    lastOutputTypeFound = item.OutputType;
                }

                // If the root override has been matched, break and return latest
                if (item == rootOverride)
                {
                    break;
                }
            }

            return lastOutputTypeFound;
        }

        public static bool WorldVisible(this IObject3D child, IObject3D rootOverride = null, bool includingRoot = true)
        {
            foreach (var item in child.AncestorsAndSelf())
            {
                if (!includingRoot
                    && item == rootOverride)
                {
                    // exit before the root item is included
                    break;
                }

                if (!item.Visible)
                {
                    return false;
                }

                // If the root override has been matched, break and return latest
                if (item == rootOverride)
                {
                    break;
                }
            }

            return true;
        }

        public class RebuildLocks : IDisposable
        {
            private readonly List<RebuildLock> rebuilLocks = new List<RebuildLock>();

            public RebuildLocks(IObject3D parent)
            {
                foreach (var item in parent.DescendantsAndSelf())
                {
                    rebuilLocks.Add(item.RebuildLock());
                }
            }

            public void Dispose()
            {
                foreach (var rebuildLock in rebuilLocks)
                {
                    rebuildLock.Dispose();
                }
            }
        }

        public static RebuildLocks RebuilLockAll(this IObject3D parent)
        {
            return new RebuildLocks(parent);
        }

        public static IEnumerable<IObject3D> DescendantsAndSelf(this IObject3D root)
        {
            var items = new Stack<IObject3D>(new[] { root });
            while (items.Count > 0)
            {
                IObject3D item = items.Pop();

                yield return item;
                foreach (var n in item.Children)
                {
                    items.Push(n);
                }
            }
        }

        /// <summary>
        /// Returns all Parents of the given object
        /// </summary>
        /// <param name="item">The context item</param>
        /// <returns>The matching parents items</returns>
        public static IEnumerable<IObject3D> Parents(this IObject3D root)
        {
            var context = root.Parent;
            while (context != null)
            {
                yield return context;

                context = context.Parent;
            }
        }

        public static IEnumerable<IObject3D> Descendants(this IObject3D root, Func<IObject3D, bool> processChildren = null)
        {
            return root.Descendants<IObject3D>(processChildren);
        }

        public static IEnumerable<T> Descendants<T>(this IObject3D root, Func<IObject3D, bool> processChildren = null) where T : IObject3D
        {
            var items = new Stack<IObject3D>();

            foreach (var n in root.Children)
            {
                items.Push(n);
            }

            while (items.Count > 0)
            {
                IObject3D item = items.Pop();

                if (item is T asType)
                {
                    yield return asType;
                }

                if (processChildren?.Invoke(item) != false)
                {
                    foreach (var n in item.Children)
                    {
                        items.Push(n);
                    }
                }
            }
        }

        public static void Rotate(this IObject3D item, Vector3 origin, Vector3 axis, double angle)
        {
            // move object relative to rotation
            item.Matrix *= Matrix4X4.CreateTranslation(-origin);
            // rotate it
            item.Matrix *= Matrix4X4.CreateRotation(axis, angle);
            // move it back
            item.Matrix *= Matrix4X4.CreateTranslation(origin);
        }

        /// <summary>
        /// Create a bounding volume hierarchy for the given mesh.
        /// </summary>
        /// <param name="mesh">The mesh to add the BVH to.</param>
        /// <returns>The created BVH tree.</returns>
        public static ITraceable CreateBVHData(this Mesh mesh, BvhCreationOptions bvhCreationOptions = BvhCreationOptions.BottomUpClustering)
        {
            // test new BvHBuilderAac
            // BvhBuilderAac.Create(mesh);

            var allPolys = new List<ITraceable>();

            mesh.AddTraceables(null, Matrix4X4.Identity, allPolys);

            return BoundingVolumeHierarchy.CreateNewHierachy(allPolys, bvhCreationOptions);
        }

        public static void AddTraceables(this Mesh mesh, MaterialAbstract material, Matrix4X4 matrix, List<ITraceable> tracePrimitives)
        {
            for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
            {
                var face = mesh.Faces[faceIndex];

                ITraceable triangle;
                if (material != null)
                {
                    triangle = new TriangleShape(
                        mesh.Vertices[face.v0].Transform(matrix),
                        mesh.Vertices[face.v1].Transform(matrix),
                        mesh.Vertices[face.v2].Transform(matrix),
                        material, faceIndex);
                }
                else
                {
                    triangle = new MinimalTriangle((fi, vi) =>
                    {
                        switch (vi)
                        {
                            case 0:
                                return mesh.Vertices[mesh.Faces[fi].v0];
                            case 1:
                                return mesh.Vertices[mesh.Faces[fi].v1];
                            default:
                                return mesh.Vertices[mesh.Faces[fi].v2];
                        }
                    }, faceIndex);
                }

                tracePrimitives.Add(triangle);
            }
        }

        /// <summary>
        /// Collapses the source object into the target list (often but not necessarily the scene)
        /// </summary>
        /// <param name="objectToCollapse">The object to collapse</param>
        /// <param name="collapseInto">The target to collapse into</param>
        /// <param name="filterToSelectionGroup">State if should filter</param>
        /// <param name="depth">The maximum times to recurse this function</param>
        public static void CollapseInto(this IObject3D objectToCollapse, List<IObject3D> collapseInto, bool filterToSelectionGroup = true, int depth = int.MaxValue)
        {
            if (objectToCollapse != null
                && objectToCollapse is SelectionGroupObject3D == filterToSelectionGroup)
            {
                // Remove the collapsing item from the list
                collapseInto.Remove(objectToCollapse);

                // Move each child from objectToCollapse into the target (often the scene), applying the parent transform to each
                foreach (var child in objectToCollapse.Children)
                {
                    if (objectToCollapse.Color != Color.Transparent)
                    {
                        child.Color = objectToCollapse.Color;
                    }

                    if (objectToCollapse.OutputType != PrintOutputTypes.Default)
                    {
                        child.OutputType = objectToCollapse.OutputType;
                    }

                    child.Matrix *= objectToCollapse.Matrix;

                    if (child is SelectionGroupObject3D && depth > 0)
                    {
                        child.CollapseInto(collapseInto, filterToSelectionGroup, depth - 1);
                    }
                    else
                    {
                        collapseInto.Add(child);
                    }
                }
            }
        }
    }
}