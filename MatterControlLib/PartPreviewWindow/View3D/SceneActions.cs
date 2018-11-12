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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
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

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class SceneActions
	{
		private static int pastObjectXOffset = 5;
		public static List<IObject3D> GetSelectedItems(this InteractiveScene scene)
		{
			var selectedItem = scene.SelectedItem;
			var selectedItems = new List<IObject3D>();
			if (selectedItem != null)
			{
				if (selectedItem is SelectionGroupObject3D)
				{
					selectedItems = selectedItem.Children.ToList();
				}
				else
				{
					selectedItems = new List<IObject3D> { selectedItem };
				}
			}

			return selectedItems;
		}

		public static async void UngroupSelection(this InteractiveScene scene)
		{
			var selectedItem = scene.SelectedItem;
			if (selectedItem != null)
			{
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
						(reporter, cancellationToken) =>
						{
							var progressStatus = new ProgressStatus();
							reporter.Report(progressStatus);
							// clear the selection
							scene.SelectedItem = null;
							progressStatus.Status = "Copy".Localize();
							reporter.Report(progressStatus);
							var ungroupMesh = selectedItem.Mesh.Copy(cancellationToken, (progress0To1, processingState) =>
							{
								progressStatus.Progress0To1 = progress0To1 * .2;
								progressStatus.Status = processingState;
								reporter.Report(progressStatus);
							});
							if(cancellationToken.IsCancellationRequested)
							{
								return Task.CompletedTask;
							}
							progressStatus.Status = "Clean".Localize();
							reporter.Report(progressStatus);
							ungroupMesh.CleanAndMergeMesh(cancellationToken, 0, (progress0To1, processingState) =>
							{
								progressStatus.Progress0To1 = .2 + progress0To1 * .3;
								progressStatus.Status = processingState;
								reporter.Report(progressStatus);
							});
							if (cancellationToken.IsCancellationRequested)
							{
								return Task.CompletedTask;
							}
							using (selectedItem.RebuildLock())
							{
								selectedItem.Mesh = ungroupMesh;
							}

							// try to cut it up into multiple meshes
							progressStatus.Status = "Split".Localize();
							var discreetMeshes = CreateDiscreteMeshes.SplitVolumesIntoMeshes(ungroupMesh, cancellationToken, (double progress0To1, string processingState) =>
							{
								progressStatus.Progress0To1 = .5 + progress0To1 * .5;
								progressStatus.Status = processingState;
								reporter.Report(progressStatus);
							});
							if (cancellationToken.IsCancellationRequested)
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
							List<IObject3D> addItems = new List<IObject3D>(discreetMeshes.Select(mesh => new Object3D()
							{
								Mesh = mesh,
							}));

							foreach(var item in addItems)
							{
								item.CopyProperties(selectedItem, Object3DPropertyFlags.All);
								item.Visible = true;
							}

							// add and do the undo data
							scene.UndoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { selectedItem }, addItems));

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

		public static async Task AutoArrangeChildren(this InteractiveScene scene, Vector3 bedCenter)
		{
			await Task.Run(() =>
			{
				PlatingHelper.ArrangeOnBed(scene.Children.ToList(), scene, bedCenter);
			});
		}

		public static void Cut(this InteractiveScene scene, IObject3D sourceItem = null)
		{
			var selectedItem = scene.SelectedItem;
			if (selectedItem != null)
			{
				Clipboard.Instance.SetText("!--IObjectSelection--!");
				ApplicationController.ClipboardItem = selectedItem.Clone();
				// put it back in right where we cut it from
				pastObjectXOffset = 0;

				scene.DeleteSelection();
			}
		}

		public static void Copy(this InteractiveScene scene, IObject3D sourceItem = null)
		{
			var selectedItem = scene.SelectedItem;
			if (selectedItem != null)
			{
				Clipboard.Instance.SetText("!--IObjectSelection--!");
				ApplicationController.ClipboardItem = selectedItem.Clone();
				// when we copy an object put it back in with a slight offset
				pastObjectXOffset = 5;
			}
		}

		public static void Paste(this BedConfig sceneContext)
		{
			var scene = sceneContext.Scene;

			if (Clipboard.Instance.ContainsImage)
			{
				// Persist
				string filePath = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".png");
				AggContext.ImageIO.SaveImageData(
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
					sceneContext.DuplicateItem(pastObjectXOffset, ApplicationController.ClipboardItem);
					// each time we put in the object offset it a bit more
					pastObjectXOffset += 5;
				}
			}
		}

		public static async void DuplicateItem(this BedConfig sceneContext, double xOffset, IObject3D sourceItem = null)
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
					if (sourceItem != null)
					{
						if (sourceItem is SelectionGroupObject3D)
						{
							// the selection is a group of objects that need to be copied
							var copyList = sourceItem.Children.ToList();
							scene.SelectedItem = null;
							foreach(var item in copyList)
							{
								var clonedItem = item.Clone();
								clonedItem.Translate(xOffset);
								// make the name unique
								var newName = agg_basics.GetNonCollidingName(item.Name, scene.DescendantsAndSelf().Select((d) => d.Name));
								clonedItem.Name = newName;
								// add it to the scene
								scene.Children.Add(clonedItem);
								// add it to the selection
								scene.AddToSelection(clonedItem);
							}
						}
						else // the selection can be cloned easily
						{
							var clonedItem = sourceItem.Clone();

							clonedItem.Translate(xOffset);
							// make the name unique
							var newName = agg_basics.GetNonCollidingName(sourceItem.Name, scene.DescendantsAndSelf().Select((d) => d.Name));
							clonedItem.Name = newName;

							// More useful if it creates the part in the exact position and then the user can move it.
							// Consistent with other software as well. LBB 2017-12-02
							//PlatingHelper.MoveToOpenPositionRelativeGroup(clonedItem, Scene.Children);

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

		public static void InsertNewItem(this BedConfig sceneContext, IObject3D newItem)
		{
			var scene = sceneContext.Scene;

			// Reposition first item to bed center
			if (scene.Children.Count == 0)
			{
				var aabb = newItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				var center = aabb.Center;

				newItem.Matrix *= Matrix4X4.CreateTranslation(
					(sceneContext.BedCenter.X - center.X),
					(sceneContext.BedCenter.Y - center.Y),
					 -aabb.minXYZ.Z);
			}

			// Create and perform a new insert operation
			var insertOperation = new InsertCommand(scene, newItem);
			insertOperation.Do();

			// Store the operation for undo/redo
			scene.UndoBuffer.Add(insertOperation);
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
			}
		}

		public static void MakeLowestFaceFlat(this InteractiveScene scene, IObject3D objectToLayFlatGroup)
		{
			bool firstVertex = true;

			IObject3D objectToLayFlat = objectToLayFlatGroup;

			IVertex lowestVertex = null;
			Vector3 lowestVertexPosition = Vector3.Zero;
			IObject3D itemToLayFlat = null;

			// Process each child, checking for the lowest vertex
			foreach (var itemToCheck in objectToLayFlat.VisibleMeshes())
			{
				var meshToCheck = itemToCheck.Mesh.GetConvexHull(false);

				if(meshToCheck == null
					&& meshToCheck.Vertices.Count < 3)
				{
					continue;
				}

				// find the lowest point on the model
				for (int testIndex = 0; testIndex < meshToCheck.Vertices.Count; testIndex++)
				{
					var vertex = meshToCheck.Vertices[testIndex];
					Vector3 vertexPosition = Vector3.Transform(vertex.Position, itemToCheck.WorldMatrix());
					if (firstVertex)
					{
						lowestVertex = meshToCheck.Vertices[testIndex];
						lowestVertexPosition = vertexPosition;
						itemToLayFlat = itemToCheck;
						firstVertex = false;
					}
					else if (vertexPosition.Z < lowestVertexPosition.Z)
					{
						lowestVertex = meshToCheck.Vertices[testIndex];
						lowestVertexPosition = vertexPosition;
						itemToLayFlat = itemToCheck;
					}
				}
			}

			if (lowestVertex == null)
			{
				// didn't find any selected mesh
				return;
			}

			PolygonMesh.Face faceToLayFlat = null;
			double lowestAngleOfAnyFace = double.MaxValue;
			// Check all the faces that are connected to the lowest point to find out which one to lay flat.
			foreach (var face in lowestVertex.ConnectedFaces())
			{
				double biggestAngleToFaceVertex = double.MinValue;
				foreach (IVertex faceVertex in face.Vertices())
				{
					if (faceVertex != lowestVertex)
					{
						Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, itemToLayFlat.WorldMatrix());
						Vector3 pointRelLowest = faceVertexPosition - lowestVertexPosition;
						double xLeg = new Vector2(pointRelLowest.X, pointRelLowest.Y).Length;
						double yLeg = pointRelLowest.Z;
						double angle = Math.Atan2(yLeg, xLeg);
						if (angle > biggestAngleToFaceVertex)
						{
							biggestAngleToFaceVertex = angle;
						}
					}
				}
				if (biggestAngleToFaceVertex < lowestAngleOfAnyFace)
				{
					lowestAngleOfAnyFace = biggestAngleToFaceVertex;
					faceToLayFlat = face;
				}
			}

			double maxDistFromLowestZ = 0;
			List<Vector3> faceVertices = new List<Vector3>();
			foreach (IVertex vertex in faceToLayFlat.Vertices())
			{
				Vector3 vertexPosition = Vector3.Transform(vertex.Position, itemToLayFlat.WorldMatrix());
				faceVertices.Add(vertexPosition);
				maxDistFromLowestZ = Math.Max(maxDistFromLowestZ, vertexPosition.Z - lowestVertexPosition.Z);
			}

			if (maxDistFromLowestZ > .001)
			{
				Vector3 xPositive = (faceVertices[1] - faceVertices[0]).GetNormal();
				Vector3 yPositive = (faceVertices[2] - faceVertices[0]).GetNormal();
				Vector3 planeNormal = Vector3.Cross(xPositive, yPositive).GetNormal();

				// this code takes the minimum rotation required and looks much better.
				Quaternion rotation = new Quaternion(planeNormal, new Vector3(0, 0, -1));
				Matrix4X4 partLevelMatrix = Matrix4X4.CreateRotation(rotation);

				// rotate it
				objectToLayFlat.Matrix = objectToLayFlatGroup.ApplyAtBoundsCenter(partLevelMatrix);
			}

			PlatingHelper.PlaceOnBed(objectToLayFlatGroup);
		}

		internal class ArrangeUndoCommand : IUndoRedoCommand
		{
			private List<TransformCommand> allUndoTransforms = new List<TransformCommand>();

			public ArrangeUndoCommand(View3DWidget view3DWidget, List<Matrix4X4> preArrangeTarnsforms, List<Matrix4X4> postArrangeTarnsforms)
			{
				for (int i = 0; i < preArrangeTarnsforms.Count; i++)
				{
					//allUndoTransforms.Add(new TransformUndoCommand(view3DWidget, i, preArrangeTarnsforms[i], postArrangeTarnsforms[i]));
				}
			}

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
