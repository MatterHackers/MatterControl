/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.Plugins.EditorTools;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class Object3DControlLayer : GuiWidget, IObject3DControlContext
	{
		private Object3DControlBase mouseDownObject3DControl = null;

		/// <summary>
		/// Gets the mapping for Object3DControls for a given type
		/// </summary>
		private Dictionary<Type, List<IObject3DControl>> Object3DControlMappings { get; } = new Dictionary<Type, List<IObject3DControl>>();

		/// <summary>
		/// Object3DControl Overrides for the selected scene item
		/// </summary>
		private List<IObject3DControl> Object3DControlOverrides = null;

		private Type selectedItemType;

		public WorldView World => sceneContext.World;

		public InteractiveScene Scene => sceneContext.Scene;

		public bool DrawOpenGLContent { get; set; } = true;

		private List<IObject3DControl> DefaultObject3DControls { get; } = new List<IObject3DControl>();

		public IEnumerable<IObject3DControl> Object3DControls
		{
			get
			{
				if (selectedItemType == null)
				{
					return Enumerable.Empty<IObject3DControl>();
				}
				else
				{
					return Object3DControlOverrides ?? DefaultObject3DControls;
				}
			}
		}

		private LightingData lighting = new LightingData();
		private GuiWidget renderSource;

		public Object3DControlLayer(ISceneContext sceneContext, ThemeConfig theme, EditorType editorType = EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.EditorMode = editorType;
			this.theme = theme;

			scene = sceneContext.Scene;

			gCodeMeshColor = new Color(theme.PrimaryAccentColor, 35);

			BuildVolumeColor = new ColorF(.2, .8, .3, .2).ToColor();

			floorDrawable = new FloorDrawable(editorType, sceneContext, this.BuildVolumeColor, theme);

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
					ImageGlPlugin.GetImageGlPlugin(ViewOnlyTexture, true, true, false);
				});
			}

			Object3DControlMappings.Add(typeof(ImageObject3D), new List<IObject3DControl> { new MoveInZControl(this) });
			Object3DControlMappings.Add(typeof(PathObject3D), new List<IObject3DControl> { new PathControl(this) });

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

		public void RegisterObject3DControl(IObject3DControl object3DControl)
		{
			DefaultObject3DControls.Add(object3DControl);
		}

		public void RegisterObject3DControls(IEnumerable<IObject3DControl> Object3DControls)
		{
			DefaultObject3DControls.AddRange(Object3DControls);
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
			foreach (var item in this.Object3DControls)
			{
				item.LostFocus();
			}

			// On selection change, update state for mappings
			selectedItemType = scene.SelectedItem?.GetType();
			Object3DControlOverrides = null;

			if (selectedItemType != null)
			{
				Object3DControlMappings.TryGetValue(selectedItemType, out Object3DControlOverrides);
			}
		}

		public static void RenderBounds(DrawEventArgs e, WorldView world, IEnumerable<BvhIterator> allResults)
		{
			foreach (var bvhIterator in allResults)
			{
				Object3DControlLayer.RenderBounds(e, world, bvhIterator.TransformToWorld, bvhIterator.Bvh, bvhIterator.Depth);
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

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			if (this.Scene.SelectedItem != null
				&& !SuppressObject3DControls
				&& FindHitObject3DControl(ray, out mouseDownObject3DControl, out IntersectInfo info))
			{
				mouseDownObject3DControl.OnMouseDown(new Mouse3DEventArgs(mouseEvent, ray, info));
				SelectedObject3DControl = mouseDownObject3DControl;
			}
			else
			{
				SelectedObject3DControl = null;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (SuppressObject3DControls
				|| !this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info = null;
			var mouseEvent3D = new Mouse3DEventArgs(mouseEvent, ray, info);

			if (MouseDownOnObject3DControlVolume && mouseDownObject3DControl != null)
			{
				mouseDownObject3DControl.OnMouseMove(mouseEvent3D);
			}
			else
			{
				this.FindHitObject3DControl(ray, out Object3DControlBase hitObject3DControl, out info);

				var object3DControls = this.Object3DControls;

				foreach (var object3DControl in object3DControls.OfType<Object3DControlBase>())
				{
					if (hitObject3DControl == object3DControl)
					{
						object3DControl.MouseIsOver = true;
						object3DControl.MouseMoveInfo = info;
						HoveredObject3DControl = object3DControl;
					}
					else
					{
						object3DControl.MouseIsOver = false;
						object3DControl.MouseMoveInfo = null;
					}

					// TODO: Why do non-hit volumes get mouse move?
					object3DControl.OnMouseMove(mouseEvent3D);
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			Invalidate();

			if (SuppressObject3DControls)
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			bool anyObject3DControlVolumeHit = FindHitObject3DControl(ray, out Object3DControlBase object3DControlBase, out IntersectInfo info);
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
					object3DControlBase.OnMouseUp(mouseEvent3D);
				}

				SelectedObject3DControl = null;
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
			Object3DControlOverrides?.Clear();
			Object3DControlMappings.Clear();
			DefaultObject3DControls.Clear();
			drawables.Clear();
			itemDrawables.Clear();
			SelectedObject3DControl = null;
			HoveredObject3DControl = null;
		}

		private bool FindHitObject3DControl(Ray ray, out Object3DControlBase hitObject3DControl, out IntersectInfo info)
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
				foreach (var object3DControlBase in object3DControls.OfType<Object3DControlBase>())
				{
					var insideBounds = new List<IBvhItem>();
					if (object3DControlBase.CollisionVolume != null)
					{
						object3DControlBase.CollisionVolume.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
						if (insideBounds.Contains(info.closestHitObject))
						{
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

		public Object3DControlBase SelectedObject3DControl { get; set; } = null;

		public Object3DControlBase HoveredObject3DControl { get; set; } = null;

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
	}
}