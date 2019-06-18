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
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class InteractionLayer : GuiWidget
	{
		private static ImageBuffer ViewOnlyTexture;

		private Color lightWireframe = new Color("#aaa4");
		private Color darkWireframe = new Color("#3334");
		private Color gCodeMeshColor;

		private readonly InteractiveScene scene;

		private readonly ISceneContext sceneContext;

		private readonly ThemeConfig theme;
		private readonly FloorDrawable floorDrawable;

		private ModelRenderStyle modelRenderStyle = ModelRenderStyle.Wireframe;

		private readonly List<IDrawable> drawables = new List<IDrawable>();
		private readonly List<IDrawableItem> itemDrawables = new List<IDrawableItem>();

		private bool emulatorHooked;
		private long lastEmulatorDrawMs;
		private readonly Mesh emulatorNozzleMesh = PlatonicSolids.CreateCube(1, 1, 10);

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public Color BuildVolumeColor { get; set; }

		public override void OnLoad(EventArgs args)
		{
#if DEBUG
			drawables.AddRange(new IDrawable[]
			{
				new AxisIndicatorDrawable(),
				new ScreenspaceAxisIndicatorDrawable(),
				new SceneTraceDataDrawable(sceneContext),
				new AABBDrawable(sceneContext),
				new LevelingDataDrawable(sceneContext),
			});
#endif
			itemDrawables.AddRange(new IDrawableItem[]
			{
				new SelectedItemDrawable(sceneContext, this),
				new ItemTraceDataDrawable(sceneContext)
			});

#if DEBUG
			itemDrawables.AddRange(new IDrawableItem[]
			{
				new InspectedItemDrawable(sceneContext),
				new NormalsDrawable(sceneContext)
			});
#endif

			base.OnLoad(args);
		}

		public override List<WidgetAndPosition> FindDescendants(IEnumerable<string> namesToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (InteractionVolume child in this.InteractionVolumes)
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
						var screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

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
					for (int i = 0; i < 4; i++)
					{
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetTopCorner(i)));
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetBottomCorner(i)));
					}

					if (touchingBounds.IsTouching(screenBoundsOfObject3D))
					{
						Vector3 renderPosition = bounds.Center;
						Vector2 objectCenterScreenSpace = this.World.GetScreenPosition(renderPosition);
						var screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.X, (int)objectCenterScreenSpace.Y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName, child));
					}
				}
			}

			return base.FindDescendants(namesToSearchFor, foundChildren, touchingBounds, seachType, allowInvalidItems);
		}

		private void DrawObject(IObject3D object3D, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			var selectedItem = scene.SelectedItem;

			foreach (var item in object3D.VisibleMeshes())
			{
				// check for correct protection rendering
				ValidateViewOnlyTexturing(item);

				Color drawColor = this.GetItemColor(item, selectedItem);

				bool hasTransparentTextures = item.Mesh.FaceTextures.Any(ft => ft.Value.image.HasTransparency);

				if ((drawColor.alpha == 255
					&& !hasTransparentTextures)
					|| (item == scene.DebugItem))
				{
					// Render as solid
					GLHelper.Render(item.Mesh,
						drawColor,
						item.WorldMatrix(),
						sceneContext.ViewState.RenderType,
						item.WorldMatrix() * World.ModelviewMatrix,
						darkWireframe,
						() => Invalidate());
				}
				else if (drawColor != Color.Transparent)
				{
					// Queue for transparency
					transparentMeshes.Add(new Object3DView(item, drawColor));
				}

				bool isSelected = selectedItem != null
					&& (item == selectedItem
						|| item.Ancestors().Any(p => p == selectedItem));

				// Invoke all item Drawables
				foreach (var drawable in itemDrawables.Where(d => d.DrawStage != DrawStage.Last && d.Enabled))
				{
					drawable.Draw(this, item, isSelected, e, Matrix4X4.Identity, this.World);
				}

				// turn lighting back on after rendering selection outlines
				GL.Enable(EnableCap.Lighting);
			}
		}

		private static void ValidateViewOnlyTexturing(IObject3D item)
		{
			// if there is no view only texture or the item is locked
			if (InteractionLayer.ViewOnlyTexture == null
				|| item.Mesh.Faces.Count == 0)
			{
				return;
			}

			// if the item is not currently be processed
			if (!item.RebuildLocked)
			{
				item.Mesh.FaceTextures.TryGetValue(0, out FaceTextureData faceTexture);
				bool viewOnlyTexture = faceTexture?.image == InteractionLayer.ViewOnlyTexture;

				// if not persistable and has view only texture, remove the view only texture if it has it
				if (item.WorldPersistable()
					&& viewOnlyTexture)
				{
					// make sure it does not have the view only texture
					using (item.RebuildLock())
					{
						item.Mesh.RemoveTexture(ViewOnlyTexture, 0);
					}
				}
				else if (!item.WorldPersistable()
					&& !viewOnlyTexture)
				{
					// add a view only texture if it does not have one
					// make a copy of the mesh and texture it
					Task.Run(() =>
					{
						// make sure it does have the view only texture
						var aabb = item.Mesh.GetAxisAlignedBoundingBox();
						var matrix = Matrix4X4.CreateScale(.5, .5, 1);
						matrix *= Matrix4X4.CreateRotationZ(MathHelper.Tau / 8);
						// make sure it has it's own copy of the mesh
						using (item.RebuildLock())
						{
							item.Mesh = item.Mesh.Copy(CancellationToken.None);
							item.Mesh.PlaceTexture(ViewOnlyTexture, matrix);
						}
					});
				}
			}
		}

		private Color GetItemColor(IObject3D item, IObject3D selectedItem)
		{
			Color drawColor = item.WorldColor();
			if (item.WorldOutputType() == PrintOutputTypes.Support)
			{
				drawColor = new Color(Color.Yellow, 120);
			}
			else if (item.WorldOutputType() == PrintOutputTypes.WipeTower)
			{
				drawColor = new Color(Color.Cyan, 120);
			}
			else if (sceneContext.ViewState.RenderType == RenderTypes.Materials)
			{
				// check if we should be rendering materials (this overrides the other colors)
				drawColor = MaterialRendering.Color(item.WorldMaterialIndex());
			}

			if (sceneContext.Printer is PrinterConfig printer)
			{
				if (printer.InsideBuildVolume(item))
				{
					if (printer.Settings.Helpers.HotendCount() > 1)
					{
						var materialIndex = item.WorldMaterialIndex();
						if (materialIndex == -1)
						{
							materialIndex = 0;
						}

						bool isWipeTower = item?.OutputType == PrintOutputTypes.WipeTower;

						// Determine if the given item is outside the bounds of the given extruder
						if (materialIndex < printer.Settings.ToolBounds.Length
							|| isWipeTower)
						{
							var itemAABB = item.WorldAxisAlignedBoundingBox();
							var itemBounds = new RectangleDouble(new Vector2(itemAABB.MinXYZ), new Vector2(itemAABB.MaxXYZ));

							var activeHotends = new HashSet<int>(new[] { materialIndex });

							if (isWipeTower)
							{
								activeHotends.Add(0);
								activeHotends.Add(1);
							}

							// Validate against active hotends
							foreach (var hotendIndex in activeHotends)
							{
								var hotendBounds = printer.Settings.ToolBounds[hotendIndex];
								if (!hotendBounds.Contains(itemBounds))
								{
									// Draw in red outside of the bounds for the hotend
									drawColor = Color.Red.WithAlpha(90);
								}
							}
						}
					}
				}
				else
				{
					// Outside of printer build volume
					drawColor = new Color(drawColor, 65);
				}
			}

			if (drawColor.alpha != 255
				&& item is Object3D item3D)
			{
				item3D.EnsureTransparentSorting();
			}

			if (selectedItem is ISelectableChildContainer selectableChildContainer)
			{
				if (item.AncestorsAndSelf().Any(i => selectableChildContainer.SelectedChildren.Contains(i.ID)))
				{
					drawColor = new Color(drawColor, 200);
				}
			}

			if (!sceneContext.ViewState.ModelView)
			{
				if (modelRenderStyle == ModelRenderStyle.WireframeAndSolid)
				{
					drawColor = gCodeMeshColor;
				}
				else if (modelRenderStyle == ModelRenderStyle.Wireframe)
				{
					drawColor = new Color(gCodeMeshColor, 1);
				}
				else if (modelRenderStyle == ModelRenderStyle.None)
				{
					drawColor = Color.Transparent;
				}
			}

			return drawColor;
		}

		public enum EditorType
		{
			Printer,
			Part
		}

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

		private void DrawGlContent(DrawEventArgs e)
		{
			var gcodeOptions = sceneContext.RendererOptions;

			if (gcodeOptions.GCodeModelView)
			{
				modelRenderStyle = ModelRenderStyle.WireframeAndSolid;
			}
			else
			{
				modelRenderStyle = ModelRenderStyle.None;
			}

			foreach (var drawable in drawables.Where(d => d.DrawStage == DrawStage.First))
			{
				if (drawable.Enabled)
				{
					drawable.Draw(this, e, Matrix4X4.Identity, this.World);
				}
			}

			GLHelper.SetGlContext(this.World, renderSource.TransformToScreenSpace(renderSource.LocalBounds), lighting);

			foreach (var drawable in drawables.Where(d => d.DrawStage == DrawStage.OpaqueContent))
			{
				if (drawable.Enabled)
				{
					drawable.Draw(this, e, Matrix4X4.Identity, this.World);
				}
			}

			// Draw solid objects, extract transparent
			var transparentMeshes = new List<Object3DView>();

			var selectedItem = scene.SelectedItem;
			bool suppressNormalDraw = false;
			if (selectedItem != null)
			{
				// Invoke existing IEditorDraw when iterating items
				if (selectedItem is IEditorDraw editorDraw)
				{
					// TODO: Putting the drawing code in the IObject3D means almost certain bindings to MatterControl in IObject3D. If instead
					// we had a UI layer object that used binding to register scene drawing hooks for specific types, we could avoid the bindings
					editorDraw.DrawEditor(this, transparentMeshes, e, ref suppressNormalDraw);
				}
			}

			foreach (var item in scene.Children)
			{
				if (item.Visible
					&& (item != selectedItem || suppressNormalDraw == false))
				{
					DrawObject(item, transparentMeshes, e);
				}
			}

			if (sceneContext.Printer?.Connection?.serialPort is PrinterEmulator.Emulator emulator)
			{
				void NozzlePositionChanged(object s, EventArgs e2)
				{
					// limit max number of updates per second to 10
					if (UiThread.CurrentTimerMs > lastEmulatorDrawMs + 100)
					{
						UiThread.RunOnIdle(Invalidate);
						// set it to now
						lastEmulatorDrawMs = UiThread.CurrentTimerMs;
					}
				}

				var matrix = Matrix4X4.CreateTranslation(emulator.CurrentPosition + new Vector3(.5, .5, 5));
				GLHelper.Render(emulatorNozzleMesh,
					MaterialRendering.Color(emulator.ExtruderIndex),
					matrix,
					RenderTypes.Shaded,
					matrix * World.ModelviewMatrix);

				if (!emulatorHooked)
				{
					emulator.DestinationChanged += NozzlePositionChanged;
					emulatorHooked = true;
				}

				Closed += (s, e3) => emulator.DestinationChanged -= NozzlePositionChanged;
			}

			transparentMeshes.Sort(BackToFrontXY);

			var bedNormalInViewSpace = Vector3Ex.TransformNormal(Vector3.UnitZ, World.ModelviewMatrix).GetNormal();
			var pointOnBedInViewSpace = Vector3Ex.Transform(new Vector3(10, 10, 0), World.ModelviewMatrix);
			var lookingDownOnBed = Vector3Ex.Dot(bedNormalInViewSpace, pointOnBedInViewSpace) < 0;

			floorDrawable.LookingDownOnBed = lookingDownOnBed;

			if (lookingDownOnBed)
			{
				floorDrawable.Draw(this, e, Matrix4X4.Identity, this.World);
			}

			var wireColor = Color.Transparent;
			switch (modelRenderStyle)
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
					wireColor,
					allowBspRendering: transparentMeshes.Count < 1000);
			}

			if (!lookingDownOnBed)
			{
				floorDrawable.Draw(this, e, Matrix4X4.Identity, this.World);
			}

			DrawInteractionVolumes(e);

			foreach (var drawable in drawables.Where(d => d.DrawStage == DrawStage.TransparentContent))
			{
				if (drawable.Enabled)
				{
					drawable.Draw(this, e, Matrix4X4.Identity, this.World);
				}
			}

			GLHelper.UnsetGlContext();

			// Invoke DrawStage.Last item drawables
			foreach (var item in scene.Children)
			{
				// HACK: Consider how shared code in DrawObject can be reused to prevent duplicate execution
				bool isSelected = selectedItem != null
					&& selectedItem.DescendantsAndSelf().Any((i) => i == item);

				foreach (var itemDrawable in itemDrawables.Where(d => d.DrawStage == DrawStage.Last && d.Enabled))
				{
					itemDrawable.Draw(this, item, isSelected, e, Matrix4X4.Identity, this.World);
				}
			}

			// Invoke DrawStage.Last scene drawables
			foreach (var drawable in drawables.Where(d => d.DrawStage == DrawStage.Last))
			{
				if (drawable.Enabled)
				{
					drawable.Draw(this, e, Matrix4X4.Identity, this.World);
				}
			}
		}

		private void DrawInteractionVolumes(DrawEventArgs e)
		{
			foreach (var item in this.InteractionVolumes.OfType<IGLInteractionElement>())
			{
				item.Visible = !SuppressUiVolumes;
			}

			if (SuppressUiVolumes)
			{
				return;
			}

			// draw on top of anything that is already drawn
			GL.Disable(EnableCap.DepthTest);

			foreach (var interactionVolume in this.InteractionVolumes.OfType<IGLInteractionElement>())
			{
				if (interactionVolume.DrawOnTop)
				{
					interactionVolume.DrawGlContent(new DrawGlContentEventArgs(false, e));
				}
			}

			// Restore DepthTest
			GL.Enable(EnableCap.DepthTest);

			// Draw again setting the depth buffer and ensuring that all the interaction objects are sorted as well as we can
			foreach (var interactionVolume in this.InteractionVolumes.OfType<IGLInteractionElement>())
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
	}

	public class Object3DView
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
