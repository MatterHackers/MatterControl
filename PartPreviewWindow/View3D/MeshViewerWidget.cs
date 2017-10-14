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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
	public enum BedShape { Rectangular, Circular };

	public class DrawGlContentEventArgs : DrawEventArgs
	{
		public bool ZBuffered { get; }

		public DrawGlContentEventArgs(bool zBuffered, DrawEventArgs e)
			: base(e.graphics2D)
		{
			ZBuffered = zBuffered;
		}
	}

	public static class MatterialRendering
	{
		public static RGBA_Bytes Color(int materialIndex)
		{
			return RGBA_Floats.FromHSL(Math.Max(materialIndex, 0) / 10.0, .99, .49).GetAsRGBA_Bytes();
		}
	}

	public class MeshViewerWidget : GuiWidget
	{
		// TODO: Need to be instance based for multi-printer
		public GuiWidget ParentSurface { get; set; }

		private RenderTypes renderType = RenderTypes.Shaded;

		private InteractionLayer interactionLayer;

		private BedConfig sceneContext;

		private double selectionHighlightWidth = 5;

		public MeshViewerWidget(BedConfig sceneContext, InteractionLayer interactionLayer, string startingTextMessage = "", EditorType editorType = EditorType.Part)
		{
			this.EditorMode = editorType;
			this.scene = sceneContext.Scene;
			this.sceneContext = sceneContext;
			this.interactionLayer = interactionLayer;
			this.World = interactionLayer.World;
			
			scene.SelectionChanged += (sender, e) =>
			{
				Invalidate();
			};
			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			BedColor = new RGBA_Floats(.8, .8, .8, .7).GetAsRGBA_Bytes();
			BuildVolumeColor = new RGBA_Floats(.2, .8, .3, .2).GetAsRGBA_Bytes();

			this.interactionLayer.DrawGlOpaqueContent += Draw_GlOpaqueContent;
			this.interactionLayer.DrawGlTransparentContent += Draw_GlTransparentContent;
		}

		public override void OnParentChanged(EventArgs e)
		{
			this.ParentSurface = this.Parent;
			base.OnParentChanged(e);
		}

		public WorldView World { get; }

		public event EventHandler LoadDone;

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public RGBA_Bytes BedColor { get; set; }

		public RGBA_Bytes BuildVolumeColor { get; set; }

		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(List<MeshGroup> meshGroups)
		{
			AxisAlignedBoundingBox totalMeshBounds = AxisAlignedBoundingBox.Empty;
			bool first = true;
			foreach (MeshGroup meshGroup in meshGroups)
			{
				AxisAlignedBoundingBox meshBounds = meshGroup.GetAxisAlignedBoundingBox();
				if (first)
				{
					totalMeshBounds = meshBounds;
					first = false;
				}
				else
				{
					totalMeshBounds = AxisAlignedBoundingBox.Union(totalMeshBounds, meshBounds);
				}
			}

			return totalMeshBounds;
		}

		public override void OnLoad(EventArgs args)
		{
			// some debug code to be able to click on parts
			if (false)
			{
				AfterDraw += (sender, e) =>
				{
					foreach (var child in scene.Children)
					{
						this.World.RenderDebugAABB(e.graphics2D, child.TraceData().GetAxisAlignedBoundingBox());
						this.World.RenderDebugAABB(e.graphics2D, child.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
					}
				};
			}

			base.OnLoad(args);
		}

		public override void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (var child in scene.Children)
			{
				string object3DName = child.Name;
				if (object3DName == null && child.MeshPath != null)
				{
					object3DName = Path.GetFileName(child.MeshPath);
				}

				bool nameFound = false;

				if (seachType == SearchType.Exact)
				{
					if (object3DName == nameToSearchFor)
					{
						nameFound = true;
					}
				}
				else
				{
					if (nameToSearchFor == ""
						|| object3DName.Contains(nameToSearchFor))
					{
						nameFound = true;
					}
				}

				if (nameFound)
				{
					AxisAlignedBoundingBox bounds = child.TraceData().GetAxisAlignedBoundingBox();

					RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
					for(int i=0; i<4; i++)
					{
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetTopCorner(i)));
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetBottomCorner(i)));
					}

					if (touchingBounds.IsTouching(screenBoundsOfObject3D))
					{
						Vector3 renderPosition = bounds.Center;
						Vector2 objectCenterScreenSpace = this.World.GetScreenPosition(renderPosition);
						Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.x, (int)objectCenterScreenSpace.y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName));
					}
				}
			}

			base.FindNamedChildrenRecursive(nameToSearchFor, foundChildren, touchingBounds, seachType, allowInvalidItems);
		}

		protected InteractiveScene scene { get; }

		public bool RenderBed { get; set; }

		public bool RenderBuildVolume { get; set; }

		public RenderTypes RenderType
		{
			get => this.IsActive ? renderType : RenderTypes.Wireframe;
			set
			{
				if (renderType != value)
				{
					renderType = value;
					foreach(var renderTransfrom in scene.VisibleMeshes())
					{
						renderTransfrom.Mesh.MarkAsChanged();
					}
				}
			}
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		public static RGBA_Bytes GetExtruderColor(int extruderIndex)
		{
			return MatterialRendering.Color(extruderIndex);
		}

		public void CreateGlDataObject(IObject3D item)
		{
			if(item.Mesh != null)
			{
				GLMeshTrianglePlugin.Get(item.Mesh);
			}

			foreach (IObject3D child in item.Children.Where(o => o.Mesh != null))
			{
				GLMeshTrianglePlugin.Get(child.Mesh);
			}
		}

		public bool SuppressUiVolumes { get; set; } = false;

		private CancellationTokenSource fileLoadCancellationTokenSource;

		public async Task LoadItemIntoScene(string itemPath, Vector2 bedCenter = new Vector2(), string itemName = null)
		{
			if (File.Exists(itemPath))
			{
				fileLoadCancellationTokenSource = new CancellationTokenSource();

				// TODO: How to we handle mesh load errors? How do we report success?
				IObject3D loadedItem = await Task.Run(() => Object3D.Load(itemPath, fileLoadCancellationTokenSource.Token));
				if (loadedItem != null)
				{
					if (itemName != null)
					{
						loadedItem.Name = itemName;
					}

					// SetMeshAfterLoad
					scene.Children.Modify(list =>
					{
						if (loadedItem.Mesh != null)
						{
							// STLs currently load directly into the mesh rather than as a group like AMF
							list.Add(loadedItem);
						}
						else
						{
							list.AddRange(loadedItem.Children);
						}
					});

					CreateGlDataObject(loadedItem);
				}
				else
				{
					// TODO: Error message container moved to Interaction Layer - how could we support this type of error for a loaded scene item?
					//partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
				}

				// Invoke LoadDone event
				LoadDone?.Invoke(this, null);
			}
			else
			{
				// TODO: Error message container moved to Interaction Layer - how could we support this type of error for a loaded scene item?
				//partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(itemPath));
			}

			fileLoadCancellationTokenSource = null;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			fileLoadCancellationTokenSource?.Cancel();
			base.OnClosed(e);
		}

		public bool IsActive { get; set; } = true;

		private void DrawObject(IObject3D object3D, List<MeshRenderData> transparentMeshes, bool parentSelected, DrawEventArgs e)
		{
			var totalVertices = 0;

			foreach (var renderData in object3D.VisibleMeshes())
			{
				totalVertices += renderData.Mesh.Vertices.Count;

				if (totalVertices > 1000)
				{
					break;
				}
			}

			bool tooBigForComplexSelection = totalVertices > 1000;
			if (tooBigForComplexSelection
				&& scene.HasSelection
				&& (object3D == scene.SelectedItem || scene.SelectedItem.Children.Contains(object3D)))
			{
				GLHelper.PrepareFor3DLineRender(true);
				RenderAABB(World.GetClippingFrustum(), object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity), Matrix4X4.Identity, RGBA_Bytes.White, selectionHighlightWidth);
				GL.Enable(EnableCap.Lighting);
			}

			foreach (var renderData in object3D.VisibleMeshes())
			{
				bool isSelected = parentSelected ||
					scene.HasSelection && (object3D == scene.SelectedItem || scene.SelectedItem.Children.Contains(object3D));

				RGBA_Bytes drawColor = renderData.Color;
				if (renderData.OutputType == PrintOutputTypes.Support)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Yellow, 120);
				}
				else if (renderData.OutputType == PrintOutputTypes.Hole)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Gray, 120);
				}

				// check if we should be rendering materials (this overrides the other colors)
				if (this.RenderType == RenderTypes.Materials)
				{
					drawColor = MatterialRendering.Color(renderData.MaterialIndex);
				}

				if (drawColor.alpha == 255)
				{
					GLHelper.Render(renderData.Mesh, drawColor, renderData.Matrix, RenderType, renderData.Matrix * World.ModelviewMatrix);
				}
				else
				{
					transparentMeshes.Add(new MeshRenderData(renderData.Mesh,
						renderData.Matrix,
						drawColor,
						renderData.MaterialIndex,
						renderData.OutputType));
				}

				if (isSelected && !tooBigForComplexSelection)
				{
					RenderSelection(renderData);
				}

				// RenderNormals(renderData);

				// turn lighting back on after rendering selection outlines
				GL.Enable(EnableCap.Lighting);
			}
		}

		private void RenderNormals(MeshRenderData renderData)
		{
			var frustum = World.GetClippingFrustum();

			foreach (var face in renderData.Mesh.Faces)
			{
				int vertexCount = 0;
				Vector3 faceCenter = Vector3.Zero;
				foreach (var vertex in face.Vertices())
				{
					faceCenter += vertex.Position;
					vertexCount++;
				}
				faceCenter /= vertexCount;

				var transformed1 = Vector3.Transform(faceCenter, renderData.Matrix);
				var normal = Vector3.TransformNormal(face.Normal, renderData.Matrix).GetNormal();

				GLHelper.Render3DLineNoPrep(frustum, World, transformed1, transformed1 + normal, RGBA_Bytes.Red, 2);
			}
		}

		private void RenderSelection(MeshRenderData renderData)
		{
			var screenPosition = new Vector3[3];
			var frustum = World.GetClippingFrustum();
			GLHelper.PrepareFor3DLineRender(true);

			if (renderData.Mesh.Vertices.Count < 1000)
			{
				foreach (MeshEdge meshEdge in renderData.Mesh.MeshEdges)
				{
					if (meshEdge.GetNumFacesSharingEdge() == 2)
					{
						var meshToView = renderData.Matrix * World.ModelviewMatrix;

						FaceEdge firstFaceEdge = meshEdge.firstFaceEdge;
						FaceEdge nextFaceEdge = meshEdge.firstFaceEdge.radialNextFaceEdge;
						// find out if one face is facing the camera and one is facing away
						var viewVertexPosition = Vector3.Transform(firstFaceEdge.FirstVertex.Position, meshToView);
						var viewFirstNormal = Vector3.TransformNormal(firstFaceEdge.ContainingFace.Normal, meshToView).GetNormal();
						var viewNextNormal = Vector3.TransformNormal(nextFaceEdge.ContainingFace.Normal, meshToView).GetNormal();

						// Is the plane facing the camera (0, 0, 0). Finding the distance from the orign to the plane along the normal.
						var firstTowards = Vector3.Dot(viewFirstNormal, viewVertexPosition) < 0;
						var nextTowards = Vector3.Dot(viewNextNormal, viewVertexPosition) < 0;

						if (firstTowards != nextTowards)
						{
							var transformed1 = Vector3.Transform(meshEdge.VertexOnEnd[0].Position, renderData.Matrix);
							var transformed2 = Vector3.Transform(meshEdge.VertexOnEnd[1].Position, renderData.Matrix);

							GLHelper.Render3DLineNoPrep(frustum, World, transformed1, transformed2, RGBA_Bytes.White, selectionHighlightWidth);
						}
					}
				}
			}
			else // just render the bounding box
			{
				RenderAABB(frustum, renderData.Mesh.GetAxisAlignedBoundingBox(), renderData.Matrix, RGBA_Bytes.White, selectionHighlightWidth);
			}
		}

		void RenderAABB(Frustum frustum, AxisAlignedBoundingBox bounds, Matrix4X4 matrix, RGBA_Bytes color, double width)
		{
			for (int i = 0; i < 4; i++)
			{
				Vector3 bottomStartPosition = Vector3.Transform(bounds.GetBottomCorner(i), matrix);
				Vector3 bottomEndPosition = Vector3.Transform(bounds.GetBottomCorner((i + 1) % 4), matrix);
				Vector3 topStartPosition = Vector3.Transform(bounds.GetTopCorner(i), matrix);
				Vector3 topEndPosition = Vector3.Transform(bounds.GetTopCorner((i + 1) % 4), matrix);

				GLHelper.Render3DLineNoPrep(frustum, World, bottomStartPosition, bottomEndPosition, color, width);
				GLHelper.Render3DLineNoPrep(frustum, World, topStartPosition, topEndPosition, color, width);
				GLHelper.Render3DLineNoPrep(frustum, World, topStartPosition, bottomStartPosition, color, width);
			}
		}

		public enum EditorType { Printer, Part }

		public EditorType EditorMode { get; set; } = EditorType.Part;

		private int BackToFrontXY(MeshRenderData a, MeshRenderData b)
		{
			var aCenterWorld = Vector3.Transform(a.Mesh.GetAxisAlignedBoundingBox().Center, a.Matrix);
			aCenterWorld.z = 0; // we only want to look at the distance on xy in world space
			var aCenterInViewSpace = Vector3.Transform(aCenterWorld, World.ModelviewMatrix);

			var bCenterWorld = Vector3.Transform(b.Mesh.GetAxisAlignedBoundingBox().Center, b.Matrix);
			bCenterWorld.z = 0; // we only want to look at the distance on xy in world space
			var bCenterInViewSpace = Vector3.Transform(bCenterWorld, World.ModelviewMatrix);

			return bCenterInViewSpace.LengthSquared.CompareTo(aCenterInViewSpace.LengthSquared);
		}

		private void Draw_GlOpaqueContent(object sender, DrawEventArgs e)
		{
			List<MeshRenderData> transparentMeshes = new List<MeshRenderData>();
			foreach (var object3D in scene.Children)
			{
				DrawObject(object3D, transparentMeshes, false, e);
			}
		}

		private void Draw_GlTransparentContent(object sender, DrawEventArgs e)
		{
			List<MeshRenderData> transparentMeshes = new List<MeshRenderData>();
			foreach (var object3D in scene.Children)
			{
				DrawObject(object3D, transparentMeshes, false, e);
			}

			transparentMeshes.Sort(BackToFrontXY);

			var bedNormalInViewSpace = Vector3.TransformNormal(Vector3.UnitZ, World.ModelviewMatrix).GetNormal();
			var pointOnBedInViewSpace = Vector3.Transform(new Vector3(10, 10, 0), World.ModelviewMatrix);
			var lookingDownOnBed = Vector3.Dot(bedNormalInViewSpace, pointOnBedInViewSpace) < 0;

			if (lookingDownOnBed)
			{
				// render the bed 
				RenderBedMesh(lookingDownOnBed);
				// than the transparent stuff
				//int colorIndex = 0; // helps debug the sorting order
				foreach (var transparentRenderData in transparentMeshes)
				{
					var color = transparentRenderData.Color;
					//color = RGBA_Floats.FromHSL(Math.Max(colorIndex++, 0) / 10.0, .99, .49).GetAsRGBA_Bytes();
					GLHelper.Render(transparentRenderData.Mesh, color, transparentRenderData.Matrix, RenderTypes.Outlines, transparentRenderData.Matrix * World.ModelviewMatrix);
				}
			}
			else
			{
				// render the transparent stuff
				foreach (var transparentRenderData in transparentMeshes)
				{
					GLHelper.Render(transparentRenderData.Mesh, transparentRenderData.Color, transparentRenderData.Matrix, RenderTypes.Outlines, transparentRenderData.Matrix * World.ModelviewMatrix);
				}
				// than render the bed 
				RenderBedMesh(lookingDownOnBed);
			}

			// we don't want to render the bed or build volume before we load a model.
			if (scene.HasChildren() || AllowBedRenderingWhenEmpty)
			{
				if (false) // this is code to draw a small axis indicator
				{
					double big = 10;
					double small = 1;
					Mesh xAxis = PlatonicSolids.CreateCube(big, small, small);
					GLHelper.Render(xAxis, RGBA_Bytes.Red);
					Mesh yAxis = PlatonicSolids.CreateCube(small, big, small);
					GLHelper.Render(yAxis, RGBA_Bytes.Green);
					Mesh zAxis = PlatonicSolids.CreateCube(small, small, big);
					GLHelper.Render(zAxis, RGBA_Bytes.Blue);
				}
			}

			DrawInteractionVolumes(e);
		}

		private void RenderBedMesh(bool lookingDownOnBed)
		{
			if (this.EditorMode == EditorType.Printer)
			{
				// only render if we are above the bed
				if (RenderBed)
				{
					var bedColor = this.BedColor;
					if (!lookingDownOnBed)
					{
						bedColor = new RGBA_Bytes(this.BedColor, this.BedColor.alpha / 4);
					}
					GLHelper.Render(sceneContext.Mesh, bedColor, RenderTypes.Shaded, World.ModelviewMatrix);
					if (sceneContext.PrinterShape != null)
					{
						GLHelper.Render(sceneContext.PrinterShape, bedColor, RenderTypes.Shaded, World.ModelviewMatrix);
					}
				}

				if (sceneContext.BuildVolumeMesh != null && RenderBuildVolume)
				{
					GLHelper.Render(sceneContext.BuildVolumeMesh, this.BuildVolumeColor, RenderTypes.Shaded, World.ModelviewMatrix);
				}
			}
			else
			{
				GL.Disable(EnableCap.Texture2D);
				GL.Disable(EnableCap.Blend);

				int width = 600;

				GL.Begin(BeginMode.Lines);
				{
					for (int i = -width; i <= width; i += 50)
					{
						GL.Color4(240, 240, 240, 255);
						GL.Vertex3(i, width, 0);
						GL.Vertex3(i, -width, 0);

						GL.Vertex3(width, i, 0);
						GL.Vertex3(-width, i, 0);
					}

					GL.Color4(255, 0, 0, 255);
					GL.Vertex3(width, 0, 0);
					GL.Vertex3(-width, 0, 0);

					GL.Color4(0, 255, 0, 255);
					GL.Vertex3(0, width, 0);
					GL.Vertex3(0, -width, 0);

					GL.Color4(0, 0, 255, 255);
					GL.Vertex3(0, 0, 10);
					GL.Vertex3(0, 0, -10);
				}
				GL.End();
			}
		}

		private void DrawInteractionVolumes(DrawEventArgs e)
		{
			if(SuppressUiVolumes)
			{
				return;
			}

			// draw on top of anything that is already drawn
			foreach (InteractionVolume interactionVolume in interactionLayer.InteractionVolumes)
			{
				if (interactionVolume.DrawOnTop)
				{
					GL.Disable(EnableCap.DepthTest);
					interactionVolume.DrawGlContent(new DrawGlContentEventArgs(false, e));
					GL.Enable(EnableCap.DepthTest);
				}
			}

			// Draw again setting the depth buffer and ensuring that all the interaction objects are sorted as well as we can
			foreach (InteractionVolume interactionVolume in interactionLayer.InteractionVolumes)
			{
				interactionVolume.DrawGlContent(new DrawGlContentEventArgs(true, e));
			}
		}
	}
}