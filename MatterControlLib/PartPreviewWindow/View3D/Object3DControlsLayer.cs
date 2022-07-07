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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    [Flags]
	public enum ControlTypes
	{
		MoveInZ = 1 << 0,
		RotateXYZ = 1 << 1,
		RotateZ = 1 << 2,
		ScaleMatrixXY = 1 << 3,
		Shadow = 1 << 4,
		SnappingIndicators = 1 << 5,

		Standard2D = MoveInZ | Shadow | SnappingIndicators | RotateZ | ScaleMatrixXY
	}

	public class Object3DControlsLayer : GuiWidget, IObject3DControlContext
	{
		private IObject3DControl mouseDownObject3DControl = null;

		public WorldView World => sceneContext.World;

		public InteractiveScene Scene => sceneContext.Scene;

		public bool DrawOpenGLContent { get; set; } = true;

		public SafeList<IObject3DControl> Object3DControls { get; set; } = new SafeList<IObject3DControl>();

		private readonly LightingData lighting = new LightingData();
		private GuiWidget renderSource;

		public Object3DControlsLayer(ISceneContext sceneContext, ThemeConfig theme, EditorType editorType = EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.EditorMode = editorType;

			scene = sceneContext.Scene;

			gCodeMeshColor = new Color(theme.PrimaryAccentColor, 35);

			BuildVolumeColor = new ColorF(.2, .8, .3, .2).ToColor();

			floorDrawable = new FloorDrawable(editorType, sceneContext, this.BuildVolumeColor, theme);

			if (stripeTexture == null)
			{
				// open gl can only be run on the ui thread so make sure it is on it by using RunOnIdle
				UiThread.RunOnIdle(() =>
				{
					stripeTexture = new ImageBuffer(32, 32, 32);
					var graphics2D = stripeTexture.NewGraphics2D();
					graphics2D.Clear(Color.White);
					graphics2D.FillRectangle(0, 0, stripeTexture.Width / 2, stripeTexture.Height, Color.LightGray);
					// request the texture so we can set it to repeat
					ImageGlPlugin.GetImageGlPlugin(stripeTexture, true, true, false);
				});
			}

			// Register listeners
			sceneContext.Scene.SelectionChanged += this.Scene_SelectionChanged;
			if (sceneContext.Printer != null)
			{
				sceneContext.Printer.Settings.SettingChanged += this.Settings_SettingChanged;
			}
		}

		private void Settings_SettingChanged(object sender, StringEventArgs e)
		{
			string settingsKey = e.Data;

			// Invalidate bed textures on related settings change
			if (settingsKey == SettingsKey.t0_inset
				|| settingsKey == SettingsKey.t1_inset
				|| settingsKey == SettingsKey.bed_size
				|| settingsKey == SettingsKey.print_center)
			{
				this.Invalidate();
			}
		}

		public void RegisterDrawable(IDrawable drawable)
		{
			drawables.Add(drawable);
		}

		public IEnumerable<IDrawable> Drawables => drawables;

		public IEnumerable<IDrawableItem> ItemDrawables => itemDrawables;

		internal void SetRenderTarget(GuiWidget renderSource)
		{
			// Unregister listener
			if (renderSource != null)
			{
				renderSource.BeforeDraw -= RenderSource_BeforeDraw;
			}

			this.renderSource = renderSource;

			// Hook our drawing operation to the renderSource so we can draw unclipped in the source objects bounds. At the time of writing,
			// this mechanism is needed to draw bed items under the semi-transparent right treeview/editor panel
			renderSource.BeforeDraw += RenderSource_BeforeDraw;
		}

		// The primary draw hook. Kick off our draw operation when the renderSource fires AfterDraw
		private void RenderSource_BeforeDraw(object sender, DrawEventArgs e)
		{
			if (this.DrawOpenGLContent)
			{
				this.DrawGlContent(e);
			}
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			var selectedItem = scene.SelectedItem;
			UiThread.RunOnIdle(() =>
			{
				DisposeCurrentSelectionObject3DControls();

				// On selection change, update state for mappings
				Object3DControls.Clear();

				if (selectedItem is IObject3DControlsProvider provider)
				{
					provider.AddObject3DControls(this);
				}
				else
				{
					// add default controls
					Object3DControls.Add(new ScaleMatrixTopControl(this));

					AddControls(ControlTypes.ScaleMatrixXY);

					AddControls(ControlTypes.RotateXYZ
						| ControlTypes.MoveInZ
						| ControlTypes.Shadow
						| ControlTypes.SnappingIndicators);
				}
			});
		}

		private List<Action<Graphics2D>> drawBeforeCallbacks = new List<Action<Graphics2D>>();

		public void DrawBeforeGui(Action<Graphics2D> drawFunction)
		{
			drawBeforeCallbacks.Add(drawFunction);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			foreach (var draw in drawBeforeCallbacks)
			{
				draw(graphics2D);
			}

			drawBeforeCallbacks.Clear();

			base.OnDraw(graphics2D);
		}

		public void AddHeightControl(IObject3D item, DoubleOrExpression width, DoubleOrExpression depth, DoubleOrExpression height)
		{
			Func<double> getWidth = () => width.Value(item);
			Action<double> setWidth = (newWidth) => width.Expression = newWidth.ToString();
			Func<double> getDepth = () => depth.Value(item);
			Action<double> setDepth = (newDepth) => depth.Expression = newDepth.ToString();
			Func<double> getHeight = null;
			Action<double> setHeight = null;
			if (height != null)
			{
				getHeight = () => height.Value(item);
				setHeight = (newHeight) => height.Expression = newHeight.ToString();
			}

			Object3DControls.Add(new ScaleHeightControl(this, getWidth, setWidth, getDepth, setDepth, getHeight, setHeight));
		}

		public void AddWidthDepthControls(IObject3D item, DoubleOrExpression width, DoubleOrExpression depth, DoubleOrExpression height)
		{
			Func<double> getWidth = () => width.Value(item);
			Action<double> setWidth = (newWidth) => width.Expression = newWidth.ToString();
			Func<double> getDepth = () => depth.Value(item);
			Action<double> setDepth = (newDepth) => depth.Expression = newDepth.ToString();
			Func<double> getHeight = null;
			Action<double> setHeight = null;
			if (height != null)
			{
				getHeight = () => height.Value(item);
				setHeight = (newHeight) => height.Expression = newHeight.ToString();
			}

			if (width != null 
				&& !width.IsEquation
				&& depth != null
				&& !depth.IsEquation)
			{
				for (int i = 0; i < 4; i++)
				{
					Object3DControls.Add(new ScaleWidthDepthCornerControl(this, getWidth, setWidth, getDepth, setDepth, getHeight, setHeight, i));
					Object3DControls.Add(new ScaleWidthDepthEdgeControl(this, getWidth, setWidth, getDepth, setDepth, getHeight, setHeight, i));
				}
			}
			else
			{
				// if the width is set and a constant
				if (width != null && !width.IsEquation)
				{
					// add width side controls
					Object3DControls.Add(new ScaleWidthDepthEdgeControl(this, getWidth, setWidth, null, null, getHeight, setHeight, 1));
					Object3DControls.Add(new ScaleWidthDepthEdgeControl(this, getWidth, setWidth, null, null, getHeight, setHeight, 3));
				}

				// if the depth is set and a constant
				if (depth != null && !depth.IsEquation)
				{
					// add depth side controls
					Object3DControls.Add(new ScaleWidthDepthEdgeControl(this, null, null, getDepth, setDepth, getHeight, setHeight, 0));
					Object3DControls.Add(new ScaleWidthDepthEdgeControl(this, null, null, getDepth, setDepth, getHeight, setHeight, 2));
				}
			}
		}

		public void AddControls(ControlTypes controls)
		{
			if (controls.HasFlag(ControlTypes.RotateXYZ))
			{
				for (int i = 0; i < 3; i++)
				{
					Object3DControls.Add(new RotateCornerControl(this, i));
				}
			}

			if (controls.HasFlag(ControlTypes.RotateZ))
			{
				Object3DControls.Add(new RotateCornerControl(this, 2));
			}

			if (controls.HasFlag(ControlTypes.MoveInZ))
			{
				Object3DControls.Add(new MoveInZControl(this));
			}

			if (controls.HasFlag(ControlTypes.ScaleMatrixXY))
			{
				for (int i = 0; i < 4; i++)
				{
					Object3DControls.Add(new ScaleMatrixCornerControl(this, i));
					Object3DControls.Add(new ScaleMatrixEdgeControl(this, i));
				}
			}

			if (controls.HasFlag(ControlTypes.Shadow))
			{
				Object3DControls.Add(new SelectionShadow(this));
			}

			if (controls.HasFlag(ControlTypes.SnappingIndicators))
			{
				Object3DControls.Add(new SnappingIndicators(this));
			}
		}

		public void AddWorldRotateControls()
		{
		}

		private void DisposeCurrentSelectionObject3DControls()
		{
			foreach (var item in this.Object3DControls)
			{
				item.Dispose();
			}

			this.Object3DControls.Clear();
		}

		public static void RenderBounds(DrawEventArgs e, WorldView world, IEnumerable<BvhIterator> allResults)
		{
			foreach (var bvhIterator in allResults)
			{
                RenderBounds(e, world, bvhIterator.TransformToWorld, bvhIterator.Bvh, bvhIterator.Depth);
			}
		}

		public static void RenderBounds(DrawEventArgs e, WorldView world, Matrix4X4 transformToWorld, IBvhItem bvh, int depth = int.MinValue)
		{
			for (int i = 0; i < 4; i++)
			{
				Vector3 bottomStartPosition = Vector3Ex.Transform(bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), transformToWorld);
				var bottomStartScreenPos = world.GetScreenPosition(bottomStartPosition);

				Vector3 bottomEndPosition = Vector3Ex.Transform(bvh.GetAxisAlignedBoundingBox().GetBottomCorner((i + 1) % 4), transformToWorld);
				var bottomEndScreenPos = world.GetScreenPosition(bottomEndPosition);

				Vector3 topStartPosition = Vector3Ex.Transform(bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), transformToWorld);
				var topStartScreenPos = world.GetScreenPosition(topStartPosition);

				Vector3 topEndPosition = Vector3Ex.Transform(bvh.GetAxisAlignedBoundingBox().GetTopCorner((i + 1) % 4), transformToWorld);
				var topEndScreenPos = world.GetScreenPosition(topEndPosition);

				e.Graphics2D.Line(bottomStartScreenPos, bottomEndScreenPos, Color.Black);
				e.Graphics2D.Line(topStartScreenPos, topEndScreenPos, Color.Black);
				e.Graphics2D.Line(topStartScreenPos, bottomStartScreenPos, Color.Black);
			}

			if (bvh is ITriangle tri)
			{
				for (int i = 0; i < 3; i++)
				{
					var vertexPos = tri.GetVertex(i);
					var screenCenter = Vector3Ex.Transform(vertexPos, transformToWorld);
					var screenPos = world.GetScreenPosition(screenCenter);

					e.Graphics2D.Circle(screenPos, 3, Color.Red);
				}
			}
			else
			{
				var center = bvh.GetCenter();
				var worldCenter = Vector3Ex.Transform(center, transformToWorld);
				var screenPos2 = world.GetScreenPosition(worldCenter);

				if (depth != int.MinValue)
				{
					e.Graphics2D.Circle(screenPos2, 3, Color.Yellow);
					e.Graphics2D.DrawString($"{depth},", screenPos2.X + 12 * depth, screenPos2.Y);
				}
			}
		}

		private bool CanSelectObject()
		{
			var view3D = this.Parents<View3DWidget>().First();
			if (view3D != null)
			{
				return view3D.TrackballTumbleWidget.TransformState == VectorMath.TrackBall.TrackBallTransformType.None;
			}

			return true;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			var ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			if (this.Scene.SelectedItem != null
				&& CanSelectObject()
				&& !SuppressObject3DControls
				&& FindHitObject3DControl(ray, out mouseDownObject3DControl, out IntersectInfo info))
			{
				mouseDownObject3DControl.OnMouseDown(new Mouse3DEventArgs(mouseEvent, ray, info));
				SelectedObject3DControl = mouseDownObject3DControl;

				// Needed for testing DimensionsWorkWhenNoSheet to work (more) reliably. Otherwise, OnMouseMove's defered update might not pick up the hover in time.
				HoveredObject3DControl = mouseDownObject3DControl;
			}
			else
			{
				SelectedObject3DControl = null;
			}
		}

		Vector2 lastMouseMovePosition;
		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			lastMouseMovePosition = mouseEvent.Position;
			base.OnMouseMove(mouseEvent);

			if (SuppressObject3DControls
				|| !this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y)
				|| !CanSelectObject())
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info = null;
			var mouseEvent3D = new Mouse3DEventArgs(mouseEvent, ray, info);

			if (MouseDownOnObject3DControlVolume && mouseDownObject3DControl != null)
			{
				mouseDownObject3DControl.OnMouseMove(mouseEvent3D, false);
			}
			else
			{
				this.FindHitObject3DControl(ray, out IObject3DControl hitObject3DControl, out _);

				var object3DControls = this.Object3DControls;

				var overControl = false;
				foreach (var object3DControl in object3DControls)
				{
					if (hitObject3DControl == object3DControl
						&& hitObject3DControl.Visible)
					{
						overControl = true;

						// we have found the control that got hit, wait 200 ms to see if we are still over the same control
						UiThread.RunOnIdle(() =>
						{
							var ray2 = this.World.GetRayForLocalBounds(lastMouseMovePosition);
							this.FindHitObject3DControl(ray2, out IObject3DControl stillOver3DControl, out _);

							if (stillOver3DControl == hitObject3DControl)
							{
								// we are over the same control as the last mouse move so set the hovered object to it
								HoveredObject3DControl = object3DControl;
								object3DControl.OnMouseMove(mouseEvent3D, true);
							}
						}, .2);
					}
					else
					{
						object3DControl.OnMouseMove(mouseEvent3D, false);
					}
				}

				if (overControl
					&& hitObject3DControl is Object3DControl object3DControl2
					&& object3DControl2.RootSelection != null)
				{
					ApplicationController.Instance.UiHint = "Click to edit values".Localize();
				}
				else if (ApplicationController.Instance.UiHint == "Click to edit values".Localize())
				{
					ApplicationController.Instance.UiHint = "";
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			Invalidate();

			if (!SuppressObject3DControls
				&& CanSelectObject())
			{
				Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
				bool anyObject3DControlVolumeHit = FindHitObject3DControl(ray, out IObject3DControl object3DControl, out IntersectInfo info);
				var mouseEvent3D = new Mouse3DEventArgs(mouseEvent, ray, info);

				if (MouseDownOnObject3DControlVolume && mouseDownObject3DControl != null)
				{
					mouseDownObject3DControl.OnMouseUp(mouseEvent3D);
					SelectedObject3DControl = null;

					mouseDownObject3DControl = null;
				}
				else
				{
					mouseDownObject3DControl = null;

					if (anyObject3DControlVolumeHit)
					{
						object3DControl.OnMouseUp(mouseEvent3D);
					}

					SelectedObject3DControl = null;
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			sceneContext.Scene.SelectionChanged -= this.Scene_SelectionChanged;
			if (renderSource != null)
			{
				renderSource.BeforeDraw -= RenderSource_BeforeDraw;
				renderSource = null;
			}

			if (sceneContext.Printer != null)
			{
				sceneContext.Printer.Settings.SettingChanged -= this.Settings_SettingChanged;
			}

			// If implemented, invoke Dispose on Drawables
			foreach (var item in drawables)
			{
				if (item is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}

			foreach (var item in itemDrawables)
			{
				if (item is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}

			base.OnClosed(e);

			// Clear lists and references
			DisposeCurrentSelectionObject3DControls();
			drawables.Clear();
			itemDrawables.Clear();
			SelectedObject3DControl = null;
			HoveredObject3DControl = null;
		}

		private bool FindHitObject3DControl(Ray ray, out IObject3DControl hitObject3DControl, out IntersectInfo info)
		{
			var object3DControls = this.Object3DControls;

			hitObject3DControl = null;

			if (!object3DControls.Any())
			{
				info = null;
				return false;
			}

			var traceables = new List<ITraceable>();
			foreach (var object3DControl in object3DControls)
			{
				ITraceable traceable = object3DControl.GetTraceable();
				if (traceable != null)
				{
					traceables.Add(traceable);
				}
			}

			if (traceables.Count <= 0)
			{
				info = null;
				return false;
			}

			var bvhHierachy = BoundingVolumeHierarchy.CreateNewHierachy(traceables);

			info = bvhHierachy.GetClosestIntersection(ray);
			if (info != null)
			{
				// we hit some part of the collection of controls, figure out which one
				foreach (var object3DControlBase in object3DControls)
				{
					var insideBounds = new List<IBvhItem>();
					var traceable = object3DControlBase.GetTraceable();
					if (traceable != null)
					{
						traceable.GetContained(insideBounds, info.ClosestHitObject.GetAxisAlignedBoundingBox());
						if (insideBounds.Contains(info.ClosestHitObject))
						{
							// we hit the control that has the hit point within its bounds
							hitObject3DControl = object3DControlBase;
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool SuppressObject3DControls { get; set; } = false;

		public bool MouseDownOnObject3DControlVolume => SelectedObject3DControl != null;

		public IObject3DControl SelectedObject3DControl { get; set; } = null;

		public IObject3DControl HoveredObject3DControl { get; set; } = null;

		public double SnapGridDistance
		{
			get
			{
				if (string.IsNullOrEmpty(UserSettings.Instance.get(UserSettingsKey.SnapGridDistance)))
				{
					return 1;
				}

				return UserSettings.Instance.GetValue<double>(UserSettingsKey.SnapGridDistance);
			}

			set
			{
				UserSettings.Instance.set(UserSettingsKey.SnapGridDistance, value.ToString());
			}
		}

		public GuiWidget GuiSurface => this;

		private static ImageBuffer stripeTexture;

		private Color lightWireframe = new Color("#aaa4");
		private Color darkWireframe = new Color("#3334");
		private Color gCodeMeshColor;

		private readonly InteractiveScene scene;

		private readonly ISceneContext sceneContext;

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
				new FrustumDrawable(),
				new Object3DControlBoundingBoxesDrawable(),
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
			foreach (IObject3DControl child in this.Object3DControls)
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
					&& child is Object3DControl object3DControl
					&& object3DControl.CollisionVolume != null)
				{
					AxisAlignedBoundingBox bounds = object3DControl.CollisionVolume.GetAxisAlignedBoundingBox();
					bounds = bounds.NewTransformed(object3DControl.TotalTransform);

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
					AxisAlignedBoundingBox bounds = child.GetBVHData().GetAxisAlignedBoundingBox();

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
				// check if the object should have a stripe texture
				ValidateStripeTexturing(item);

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
						|| item.Parents().Any(p => p == selectedItem));

				// Invoke all item Drawables
				foreach (var drawable in itemDrawables.Where(d => d.DrawStage != DrawStage.Last && d.Enabled))
				{
					drawable.Draw(this, item, isSelected, e, Matrix4X4.Identity, this.World);
				}

				// turn lighting back on after rendering selection outlines
				GL.Enable(EnableCap.Lighting);
				GL.Disable(EnableCap.Blend);
			}
		}

		private static void ValidateStripeTexturing(IObject3D item)
		{
			// if there is no stripe texture built or the item is locked
			if (stripeTexture == null
				|| item.Mesh.Faces.Count == 0)
			{
				// exit and wait for a later time
				return;
			}

			// if the item is not currently be processed
			if (!item.RebuildLocked)
			{
				item.Mesh.FaceTextures.TryGetValue(0, out FaceTextureData faceTexture);
				bool faceIsTextured = faceTexture?.image != null;

				// if persistable and has a stripe texture, remove the stripe texture
				if (faceIsTextured
					&& item.WorldPersistable()
					&& item.WorldOutputType() != PrintOutputTypes.Hole)
				{
					// make sure it does not have a stripe texture
					using (item.RebuildLock())
					{
						item.Mesh.RemoveTexture(stripeTexture, 0);
					}
				}
				else if (!faceIsTextured
					&& !item.RebuildLocked 
					// and it is protected or a hole
					&& (!item.WorldPersistable() || item.WorldOutputType() == PrintOutputTypes.Hole))
				{
					// add a stripe texture if it does not have one
					Task.Run(() =>
					{
						// put on the stripe texture
						var aabb = item.Mesh.GetAxisAlignedBoundingBox();
						var matrix = Matrix4X4.CreateScale(.5, .5, 1);
						matrix *= Matrix4X4.CreateRotationZ(MathHelper.Tau / 8);
						// make sure it has it's own copy of the mesh
						using (item.RebuildLock())
						{
							// we make a copy so that we don't modify the global instance of a mesh
							item.Mesh = item.Mesh.Copy(CancellationToken.None);
							item.Mesh.PlaceTexture(stripeTexture, matrix);
						}
					});
				}
			}
		}

		private Color GetItemColor(IObject3D item, IObject3D selectedItem)
		{
			var drawColor = item.WorldColor();
			var drawColorWithOutputType = item.WorldColor(checkOutputType: true);
			if (drawColor != drawColorWithOutputType)
            {
				// color bering set by output type
				drawColor = drawColorWithOutputType;
            }
			else if (sceneContext.ViewState.RenderType == RenderTypes.Materials)
			{
				// check if we should be rendering materials (this overrides the other colors)
				drawColor = MaterialRendering.Color(sceneContext.Printer, item.WorldMaterialIndex());
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
								if (printer?.Settings?.ToolBounds != null
									&& hotendIndex < printer.Settings.ToolBounds.Length)
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
				}
				else
				{
					// Outside of printer build volume
					drawColor = new Color(drawColor, 65);
				}
			}

			if (drawColor.alpha < 255
				&& drawColor.alpha > 0
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

		private Matrix4X4 GetEmulatorNozzleTransform()
		{
			var emulator = (PrinterEmulator.Emulator)sceneContext.Printer.Connection.serialPort;
			return Matrix4X4.CreateTranslation(emulator.CurrentPosition + new Vector3(.5, .5, 5));
		}

		private HashSet<IObject3D> editorDrawItems = new HashSet<IObject3D>();
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
			editorDrawItems.Clear();
			editorDrawItems.Add(selectedItem);

			foreach (var item in scene.Children)
			{
				if (item.Visible)
				{
					DrawObject(item, transparentMeshes, e);
				}
			}

			foreach (var item in scene.Descendants().Where(i => i is IEditorDrawControled customEditorDraw1 && customEditorDraw1.DoEditorDraw(i == selectedItem)))
			{
				editorDrawItems.Add(item);
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

				var matrix = GetEmulatorNozzleTransform();
				GLHelper.Render(emulatorNozzleMesh,
					MaterialRendering.Color(sceneContext.Printer, emulator.ExtruderIndex),
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
			floorDrawable.LookingDownOnBed = Vector3Ex.Dot(bedNormalInViewSpace, pointOnBedInViewSpace) < 0;

			floorDrawable.SelectedObjectUnderBed = false;
			if (selectedItem != null)
			{
				var aabb = selectedItem.GetAxisAlignedBoundingBox();
				if (aabb.MinXYZ.Z < 0)
				{
					floorDrawable.SelectedObjectUnderBed = true;
				}
			}

			var renderBedTransparent = !floorDrawable.LookingDownOnBed || floorDrawable.SelectedObjectUnderBed;

			if (renderBedTransparent)
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

			// Add transparent draws to the editor
			foreach (var item in editorDrawItems)
			{
				// Invoke existing IEditorDraw when iterating items
				if (item is ICustomEditorDraw cutomEditorDraw)
				{
					cutomEditorDraw.AddEditorTransparents(this, transparentMeshes, e);
				}
			}

			// Draw transparent objects
			foreach (var item in transparentMeshes)
			{
				GL.Enable(EnableCap.Lighting);

				var object3D = item.Object3D;
				GLHelper.Render(
					object3D.Mesh,
					item.Color,
					object3D.WorldMatrix(),
					RenderTypes.Outlines,
					object3D.WorldMatrix() * World.ModelviewMatrix,
					wireColor,
					allowBspRendering: transparentMeshes.Count < 1000,
					forceCullBackFaces: false);
			}

			if (!renderBedTransparent)
			{
				floorDrawable.Draw(this, e, Matrix4X4.Identity, this.World);
			}

			// Draw the editor items in the same scope as the 3D Controls
			foreach (var item in editorDrawItems)
			{
				// Invoke existing IEditorDraw when iterating items
				if (item is IEditorDraw editorDraw)
				{
					editorDraw.DrawEditor(this, e);
				}
			}

			DrawObject3DControlVolumes(e);

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

		private void DrawObject3DControlVolumes(DrawEventArgs e)
		{
			this.Object3DControls.Modify((currentControls) =>
			{
				foreach (var item in currentControls)
				{
					item.Visible = !SuppressObject3DControls;
				}

				if (SuppressObject3DControls)
				{
					return;
				}

				// draw on top of anything that is already drawn
				GL.Disable(EnableCap.DepthTest);

				foreach (var object3DControl in currentControls)
				{
					if (object3DControl.DrawOnTop)
					{
						object3DControl.Draw(new DrawGlContentEventArgs(false, Constants.Controls3DAlpha, e));
					}
				}

				// Restore DepthTest
				GL.Enable(EnableCap.DepthTest);

				// Draw again setting the depth buffer and ensuring that all the interaction objects are sorted as well as we can
				foreach (var object3DVolume in currentControls)
				{
					object3DVolume.Draw(new DrawGlContentEventArgs(true, 255, e));
				}
			});
		}

		public enum ModelRenderStyle
		{
			Solid,
			Wireframe,
			WireframeAndSolid,
			None
		}

		public List<AxisAlignedBoundingBox> MakeListOfObjectControlBoundingBoxes()
		{
			var selectedItem = scene.SelectedItem;

			var aabbs = new List<AxisAlignedBoundingBox>(100);

			foreach (var ctrl in Object3DControls)
			{
				aabbs.Add(ctrl.GetWorldspaceAABB());
			}

			if (selectedItem is IEditorDraw editorDraw)
			{
				aabbs.Add(editorDraw.GetEditorWorldspaceAABB(this));
			}

			foreach (var ctrl in scene.Descendants())
			{
				if (ctrl is ICustomEditorDraw customEditorDraw1 && customEditorDraw1.DoEditorDraw(ctrl == selectedItem))
				{
					if (ctrl is IEditorDraw editorDraw2)
					{
						aabbs.Add(editorDraw2.GetEditorWorldspaceAABB(this));
					}
				}
			}

			foreach (var ctrl in drawables)
			{
				if (ctrl.Enabled)
				{
					aabbs.Add(ctrl.GetWorldspaceAABB());
				}
			}

			foreach (var obj in scene.Children)
			{
				if (obj.Visible)
				{
					foreach (var item in obj.VisibleMeshes())
					{
						bool isSelected = selectedItem != null
							&& (item == selectedItem
								|| item.Parents().Any(p => p == selectedItem));

						// Invoke all item Drawables
						foreach (var drawable in itemDrawables)
						{
							var selectedItemDrawable = drawable as SelectedItemDrawable;
							if ((selectedItemDrawable != null && isSelected) 
								|| (selectedItemDrawable == null && drawable.Enabled))
							{
								aabbs.Add(drawable.GetWorldspaceAABB(item, isSelected, this.World));
							}
						}
					}
				}
			}

			aabbs.Add(floorDrawable.GetWorldspaceAABB());

			return aabbs;
		}

		public AxisAlignedBoundingBox GetPrinterNozzleAABB()
		{
			if (sceneContext.Printer?.Connection?.serialPort is PrinterEmulator.Emulator emulator)
			{
				return emulatorNozzleMesh.GetAxisAlignedBoundingBox().NewTransformed(GetEmulatorNozzleTransform());
			}
			
			return AxisAlignedBoundingBox.Empty();
		}
	}
}
