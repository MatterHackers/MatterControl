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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class InteractionLayer : GuiWidget, IInteractionVolumeContext
	{
		private int volumeIndexWithMouseDown = -1;

		public WorldView World { get; }

		public InteractiveScene Scene { get; }

		public event EventHandler<DrawEventArgs> DrawGlOpaqueContent;
		public event EventHandler<DrawEventArgs> DrawGlTransparentContent;

		public bool DoOpenGlDrawing { get; set; } = true;

		// TODO: Collapse into auto-property
		private List<InteractionVolume> interactionVolumes = new List<InteractionVolume>();
		public List<InteractionVolume> InteractionVolumes { get; }

		private UndoBuffer undoBuffer;

		private LightingData lighting = new LightingData();
		private GuiWidget renderSource;

		public InteractionLayer(WorldView world, UndoBuffer undoBuffer, BedConfig sceneContext, ThemeConfig theme, EditorType editorType = EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.Scene = scene = sceneContext.Scene;
			this.World = world;
			this.InteractionVolumes = interactionVolumes;
			this.undoBuffer = undoBuffer;
			this.EditorMode = editorType;
			this.theme = theme;

			gCodeMeshColor = new Color(theme.PrimaryAccentColor, 35);

			// Register listeners
			scene.SelectionChanged += selection_Changed;

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
					var plugin = ImageGlPlugin.GetImageGlPlugin(ViewOnlyTexture, true, true, false);
				});
			}
		}

		internal void SetRenderTarget(GuiWidget renderSource)
		{
			this.renderSource = renderSource;

			// Hook our drawing operation to the renderSource so we can draw unclipped in the source objects bounds. At the time of writing,
			// this mechanism is needed to draw bed items under the semi-transparent right treeview/editor panel
			renderSource.BeforeDraw += renderSource_BeforeDraw;
		}

		// The primary draw hook. Kick off our draw operation when the renderSource fires AfterDraw
		private void renderSource_BeforeDraw(object sender, DrawEventArgs e)
		{
			if (DoOpenGlDrawing)
			{
				GLHelper.SetGlContext(this.World, renderSource.TransformToScreenSpace(renderSource.LocalBounds), lighting);
				OnDrawGlContent(e);
				GLHelper.UnsetGlContext();
			}
		}

		public static void RenderBounds(DrawEventArgs e, WorldView World, IEnumerable<BvhIterator> allResults)
		{
			foreach (var x in allResults)
			{
				for (int i = 0; i < 4; i++)
				{
					Vector3 bottomStartPosition = Vector3Ex.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), x.TransformToWorld);
					var bottomStartScreenPos = World.GetScreenPosition(bottomStartPosition);

					Vector3 bottomEndPosition = Vector3Ex.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner((i + 1) % 4), x.TransformToWorld);
					var bottomEndScreenPos = World.GetScreenPosition(bottomEndPosition);

					Vector3 topStartPosition = Vector3Ex.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), x.TransformToWorld);
					var topStartScreenPos = World.GetScreenPosition(topStartPosition);

					Vector3 topEndPosition = Vector3Ex.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner((i + 1) % 4), x.TransformToWorld);
					var topEndScreenPos = World.GetScreenPosition(topEndPosition);

					e.Graphics2D.Line(bottomStartScreenPos, bottomEndScreenPos, Color.Black);
					e.Graphics2D.Line(topStartScreenPos, topEndScreenPos, Color.Black);
					e.Graphics2D.Line(topStartScreenPos, bottomStartScreenPos, Color.Black);
				}

				ITriangle tri = x.Bvh as ITriangle;
				if (tri != null)
				{
					for (int i = 0; i < 3; i++)
					{
						var vertexPos = tri.GetVertex(i);
						var screenCenter = Vector3Ex.Transform(vertexPos, x.TransformToWorld);
						var screenPos = World.GetScreenPosition(screenCenter);

						e.Graphics2D.Circle(screenPos, 3, Color.Red);
					}
				}
				else
				{
					var center = x.Bvh.GetCenter();
					var worldCenter = Vector3Ex.Transform(center, x.TransformToWorld);
					var screenPos2 = World.GetScreenPosition(worldCenter);
					e.Graphics2D.Circle(screenPos2, 3, Color.Yellow);
					e.Graphics2D.DrawString($"{x.Depth},", screenPos2.X + 12 * x.Depth, screenPos2.Y);
				}
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info;
			if (this.Scene.SelectedItem != null
				&& !SuppressUiVolumes
				&& FindInteractionVolumeHit(ray, out volumeHitIndex, out info))
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				volumeIndexWithMouseDown = volumeHitIndex;
				interactionVolumes[volumeHitIndex].OnMouseDown(mouseEvent3D);
				SelectedInteractionVolume = interactionVolumes[volumeHitIndex];
			}
			else
			{
				SelectedInteractionVolume = null;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (SuppressUiVolumes
				|| !this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info = null;
			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				interactionVolumes[volumeIndexWithMouseDown].OnMouseMove(mouseEvent3D);
			}
			else
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

				int volumeHitIndex;
				FindInteractionVolumeHit(ray, out volumeHitIndex, out info);

				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					if (i == volumeHitIndex)
					{
						interactionVolumes[i].MouseOver = true;
						interactionVolumes[i].MouseMoveInfo = info;

						HoveredInteractionVolume = interactionVolumes[i];
					}
					else
					{
						interactionVolumes[i].MouseOver = false;
						interactionVolumes[i].MouseMoveInfo = null;
					}

					interactionVolumes[i].OnMouseMove(mouseEvent3D);
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			Invalidate();

			if (SuppressUiVolumes)
			{
				return;
			}

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info;
			bool anyInteractionVolumeHit = FindInteractionVolumeHit(ray, out volumeHitIndex, out info);
			MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				interactionVolumes[volumeIndexWithMouseDown].OnMouseUp(mouseEvent3D);
				SelectedInteractionVolume = null;

				volumeIndexWithMouseDown = -1;
			}
			else
			{
				volumeIndexWithMouseDown = -1;

				if (anyInteractionVolumeHit)
				{
					interactionVolumes[volumeHitIndex].OnMouseUp(mouseEvent3D);
				}
				SelectedInteractionVolume = null;
			}

			base.OnMouseUp(mouseEvent);
		}

		private bool FindInteractionVolumeHit(Ray ray, out int interactionVolumeHitIndex, out IntersectInfo info)
		{
			interactionVolumeHitIndex = -1;
			if (interactionVolumes.Count == 0 || interactionVolumes[0].CollisionVolume == null)
			{
				info = null;
				return false;
			}

			// TODO: Rewrite as projection without extra list
			List<IPrimitive> uiTraceables = new List<IPrimitive>();
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				if (interactionVolume.CollisionVolume != null)
				{
					IPrimitive traceData = interactionVolume.CollisionVolume;
					uiTraceables.Add(new Transform(traceData, interactionVolume.TotalTransform));
				}
			}

			IPrimitive allUiObjects = BoundingVolumeHierarchy.CreateNewHierachy(uiTraceables);

			info = allUiObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					List<IBvhItem> insideBounds = new List<IBvhItem>();
					if (interactionVolumes[i].CollisionVolume != null)
					{
						interactionVolumes[i].CollisionVolume.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
						if (insideBounds.Contains(info.closestHitObject))
						{
							interactionVolumeHitIndex = i;
							return true;
						}
					}
				}
			}

			return false;
		}

		public void AddTransformSnapshot(Matrix4X4 originalTransform)
		{
			var selectedItem = Scene.SelectedItem;
			if (selectedItem != null && selectedItem.Matrix != originalTransform)
			{
				this.undoBuffer.Add(new TransformCommand(selectedItem, originalTransform, Scene.SelectedItem.Matrix));
			}
		}

		public bool SuppressUiVolumes { get; set; } = false;

		public bool MouseDownOnInteractionVolume => SelectedInteractionVolume != null;

		public InteractionVolume SelectedInteractionVolume { get; set; } = null;
		public InteractionVolume HoveredInteractionVolume { get; set; } = null;

		public double SnapGridDistance
		{
			get
			{
				if(string.IsNullOrEmpty(UserSettings.Instance.get(UserSettingsKey.SnapGridDistance)))
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

		private void OnDrawGlContent(DrawEventArgs e)
		{
			DrawGlOpaqueContent?.Invoke(this, e);

			this.Draw_GlTransparentContent(this, e);
		}
	}
}