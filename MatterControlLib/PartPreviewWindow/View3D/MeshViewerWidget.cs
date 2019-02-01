/*
Copyright (c) 2019, Lars Brubaker
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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using static MatterHackers.Agg.Easing;

namespace MatterHackers.MeshVisualizer
{
	public class MeshViewerWidget : GuiWidget
	{
		private static ImageBuffer ViewOnlyTexture;

		private Color lightWireframe = new Color("#aaa4");
		private Color darkWireframe = new Color("#3334");
		private GridColors gridColors;
		private Color gCodeMeshColor;

		private InteractiveScene scene;

		private InteractionLayer interactionLayer;

		private BedConfig sceneContext;

		private Color debugBorderColor = Color.Green;

		public MeshViewerWidget(BedConfig sceneContext, InteractionLayer interactionLayer, ThemeConfig theme, EditorType editorType = EditorType.Part)
		{
			this.EditorMode = editorType;
			this.scene = sceneContext.Scene;
			this.sceneContext = sceneContext;
			this.interactionLayer = interactionLayer;
			this.World = interactionLayer.World;
			this.theme = theme;

			gridColors = new GridColors()
			{
				Gray = theme.ResolveColor(theme.BackgroundColor, theme.GetBorderColor((theme.IsDarkTheme ? 35 : 55))),
				Red = theme.ResolveColor(theme.BackgroundColor, new Color(Color.Red, (theme.IsDarkTheme ? 105 : 170))),
				Green = theme.ResolveColor(theme.BackgroundColor, new Color(Color.Green, (theme.IsDarkTheme ? 105 : 170))),
				Blue = theme.ResolveColor(theme.BackgroundColor, new Color(Color.Blue, 195))
			};

			gCodeMeshColor = new Color(theme.PrimaryAccentColor, 35);

			// Register listeners
			scene.SelectionChanged += selection_Changed;

			BuildVolumeColor = new ColorF(.2, .8, .3, .2).ToColor();

			this.interactionLayer.DrawGlTransparentContent += Draw_GlTransparentContent;

			if (ViewOnlyTexture == null)
			{
				// TODO: What is the ViewOnlyTexture???
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

		public WorldView World { get; }

		private ThemeConfig theme;

		private class GridColors
		{
			public Color Red { get; set; }
			public Color Green { get; set; }
			public Color Blue { get; set; }
			public Color Gray { get; set; }
		}

		public bool AllowBedRenderingWhenEmpty { get; set; }

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
						this.World.RenderDebugAABB(e.Graphics2D, child.TraceData().GetAxisAlignedBoundingBox());
						this.World.RenderDebugAABB(e.Graphics2D, child.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
					}
				};
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			scene.SelectionChanged -= selection_Changed;

			base.OnClosed(e);
		}

		public override List<WidgetAndPosition> FindDescendants(IEnumerable<string> namesToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (InteractionVolume child in interactionLayer.InteractionVolumes)
			{
				string object3DName = child.Name;

				bool nameFound = false;

				foreach (var nameToSearchFor in namesToSearchFor)
				{
					if (seachType == SearchType.Exact)
					{
						if (object3DName == nameToSearchFor)
						{
							nameFound = true;
							break;
						}
					}
					else
					{
						if (nameToSearchFor == ""
							|| object3DName.Contains(nameToSearchFor))
						{
							nameFound = true;
							break;
						}
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

				foreach (var nameToSearchFor in namesToSearchFor)
				{
					if (seachType == SearchType.Exact)
					{
						if (object3DName == nameToSearchFor)
						{
							nameFound = true;
							break;
						}
					}
					else
					{
						if (nameToSearchFor == ""
							|| object3DName.Contains(nameToSearchFor))
						{
							nameFound = true;
							break;
						}
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

			return base.FindDescendants(namesToSearchFor, foundChildren, touchingBounds, seachType, allowInvalidItems);
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

		private void DrawObject(IObject3D object3D, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			var selectedItem = scene.SelectedItem;

			foreach (var item in object3D.VisibleMeshes())
			{
				// check for correct persistable rendering
				if(MeshViewerWidget.ViewOnlyTexture != null
					&& item.Mesh.Faces.Count > 0)
				{
					ImageBuffer faceTexture = null;

					//item.Mesh.FaceTexture.TryGetValue((item.Mesh.Faces[0], 0), out faceTexture);
					bool hasPersistableTexture = faceTexture == MeshViewerWidget.ViewOnlyTexture;

					if (item.WorldPersistable())
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

				if(selectedItem is ISelectableChildContainer selectableChildContainer)
				{
					if(item.AncestorsAndSelf().Any(i => selectableChildContainer.SelectedChildren.Contains(i.ID)))
					{
						drawColor = new Color(drawColor, 200);
					}
				}

				bool isDebugItem = (item == scene.DebugItem);

				if (!sceneContext.ViewState.ModelView)
				{
					if (modelRenderStyle == ModelRenderStyle.WireframeAndSolid)
					{
						drawColor = gCodeMeshColor;
					}
					else if(modelRenderStyle == ModelRenderStyle.Wireframe)
					{
						drawColor = new Color(gCodeMeshColor, 1);
					}
					else if (modelRenderStyle == ModelRenderStyle.None)
					{
						drawColor = Color.Transparent;
					}
				}

				bool hasTransparentTextures = item.Mesh.FaceTextures.Any((ft) => ft.Value.image.HasTransparency);

				if ((drawColor.alpha == 255
					&& !hasTransparentTextures)
					|| isDebugItem)
				{
					// Render as solid
					GLHelper.Render(item.Mesh,
						drawColor,
						item.WorldMatrix(),
						sceneContext.ViewState.RenderType,
						item.WorldMatrix() * World.ModelviewMatrix,
						darkWireframe, () => Invalidate());
				}
				else if (drawColor != Color.Transparent)
				{
					// Queue for transparency
					transparentMeshes.Add(new Object3DView(item, drawColor));
				}

				bool isSelected = selectedItem != null
					&& (selectedItem.DescendantsAndSelf().Any((i) => i == item)
						|| selectedItem.Parents<ModifiedMeshObject3D>().Any((mw) => mw == item));

				if (isSelected && scene.DrawSelection)
				{
					var frustum = World.GetClippingFrustum();

					var selectionColor = Color.White;
					double secondsSinceSelectionChanged = (UiThread.CurrentTimerMs - lastSelectionChangedMs) / 1000.0;
					if (secondsSinceSelectionChanged < .5)
					{
						var accentColor = Color.LightGray;
						if (secondsSinceSelectionChanged < .25)
						{
							selectionColor = Color.White.Blend(accentColor, Quadratic.InOut(secondsSinceSelectionChanged * 4));
						}
						else
						{
							selectionColor = accentColor.Blend(Color.White, Quadratic.InOut((secondsSinceSelectionChanged - .25) * 4));
						}
						Invalidate();
					}

					RenderSelection(item, frustum, selectionColor);
				}

#if DEBUG
				if (isDebugItem)
				{
					var frustum = World.GetClippingFrustum();

					var aabb = object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					World.RenderAabb(aabb, Matrix4X4.Identity, debugBorderColor, 1);

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

			// If there is a printer - check if the object is within the bed volume (has no AABB outside the bed volume)
			if (sceneContext.Printer != null)
			{
				if (!sceneContext.Printer.InsideBuildVolume(item))
				{
					drawColor = new Color(drawColor, 65);
				}
			}

			// check if we should be rendering materials (this overrides the other colors)
			if (sceneContext.ViewState.RenderType == RenderTypes.Materials)
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
			throw new NotImplementedException();
			//var frustum = World.GetClippingFrustum();

			//foreach (var face in renderData.Mesh.Faces)
			//{
			//	int vertexCount = 0;
			//	Vector3 faceCenter = Vector3.Zero;
			//	foreach (var vertex in face.Vertices())
			//	{
			//		faceCenter += vertex.Position;
			//		vertexCount++;
			//	}
			//	faceCenter /= vertexCount;

			//	var transformed1 = Vector3Ex.Transform(faceCenter, renderData.Matrix);
			//	var normal = Vector3Ex.TransformNormal(face.Normal, renderData.Matrix).GetNormal();

			//	World.Render3DLineNoPrep(frustum, transformed1, transformed1 + normal, Color.Red, 2);
			//}
		}

		private void RenderSelection(IObject3D item, Frustum frustum, Color selectionColor)
		{
			if (item.Mesh == null)
			{
				return;
			}

			// Turn off lighting
			GL.Disable(EnableCap.Lighting);
			// Only render back faces
			GL.CullFace(CullFaceMode.Front);
			// Expand the object
			var worldMatrix = item.WorldMatrix();
			var worldBounds = item.Mesh.GetAxisAlignedBoundingBox(worldMatrix);
			var worldCenter = worldBounds.Center;
			double distBetweenPixelsWorldSpace = World.GetWorldUnitsPerScreenPixelAtPosition(worldCenter);
			var pixelsAccross = worldBounds.Size / distBetweenPixelsWorldSpace;
			var pixelsWant = pixelsAccross + Vector3.One * 4 * Math.Sqrt(2);

			var wantMm = pixelsWant * distBetweenPixelsWorldSpace;

			var scaleMatrix = worldMatrix.ApplyAtPosition(worldCenter, Matrix4X4.CreateScale(
				wantMm.X / worldBounds.XSize,
				wantMm.Y / worldBounds.YSize,
				wantMm.Z / worldBounds.ZSize));

			GLHelper.Render(item.Mesh,
				selectionColor,
				scaleMatrix, RenderTypes.Shaded,
				null,
				darkWireframe);

			// restore settings
			GL.CullFace(CullFaceMode.Back);
			GL.Enable(EnableCap.Lighting);
		}

		public enum EditorType { Printer, Part }

		public EditorType EditorMode { get; set; } = EditorType.Part;

		private int BackToFrontXY(Object3DView a, Object3DView b)
		{
			var meshA = a.Object3D.Mesh;
			var meshB = b.Object3D.Mesh;

			if (meshA == null)
			{
				return 1;
			}
			else if (meshB == null)
			{
				return -1;
			}

			var aCenterWorld = Vector3Ex.Transform(meshA.GetAxisAlignedBoundingBox().Center, a.Object3D.Matrix);
			aCenterWorld.Z = 0; // we only want to look at the distance on xy in world space
			var aCenterInViewSpace = Vector3Ex.Transform(aCenterWorld, World.ModelviewMatrix);

			var bCenterWorld = Vector3Ex.Transform(meshB.GetAxisAlignedBoundingBox().Center, b.Object3D.Matrix);
			bCenterWorld.Z = 0; // we only want to look at the distance on xy in world space
			var bCenterInViewSpace = Vector3Ex.Transform(bCenterWorld, World.ModelviewMatrix);

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
					DrawObject(object3D, transparentMeshes, e);
				}
			}

			transparentMeshes.Sort(BackToFrontXY);

			var bedNormalInViewSpace = Vector3Ex.TransformNormal(Vector3.UnitZ, World.ModelviewMatrix).GetNormal();
			var pointOnBedInViewSpace = Vector3Ex.Transform(new Vector3(10, 10, 0), World.ModelviewMatrix);
			var lookingDownOnBed = Vector3Ex.Dot(bedNormalInViewSpace, pointOnBedInViewSpace) < 0;

			if (lookingDownOnBed)
			{
				RenderBedMesh(lookingDownOnBed);
			}

			var wireColor = Color.Transparent;
			switch(modelRenderStyle)
			{
				case ModelRenderStyle.Wireframe:
					wireColor = darkWireframe;
					break;

				case ModelRenderStyle.WireframeAndSolid:
					wireColor = lightWireframe;
					break;
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
					wireColor);
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
				if (sceneContext.RendererOptions.RenderBed)
				{
					var bedColor = theme.ResolveColor(Color.White, theme.BackgroundColor.WithAlpha(111));

					if (!lookingDownOnBed)
					{
						bedColor = new Color(bedColor, bedColor.alpha / 4);
					}

					GLHelper.Render(sceneContext.Mesh, bedColor, RenderTypes.Shaded, World.ModelviewMatrix);

					if (sceneContext.PrinterShape != null)
					{
						GLHelper.Render(sceneContext.PrinterShape, bedColor, RenderTypes.Shaded, World.ModelviewMatrix);
					}
				}

				if (sceneContext.BuildVolumeMesh != null && sceneContext.RendererOptions.RenderBuildVolume)
				{
					GLHelper.Render(sceneContext.BuildVolumeMesh, this.BuildVolumeColor, RenderTypes.Shaded, World.ModelviewMatrix);
				}
			}
			else
			{
				GL.Disable(EnableCap.Texture2D);
				GL.Disable(EnableCap.Blend);
				GL.Disable(EnableCap.Lighting);

				int width = 600;

				GL.Begin(BeginMode.Lines);
				{
					for (int i = -width; i <= width; i += 50)
					{
						GL.Color4(gridColors.Gray);
						GL.Vertex3(i, width, 0);
						GL.Vertex3(i, -width, 0);

						GL.Vertex3(width, i, 0);
						GL.Vertex3(-width, i, 0);
					}

					// X axis
					GL.Color4(gridColors.Red);
					GL.Vertex3(width, 0, 0);
					GL.Vertex3(-width, 0, 0);

					// Y axis
					GL.Color4(gridColors.Green);
					GL.Vertex3(0, width, 0);
					GL.Vertex3(0, -width, 0);

					// Z axis
					GL.Color4(gridColors.Blue);
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

		void selection_Changed(object sender, EventArgs e)
		{
			Invalidate();
			lastSelectionChangedMs = UiThread.CurrentTimerMs;
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
