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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
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

	public static class MaterialRendering
	{
		public static Color Color(int materialIndex)
		{
			return ColorF.FromHSL(Math.Max(materialIndex, 0) / 10.0, .99, .49).ToColor();
		}

		public static bool InsideBuildVolume(this IObject3D item, PrinterConfig printerConfig)
		{
			if(item.Mesh == null)
			{
				return true;
			}

			var worldMatrix = item.WorldMatrix();
			// probably need , true (require precision)
			var aabb = item.Mesh.GetAxisAlignedBoundingBox(worldMatrix);

			var bed = printerConfig.Bed;

			if (bed.BuildHeight > 0
				&& (aabb.maxXYZ.Z <= 0
				|| aabb.maxXYZ.Z >= bed.BuildHeight))
			{
				// object completely below the bed or any part above the build volume
				return false;
			}

			switch(bed.BedShape)
			{
				case BedShape.Rectangular:
					if(aabb.minXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X/2
						|| aabb.maxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.minXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.maxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						return false;
					}
					break;

				case BedShape.Circular: 
					// This could be much better if it checked the actual vertext data of the mesh against the cylinder
					// first check if any of it is outside the bed rect
					if (aabb.minXYZ.X < bed.BedCenter.X - bed.ViewerVolume.X / 2
						|| aabb.maxXYZ.X > bed.BedCenter.X + bed.ViewerVolume.X / 2
						|| aabb.minXYZ.Y < bed.BedCenter.Y - bed.ViewerVolume.Y / 2
						|| aabb.maxXYZ.Y > bed.BedCenter.Y + bed.ViewerVolume.Y / 2)
					{
						// TODO: then check if all of it is outside the bed circle
						return false;
					}
					break;
			}

			return true;
		}
	}

	public class MeshViewerWidget : GuiWidget
	{
		static ImageBuffer ViewOnlyTexture;

		private Color lightWireframe = new Color("#aaa4");
		private Color darkWireframe = new Color("#3334");
		private Color gCodeMeshColor;

		// TODO: Need to be instance based for multi-printer
		public GuiWidget ParentSurface { get; set; }

		private RenderTypes renderType = RenderTypes.Shaded;

		private InteractionLayer interactionLayer;

		private BedConfig sceneContext;

		private double selectionHighlightWidth = 5;

		private Color debugBorderColor = Color.Green;
		private Color debugNotSelectedFillColor = new Color(Color.White, 120);

		public MeshViewerWidget(BedConfig sceneContext, InteractionLayer interactionLayer, string startingTextMessage = "", EditorType editorType = EditorType.Part)
		{
			this.EditorMode = editorType;
			this.scene = sceneContext.Scene;
			this.sceneContext = sceneContext;
			this.interactionLayer = interactionLayer;
			this.World = interactionLayer.World;

			var theme = ApplicationController.Instance.Theme;

			gCodeMeshColor = new Color(theme.Colors.PrimaryAccentColor, 35);

			scene.SelectionChanged += (sender, e) =>
			{
				Invalidate();
				lastSelectionChangedMs = UiThread.CurrentTimerMs;
			};

			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			BedColor = new ColorF(.8, .8, .8, .7).ToColor();
			BuildVolumeColor = new ColorF(.2, .8, .3, .2).ToColor();

			this.interactionLayer.DrawGlTransparentContent += Draw_GlTransparentContent;

			if (ViewOnlyTexture == null)
			{
				UiThread.RunOnIdle(() =>
				{
					ViewOnlyTexture = new ImageBuffer(32, 32, 32);
					var graphics2D = ViewOnlyTexture.NewGraphics2D();
					graphics2D.Clear(Color.White);
					graphics2D.FillRectangle(0, 0, ViewOnlyTexture.Width / 2, ViewOnlyTexture.Height, Color.LightGray);
					// request the texture so we can set it to repeat
					var plugin = ImageGlPlugin.GetImageGlPlugin(ViewOnlyTexture, true, true, false);
				});
			}
		}

		public override void OnParentChanged(EventArgs e)
		{
			this.ParentSurface = this.Parent;
			base.OnParentChanged(e);
		}

		public WorldView World { get; }

		public event EventHandler LoadDone;

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public Color BedColor { get; set; }

		public Color BuildVolumeColor { get; set; }

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
			foreach (InteractionVolume child in interactionLayer.InteractionVolumes)
			{
				string object3DName = child.Name;

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

				if (nameFound
					&& child.CollisionVolume != null)
				{
					AxisAlignedBoundingBox bounds = child.CollisionVolume.GetAxisAlignedBoundingBox();
					bounds = bounds.NewTransformed(child.TotalTransform);

					RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
					for (int i = 0; i < 4; i++)
					{
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetTopCorner(i)));
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetBottomCorner(i)));
					}

					if (touchingBounds.IsTouching(screenBoundsOfObject3D))
					{
						Vector3 renderPosition = bounds.Center;
						Vector2 objectCenterScreenSpace = this.World.GetScreenPosition(renderPosition);
						Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName, child));
					}
				}
			}

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
						Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName, child));
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
			get => this.ModelView ? renderType : RenderTypes.Wireframe;
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

		public static Color GetExtruderColor(int extruderIndex)
		{
			return MaterialRendering.Color(extruderIndex);
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

		public bool ModelView { get; set; } = true;

		private void DrawObject(IObject3D object3D, List<Object3DView> transparentMeshes, bool parentSelected, DrawEventArgs e)
		{
			foreach (var item in object3D.VisibleMeshes())
			{
				// check for correct persistable rendering
				if(MeshViewerWidget.ViewOnlyTexture != null
					&& item.Mesh.Faces.Count > 0)
				{
					ImageBuffer faceTexture = null;
					item.Mesh.FaceTexture.TryGetValue((item.Mesh.Faces[0], 0), out faceTexture);
					bool hasPersistableTexture = faceTexture == MeshViewerWidget.ViewOnlyTexture;

					if (item.Persistable)
					{
						if (hasPersistableTexture)
						{
							// make sure it does not have the view only texture
							item.Mesh.RemoveTexture(ViewOnlyTexture, 0);
						}
					}
					else
					{
						if (!hasPersistableTexture)
						{
							// make sure it does have the view only texture
							var aabb = item.Mesh.GetAxisAlignedBoundingBox();
							var matrix = Matrix4X4.CreateScale(.5, .5, 1);
							matrix *= Matrix4X4.CreateRotationZ(MathHelper.Tau / 8);
							item.Mesh.PlaceTexture(ViewOnlyTexture, matrix);
						}
					}
				}

				Color drawColor = GetItemColor(item);

				bool isDebugItem = (item == scene.DebugItem);

				if (!this.ModelView)
				{
					if (modelRenderStyle == ModelRenderStyle.WireframeAndSolid)
					{
						drawColor = gCodeMeshColor;
					}
					else if (modelRenderStyle == ModelRenderStyle.None)
					{
						drawColor = Color.Transparent;
					}
				}

				if (drawColor.alpha == 255
					|| isDebugItem)
				{
					// Render as solid
					GLHelper.Render(item.Mesh, drawColor, item.WorldMatrix(), this.RenderType, item.WorldMatrix() * World.ModelviewMatrix, darkWireframe);
				}
				else if (drawColor != Color.Transparent)
				{
					// Queue for transparency
					transparentMeshes.Add(new Object3DView(item, drawColor));
				}

				bool isSelected = parentSelected ||
					scene.HasSelection && (object3D == scene.SelectedItem || scene.SelectedItem.Children.Contains(object3D));

				if (isSelected)
				{
					var totalVertices = 0;

					foreach (var visibleMesh in object3D.VisibleMeshes())
					{
						totalVertices += visibleMesh.Mesh.Vertices.Count;

						if (totalVertices > 1000)
						{
							break;
						}
					}

					var frustum = World.GetClippingFrustum();

					var selectionColor = Color.White;
					double secondsSinceSelectionChanged = (UiThread.CurrentTimerMs - lastSelectionChangedMs) / 1000.0;
					if (secondsSinceSelectionChanged < .5)
					{
						//var accentColor = ApplicationController.Instance.Theme.Colors.PrimaryAccentColor;
						var accentColor = Color.LightGray;
						if (secondsSinceSelectionChanged < .25)
						{
							selectionColor = Color.White.Blend(accentColor, EaseInOut(secondsSinceSelectionChanged * 4));
						}
						else
						{
							selectionColor = accentColor.Blend(Color.White, EaseInOut((secondsSinceSelectionChanged - .25) * 4));
						}
						Invalidate();
					}

					bool tooBigForComplexSelection = totalVertices > 1000;
					if (tooBigForComplexSelection
						&& scene.DebugItem == null)
					{
						GLHelper.PrepareFor3DLineRender(true);
						RenderAABB(frustum, object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity), Matrix4X4.Identity, selectionColor, selectionHighlightWidth);
						GL.Enable(EnableCap.Lighting);
					}
					else
					{
						RenderSelection(item, frustum, selectionColor);
					}
				}

#if DEBUG
				if (isDebugItem)
				{
					var frustum = World.GetClippingFrustum();

					var aabb = object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					GLHelper.PrepareFor3DLineRender(true);
					RenderAABB(frustum, aabb, Matrix4X4.Identity, debugBorderColor, 1);

					if (item.Mesh != null)
					{
						GLHelper.Render(item.Mesh, debugBorderColor, item.WorldMatrix(), 
							RenderTypes.Wireframe, item.WorldMatrix() * World.ModelviewMatrix);
					}
				}
#endif

				// RenderNormals(renderData);

				// turn lighting back on after rendering selection outlines
				GL.Enable(EnableCap.Lighting);
			}
		}

		private Color GetItemColor(IObject3D item)
		{
			Color drawColor = item.WorldColor();
			if (item.WorldOutputType() == PrintOutputTypes.Support)
			{
				drawColor = new Color(Color.Yellow, 120);
			}
			else if (item.WorldOutputType() == PrintOutputTypes.Hole)
			{
				drawColor = new Color(Color.Gray, 120);
			}

			// If there is a printer - check if the object is within the bed volume (has no AABB outside the bed volume)
			if (sceneContext.Printer != null)
			{
				if (!item.InsideBuildVolume(sceneContext.Printer))
				{
					drawColor = new Color(drawColor, 65);
				}
			}

			// check if we should be rendering materials (this overrides the other colors)
			if (this.RenderType == RenderTypes.Materials)
			{
				drawColor = MaterialRendering.Color(item.WorldMaterialIndex());
			}

			if(drawColor.alpha != 255
				&& item is Object3D item3D)
			{
				item3D.EnsureTransparentSorting();
			}

			return drawColor;
		}

		private void RenderNormals(IObject3D renderData)
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

				GLHelper.Render3DLineNoPrep(frustum, World, transformed1, transformed1 + normal, Color.Red, 2);
			}
		}

		private void RenderSelection(IObject3D renderData, Frustum frustum, Color selectionColor)
		{
			if(renderData.Mesh == null)
			{
				return;
			}
			var screenPosition = new Vector3[3];
			GLHelper.PrepareFor3DLineRender(true);

			if (renderData.Mesh.Vertices.Count < 1000)
			{
				bool renderedAnything = false;
				foreach (MeshEdge meshEdge in renderData.Mesh.MeshEdges)
				{
					if (meshEdge.GetNumFacesSharingEdge() == 2)
					{
						renderedAnything = true;
						var meshToView = renderData.WorldMatrix() * World.ModelviewMatrix;

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
							var transformed1 = Vector3.Transform(meshEdge.VertexOnEnd[0].Position, renderData.WorldMatrix());
							var transformed2 = Vector3.Transform(meshEdge.VertexOnEnd[1].Position, renderData.WorldMatrix());

							GLHelper.Render3DLineNoPrep(frustum, World, transformed1, transformed2, selectionColor, selectionHighlightWidth);
						}
					}
				}

				if(!renderedAnything)
				{
					RenderAABB(frustum, renderData.Mesh.GetAxisAlignedBoundingBox(), renderData.WorldMatrix(), selectionColor, selectionHighlightWidth);
				}
			}
			else // just render the bounding box
			{
				RenderAABB(frustum, renderData.Mesh.GetAxisAlignedBoundingBox(), renderData.WorldMatrix(), selectionColor, selectionHighlightWidth);
			}
		}

		private double EaseInOut(double t)
		{
			if (t <= 0.5f)
			{
				return 2.0f * (t * t);
			}

			t -= 0.5f;
			return 2.0f * t * (1.0f - t) + 0.5;
		}

		void RenderAABB(Frustum frustum, AxisAlignedBoundingBox bounds, Matrix4X4 matrix, Color color, double width)
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

		private int BackToFrontXY(Object3DView a, Object3DView b)
		{
			var aCenterWorld = Vector3.Transform(a.Object3D.Mesh.GetAxisAlignedBoundingBox().Center, a.Object3D.Matrix);
			aCenterWorld.Z = 0; // we only want to look at the distance on xy in world space
			var aCenterInViewSpace = Vector3.Transform(aCenterWorld, World.ModelviewMatrix);

			var bCenterWorld = Vector3.Transform(b.Object3D.Mesh.GetAxisAlignedBoundingBox().Center, b.Object3D.Matrix);
			bCenterWorld.Z = 0; // we only want to look at the distance on xy in world space
			var bCenterInViewSpace = Vector3.Transform(bCenterWorld, World.ModelviewMatrix);

			return bCenterInViewSpace.LengthSquared.CompareTo(aCenterInViewSpace.LengthSquared);
		}

		private void Draw_GlTransparentContent(object sender, DrawEventArgs e)
		{
			var gcodeOptions = sceneContext.RendererOptions;

			switch(gcodeOptions.GCodeModelView)
			{
				case "Wireframe":
					modelRenderStyle = ModelRenderStyle.Wireframe;
					break;

				case "Semi-Transparent":
					modelRenderStyle = ModelRenderStyle.WireframeAndSolid;
					break;

				default:
					modelRenderStyle = ModelRenderStyle.None;
					break;
			}

			// Draw solid objects, extract transparent
			var transparentMeshes = new List<Object3DView>();
			foreach (var object3D in scene.Children)
			{
				if (object3D.Visible)
				{
					DrawObject(object3D, transparentMeshes, false, e);
				}
			}

			transparentMeshes.Sort(BackToFrontXY);

			var bedNormalInViewSpace = Vector3.TransformNormal(Vector3.UnitZ, World.ModelviewMatrix).GetNormal();
			var pointOnBedInViewSpace = Vector3.Transform(new Vector3(10, 10, 0), World.ModelviewMatrix);
			var lookingDownOnBed = Vector3.Dot(bedNormalInViewSpace, pointOnBedInViewSpace) < 0;

			if (lookingDownOnBed)
			{
				RenderBedMesh(lookingDownOnBed);
			}

			// Draw transparent objects
			foreach (var item in transparentMeshes)
			{
				var object3D = item.Object3D;
				GLHelper.Render(
					object3D.Mesh,
					item.Color,
					object3D.WorldMatrix(),
					RenderTypes.Outlines,
					object3D.WorldMatrix() * World.ModelviewMatrix,
					(modelRenderStyle == ModelRenderStyle.Solid) ? Color.Transparent : lightWireframe);
			}

			if (!lookingDownOnBed)
			{
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
					GLHelper.Render(xAxis, Color.Red);
					Mesh yAxis = PlatonicSolids.CreateCube(small, big, small);
					GLHelper.Render(yAxis, Color.Green);
					Mesh zAxis = PlatonicSolids.CreateCube(small, small, big);
					GLHelper.Render(zAxis, Color.Blue);
				}
			}

			DrawInteractionVolumes(e);

			if (scene.DebugItem?.Mesh != null)
			{
				var debugItem = scene.DebugItem;
				GLHelper.Render(debugItem.Mesh, debugBorderColor, debugItem.WorldMatrix(), 
					RenderTypes.Wireframe, debugItem.WorldMatrix() * World.ModelviewMatrix);
			}
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
						bedColor = new Color(this.BedColor, this.BedColor.alpha / 4);
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

		public enum ModelRenderStyle
		{
			Solid,
			Wireframe,
			WireframeAndSolid,
			None
		}

		private ModelRenderStyle modelRenderStyle = MeshViewerWidget.ModelRenderStyle.Wireframe;
		private long lastSelectionChangedMs;

		private class Object3DView
		{
			public Color Color { get; set; }

			public IObject3D Object3D { get; }

			public Object3DView(IObject3D source, Color color)
			{
				this.Object3D = source;
				this.Color = color;

				if (source is Object3D object3D
					&& color != source.Color
						&& color.alpha != 255)
				{
					object3D.EnsureTransparentSorting();
				}
			}
		}
	}
}
 