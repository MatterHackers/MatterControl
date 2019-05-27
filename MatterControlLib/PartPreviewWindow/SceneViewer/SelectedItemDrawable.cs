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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedItemDrawable : IDrawableItem, IDisposable
	{
		private readonly InteractiveScene scene;
		private readonly GuiWidget guiWidget;
		private long lastSelectionChangedMs;

		// TODO: Use theme colors
		private Color darkWireframe = new Color("#3334");

		public SelectedItemDrawable(ISceneContext sceneContext, GuiWidget guiWidget)
		{
			this.scene = sceneContext.Scene;
			this.guiWidget = guiWidget;

			// Register listeners
			scene.SelectionChanged += Selection_Changed;
		}

		public bool Enabled { get; set; } = true;

		public string Title { get; } = "Selection Renderer";

		public string Description { get; } = "Render selected item with expanded mesh";

		public DrawStage DrawStage { get; } = DrawStage.TransparentContent;

		public void Dispose()
		{
			// Unregister listeners
			scene.SelectionChanged -= Selection_Changed;
		}

		private void Selection_Changed(object sender, EventArgs e)
		{
			guiWidget.Invalidate();
			lastSelectionChangedMs = UiThread.CurrentTimerMs;
		}

		public void Draw(GuiWidget sender, IObject3D item, bool isSelected, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (isSelected && scene.DrawSelection)
			{
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

					guiWidget.Invalidate();
				}

				this.RenderSelection(item, selectionColor, world);
			}
		}

		private void RenderSelection(IObject3D item, Color selectionColor, WorldView world)
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
			double distBetweenPixelsWorldSpace = world.GetWorldUnitsPerScreenPixelAtPosition(worldCenter);
			var pixelsAccross = worldBounds.Size / distBetweenPixelsWorldSpace;
			var pixelsWant = pixelsAccross + Vector3.One * 4 * Math.Sqrt(2);

			var wantMm = pixelsWant * distBetweenPixelsWorldSpace;

			var scaleMatrix = worldMatrix.ApplyAtPosition(worldCenter, Matrix4X4.CreateScale(
				wantMm.X / worldBounds.XSize,
				wantMm.Y / worldBounds.YSize,
				wantMm.Z / worldBounds.ZSize));

			GLHelper.Render(item.Mesh,
				selectionColor,
				scaleMatrix,
				RenderTypes.Shaded,
				null,
				darkWireframe);

			// restore settings
			GL.CullFace(CullFaceMode.Back);
			GL.Enable(EnableCap.Lighting);
		}
	}
}