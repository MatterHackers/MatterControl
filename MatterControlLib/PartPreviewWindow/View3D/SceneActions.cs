﻿/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public static class SceneActions
    {
        private static int pasteObjectXOffset = 5;

        public static void AddPhilToBed(this ISceneContext sceneContext)
        {
            var philStl = StaticData.Instance.MapPath(Path.Combine("OEMSettings", "SampleParts", "Phil A Ment.stl"));
            sceneContext.AddToPlate(new string[] { philStl }, false);
        }

        public static void AddTransformSnapshot(this InteractiveScene scene, Matrix4X4 originalTransform)
        {
            var selectedItem = scene.SelectedItem;
            if (selectedItem != null && selectedItem.Matrix != originalTransform)
            {
                if (!string.IsNullOrEmpty(selectedItem.CloneID))
                {
                    selectedItem.CloneUpdateCount++;
                    selectedItem.Invalidate(new InvalidateArgs(selectedItem, InvalidateType.Matrix));
                }
                scene.UndoBuffer.Add(new TransformCommand(selectedItem, originalTransform, selectedItem.Matrix));
            }
        }

        public static async Task AutoArrangeChildren(this InteractiveScene scene, Vector3 bedCenter)
        {
            await Task.Run(() =>
            {
                // Clear selection to ensure all root level children are arranged on the bed
                using (new SelectionMaintainer(scene))
                {
                    var children = scene.Children.ToList().Where(item =>
                    {
                        var aabb = item.WorldAxisAlignedBoundingBox();
                        if (aabb.Center.Length > 1000)
                        {
                            return true;
                        }

                        return item.Persistable == true;
                    }).ToList();
                    var transformData = new List<TransformData>();
                    foreach (var child in children)
                    {
                        transformData.Add(new TransformData() { TransformedObject = child, UndoTransform = child.Matrix });
                    }

                    PlatingHelper.ArrangeOnBed(children, bedCenter, PlatingHelper.PositionType.Center);
                    int i = 0;
                    foreach (var child in children)
                    {
                        transformData[i].RedoTransform = child.Matrix;
                        i++;
                    }

                    scene.UndoBuffer.Add(new TransformCommand(transformData));
                }
            });
        }

        public static void Copy(this InteractiveScene scene, IObject3D sourceItem = null)
        {
            var selectedItem = scene.SelectedItem;
            if (selectedItem != null)
            {
                Clipboard.Instance.SetText("!--IObjectSelection--!");
                ApplicationController.ClipboardItem = selectedItem.DeepCopy();
                // when we copy an object put it back in with a slight offset
                pasteObjectXOffset = 5;
            }
        }

        public static void Cut(this InteractiveScene scene, IObject3D sourceItem = null)
        {
            var selectedItem = scene.SelectedItem;
            if (selectedItem != null)
            {
                Clipboard.Instance.SetText("!--IObjectSelection--!");
                ApplicationController.ClipboardItem = selectedItem.DeepCopy();
                // put it back in right where we cut it from
                pasteObjectXOffset = 0;

                scene.DeleteSelection();
            }
        }

        public static void DeleteSelection(this InteractiveScene scene)
        {
            var selectedItem = scene.SelectedItem;
            if (selectedItem != null)
            {
                // Create and perform the delete operation
                var deleteOperation = new DeleteCommand(scene, selectedItem);

                // Store the operation for undo/redo
                scene.UndoBuffer.AddAndDo(deleteOperation);

                scene.ClearSelection();

                scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));
            }
        }

        public static async void DuplicateItemAddToScene(this ISceneContext sceneContext, double xOffset, IObject3D sourceItem = null)
        {
            var scene = sceneContext.Scene;
            if (sourceItem == null)
            {
                var selectedItem = scene.SelectedItem;
                if (selectedItem != null)
                {
                    sourceItem = selectedItem;
                }
            }

            if (sourceItem != null)
            {
                // Copy selected item
                IObject3D newItem = await Task.Run(() =>
                {
                    var namedItems = new HashSet<string>(scene.DescendantsAndSelf().Select((d) => d.Name));
                    if (sourceItem != null)
                    {
                        if (sourceItem is SelectionGroupObject3D)
                        {
                            // the selection is a group of objects that need to be copied
                            var copyList = sourceItem.Children.ToList();
                            scene.SelectedItem = null;
                            foreach (var item in copyList)
                            {
                                var clonedItem = item.DeepCopy();
                                clonedItem.Translate(xOffset);
                                // make the name unique
                                var newName = Util.GetNonCollidingName(item.Name, namedItems);
                                clonedItem.Name = newName;
                                // add it to the scene
                                scene.Children.Add(clonedItem);
                                // add it to the selection
                                scene.AddToSelection(clonedItem);
                            }
                        }
                        else // the selection can be cloned easily
                        {
                            var clonedItem = sourceItem.DeepCopy();

                            clonedItem.Translate(xOffset);
                            // an empty string is used to denote special name processing for some container types
                            if (!string.IsNullOrWhiteSpace(sourceItem.Name))
                            {
                                // make the name unique
                                var newName = Util.GetNonCollidingName(sourceItem.Name, namedItems);
                                clonedItem.Name = newName;
                            }

                            // More useful if it creates the part in the exact position and then the user can move it.
                            // Consistent with other software as well. LBB 2017-12-02
                            // PlatingHelper.MoveToOpenPositionRelativeGroup(clonedItem, Scene.Children);

                            return clonedItem;
                        }
                    }

                    return null;
                });

                // it might come back null due to threading
                if (newItem != null)
                {
                    sceneContext.InsertNewItem(newItem);
                }
            }
        }

        public static void InsertNewItem(this ISceneContext sceneContext, IObject3D newItem)
        {
            var scene = sceneContext.Scene;

            // Reposition first item to bed center
            if (scene.Children.Count == 0)
            {
                var aabb = newItem.GetAxisAlignedBoundingBox();
                var center = aabb.Center;

                newItem.Matrix *= Matrix4X4.CreateTranslation(
                    sceneContext.BedCenter.X - center.X,
                    sceneContext.BedCenter.Y - center.Y,
                    -aabb.MinXYZ.Z);
            }

            // Create and perform a new insert operation
            var insertOperation = new InsertCommand(scene, newItem);
            insertOperation.Do();

            // Store the operation for undo/redo
            scene.UndoBuffer.Add(insertOperation);
        }

        public static void MakeLowestFaceFlat(this InteractiveScene scene, IObject3D objectToLayFlatGroup)
        {
            var preLayFlatMatrix = objectToLayFlatGroup.Matrix;

            bool firstVertex = true;

            IObject3D objectToLayFlat = objectToLayFlatGroup;

            Vector3Float lowestPosition = Vector3Float.PositiveInfinity;
            Vector3Float sourceVertexPosition = Vector3Float.NegativeInfinity;
            IObject3D itemToLayFlat = null;
            Mesh meshWithLowest = null;

            var items = objectToLayFlat.VisibleMeshes().Where(i => i.OutputType != PrintOutputTypes.Hole);
            if (!items.Any())
            {
                items = objectToLayFlat.VisibleMeshes();
            }

            // Process each child, checking for the lowest vertex
            foreach (var itemToCheck in items)
            {
                var meshToCheck = itemToCheck.Mesh.GetConvexHull(false);

                if (meshToCheck == null
                    && meshToCheck.Vertices.Count < 3)
                {
                    continue;
                }

                var maxArea = 0.0;
                // find the lowest point on the model
                for (int testFace = 0; testFace < meshToCheck.Faces.Count; testFace++)
                {
                    var face = meshToCheck.Faces[testFace];
                    var vertex = meshToCheck.Vertices[face.v0];
                    var vertexPosition = vertex.Transform(itemToCheck.WorldMatrix());
                    if (firstVertex)
                    {
                        meshWithLowest = meshToCheck;
                        lowestPosition = vertexPosition;
                        sourceVertexPosition = vertex;
                        itemToLayFlat = itemToCheck;
                        firstVertex = false;
                    }
                    else if (vertexPosition.Z < lowestPosition.Z)
                    {
                        if (Math.Abs(vertexPosition.Z - lowestPosition.Z) < .001)
                        {
                            // check if this face has a bigger area than the other face that is also this low
                            var faceArea = face.GetArea(meshToCheck);
                            if (faceArea > maxArea)
                            {
                                maxArea = faceArea;
                                meshWithLowest = meshToCheck;
                                lowestPosition = vertexPosition;
                                sourceVertexPosition = vertex;
                                itemToLayFlat = itemToCheck;
                            }
                        }
                        else
                        {
                            // reset the max area
                            maxArea = face.GetArea(meshToCheck);
                            meshWithLowest = meshToCheck;
                            lowestPosition = vertexPosition;
                            sourceVertexPosition = vertex;
                            itemToLayFlat = itemToCheck;
                        }
                    }
                }
            }

            if (meshWithLowest == null)
            {
                // didn't find any selected mesh
                return;
            }

            int faceToLayFlat = -1;
            double largestAreaOfAnyFace = 0;
            var facesSharingLowestVertex = meshWithLowest.Faces
                .Select((face, i) => new { face, i })
                .Where(faceAndIndex => meshWithLowest.Vertices[faceAndIndex.face.v0] == sourceVertexPosition
                    || meshWithLowest.Vertices[faceAndIndex.face.v1] == sourceVertexPosition
                    || meshWithLowest.Vertices[faceAndIndex.face.v2] == sourceVertexPosition)
                .Select(j => j.i);

            var lowestFacesByAngle = facesSharingLowestVertex.OrderBy(i =>
            {
                var face = meshWithLowest.Faces[i];
                var worldNormal = face.normal.TransformNormal(itemToLayFlat.WorldMatrix());
                return worldNormal.CalculateAngle(-Vector3Float.UnitZ);
            });

            // Check all the faces that are connected to the lowest point to find out which one to lay flat.
            foreach (var faceIndex in lowestFacesByAngle)
            {
                var face = meshWithLowest.Faces[faceIndex];

                var worldNormal = face.normal.TransformNormal(itemToLayFlat.WorldMatrix());
                var worldAngleDegrees = MathHelper.RadiansToDegrees(worldNormal.CalculateAngle(-Vector3Float.UnitZ));

                double largestAreaFound = 0;
                var faceVeretexIndices = new int[] { face.v0, face.v1, face.v2 };

                foreach (var vi in faceVeretexIndices)
                {
                    if (meshWithLowest.Vertices[vi] != lowestPosition)
                    {
                        var planSurfaceArea = 0.0;
                        foreach (var coPlanarFace in meshWithLowest.GetCoplanarFaces(faceIndex))
                        {
                            planSurfaceArea += meshWithLowest.GetSurfaceArea(coPlanarFace);
                        }

                        if (largestAreaOfAnyFace == 0
                            || (planSurfaceArea > largestAreaFound
                                && worldAngleDegrees < 45))
                        {
                            largestAreaFound = planSurfaceArea;
                        }
                    }
                }

                if (largestAreaFound > largestAreaOfAnyFace)
                {
                    largestAreaOfAnyFace = largestAreaFound;
                    faceToLayFlat = faceIndex;
                }
            }

            double maxDistFromLowestZ = 0;
            var lowestFace = meshWithLowest.Faces[faceToLayFlat];
            var lowestFaceIndices = new int[] { lowestFace.v0, lowestFace.v1, lowestFace.v2 };
            var faceVertices = new List<Vector3Float>();
            foreach (var vertex in lowestFaceIndices)
            {
                var vertexPosition = meshWithLowest.Vertices[vertex].Transform(itemToLayFlat.WorldMatrix());
                faceVertices.Add(vertexPosition);
                maxDistFromLowestZ = Math.Max(maxDistFromLowestZ, vertexPosition.Z - lowestPosition.Z);
            }

            if (maxDistFromLowestZ > .001)
            {
                var xPositive = (faceVertices[1] - faceVertices[0]).GetNormal();
                var yPositive = (faceVertices[2] - faceVertices[0]).GetNormal();
                var planeNormal = xPositive.Cross(yPositive).GetNormal();

                // this code takes the minimum rotation required and looks much better.
                var rotation = new Quaternion(planeNormal, new Vector3Float(0, 0, -1));
                var partLevelMatrix = Matrix4X4.CreateRotation(rotation);

                // rotate it
                objectToLayFlat.Matrix = objectToLayFlatGroup.ApplyAtBoundsCenter(partLevelMatrix);
            }

            if (objectToLayFlatGroup is Object3D object3D)
            {
                AxisAlignedBoundingBox bounds = object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity, (item) =>
                {
                    return item.OutputType != PrintOutputTypes.Hole;
                });
                Vector3 boundsCenter = (bounds.MaxXYZ + bounds.MinXYZ) / 2;

                object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.Z + bounds.ZSize / 2));
            }
            else
            {
                PlatingHelper.PlaceOnBed(objectToLayFlatGroup);
            }

            scene.UndoBuffer.Add(new TransformCommand(objectToLayFlatGroup, preLayFlatMatrix, objectToLayFlatGroup.Matrix));
        }

        public static void Paste(this ISceneContext sceneContext)
        {
            var scene = sceneContext.Scene;

            if (Clipboard.Instance.ContainsImage)
            {
                // Persist
                string filePath = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".png");
                ImageIO.SaveImageData(
                    filePath,
                    Clipboard.Instance.GetImage());

                scene.UndoBuffer.AddAndDo(
                    new InsertCommand(
                        scene,
                        new ImageObject3D()
                        {
                            AssetPath = filePath
                        }));
            }
            else if (Clipboard.Instance.ContainsText)
            {
                if (Clipboard.Instance.GetText() == "!--IObjectSelection--!")
                {
                    sceneContext.DuplicateItemAddToScene(0, ApplicationController.ClipboardItem);
                    // each time we put in the object offset it a bit more
                    pasteObjectXOffset += 5;
                }
            }
        }

        public static void PasteIntoSelection(this ISceneContext sceneContext)
        {
            var scene = sceneContext.Scene;
            var selectedItem = scene.SelectedItem;
            var clipboardItem = ApplicationController.ClipboardItem;

            if (Clipboard.Instance.ContainsText
                && Clipboard.Instance.GetText() == "!--IObjectSelection--!"
                // there is a selected item to paste into
                && selectedItem != null
                // the selected item is not a primitve
                && !(selectedItem is PrimitiveObject3D)
                && clipboardItem != null)
            {
                if (selectedItem is OperationSourceContainerObject3D sourceContainer)
                {
                    sourceContainer.SourceContainer.Children.Add(clipboardItem.DeepCopy());
                }
                else
                {
                    selectedItem.Children.Add(clipboardItem.DeepCopy());
                }
            }
        }

        public static async void UngroupSelection(this InteractiveScene scene)
        {
            var selectedItem = scene.SelectedItem;
            if (selectedItem != null)
            {
                if (selectedItem.CanApply)
                {
                    selectedItem.Apply(scene.UndoBuffer);
                    scene.SelectedItem = null;
                    return;
                }

                bool isGroupItemType = selectedItem.Children.Count > 0;

                // If not a Group ItemType, look for mesh volumes and split into distinct objects if found
                if (isGroupItemType)
                {
                    // Create and perform the delete operation
                    // Store the operation for undo/redo
                    scene.UndoBuffer.AddAndDo(new UngroupCommand(scene, selectedItem));
                }
                else if (!selectedItem.HasChildren()
                    && selectedItem.Mesh != null)
                {
                    await ApplicationController.Instance.Tasks.Execute(
                        "Ungroup".Localize(),
                        null,
                        (reporter, cancellationTokenSource) =>
                        {
                            // clear the selection
                            scene.SelectedItem = null;
                            var status = "Copy".Localize();
                            reporter?.Invoke(0, status);

                            // try to cut it up into multiple meshes
                            status = "Split".Localize();

                            var cleanMesh = selectedItem.Mesh.Copy(cancellationTokenSource.Token);
                            cleanMesh.MergeVertices(.01);

                            var discreetMeshes = CreateDiscreteMeshes.SplitVolumesIntoMeshes(cleanMesh, cancellationTokenSource.Token, (double progress0To1, string processingState) =>
                            {
                                status = processingState;
                                reporter?.Invoke(.5 + progress0To1 * .5, status);
                            });
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                return Task.CompletedTask;
                            }

                            if (discreetMeshes.Count == 1)
                            {
                                // restore the selection
                                scene.SelectedItem = selectedItem;
                                // No further processing needed, nothing to ungroup
                                return Task.CompletedTask;
                            }

                            // build the ungroup list
                            var addItems = new List<IObject3D>(discreetMeshes.Select(mesh => new Object3D()
                            {
                                Mesh = mesh,
                            }));

                            foreach (var item in addItems)
                            {
                                item.CopyProperties(selectedItem, Object3DPropertyFlags.All);
                                item.Visible = true;
                            }

                            // add and do the undo data
                            scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, addItems));

                            foreach (var item in addItems)
                            {
                                item.MakeNameNonColliding();
                            }

                            return Task.CompletedTask;
                        });
                }

                // leave no selection
                scene.SelectedItem = null;
            }
        }

        internal class ArrangeUndoCommand : IUndoRedoCommand
        {
            private List<TransformCommand> allUndoTransforms = new List<TransformCommand>();

            public ArrangeUndoCommand(View3DWidget view3DWidget, List<Matrix4X4> preArrangeTarnsforms, List<Matrix4X4> postArrangeTarnsforms)
            {
                for (int i = 0; i < preArrangeTarnsforms.Count; i++)
                {
                    //a llUndoTransforms.Add(new TransformUndoCommand(view3DWidget, i, preArrangeTarnsforms[i], postArrangeTarnsforms[i]));
                }
            }

            public string Name => "Arrange".Localize();

            public void Do()
            {
                for (int i = 0; i < allUndoTransforms.Count; i++)
                {
                    allUndoTransforms[i].Do();
                }
            }

            public void Undo()
            {
                for (int i = 0; i < allUndoTransforms.Count; i++)
                {
                    allUndoTransforms[i].Undo();
                }
            }
        }
    }
}