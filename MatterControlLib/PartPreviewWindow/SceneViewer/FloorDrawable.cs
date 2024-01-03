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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class FloorDrawable : IDrawable, IDisposable
	{
		private ISceneContext sceneContext;
		private Object3DControlsLayer.EditorType editorType;
		private ThemeConfig theme;
		private PrinterConfig printer;

		private Color buildVolumeColor;

		private int activeBedToolClippingImage = int.MinValue;

		private ImageBuffer[] bedTextures = null;

		private bool loadingTextures = false;

		private const int GridSize = 600;

		public FloorDrawable(Object3DControlsLayer.EditorType editorType, ISceneContext sceneContext, Color buildVolumeColor, ThemeConfig theme)
		{
			this.buildVolumeColor = buildVolumeColor;
			this.sceneContext = sceneContext;
			this.editorType = editorType;
			this.theme = theme;
			this.printer = sceneContext.Printer;

			// Register listeners
			if (printer != null)
			{
				printer.Settings.SettingChanged += this.Settings_SettingChanged;
			}
		}

		public bool Enabled { get; set; }

		public string Title { get; } = "Render Floor";

		public string Description { get; } = "Render a plane or bed floor";

		// TODO: Investigate if stage should really change dynamically based on lookingDownOnBed
		public DrawStage DrawStage { get; } = DrawStage.First;

		public bool LookingDownOnBed { get; set; }
        public bool SelectedObjectUnderBed { get; set; }

        public void Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (!sceneContext.RendererOptions.RenderBed)
			{
				return;
			}

			if (editorType == Object3DControlsLayer.EditorType.Printer)
			{
				var alpha = 255;
				if (SelectedObjectUnderBed)
                {
					alpha = 200;
                }
				if (!LookingDownOnBed)
				{
					alpha = 32;
				}

				GL.Disable(EnableCap.Lighting);
				GLHelper.Render(
					sceneContext.Mesh,
					Color.White.WithAlpha(alpha),
					RenderTypes.Shaded,
					world.ModelviewMatrix,
					forceCullBackFaces: false);
				GL.Enable(EnableCap.Lighting);

				if (sceneContext.PrinterShape != null)
				{
					GLHelper.Render(sceneContext.PrinterShape, theme.BedColor, RenderTypes.Shaded, world.ModelviewMatrix);
				}

				if (sceneContext.BuildVolumeMesh != null && sceneContext.RendererOptions.RenderBuildVolume)
				{
					GLHelper.Render(sceneContext.BuildVolumeMesh, buildVolumeColor, RenderTypes.Shaded, world.ModelviewMatrix);
				}
			}
			else
			{
				int width = GridSize;

				GL.Disable(EnableCap.Lighting);
				GL.Disable(EnableCap.CullFace);

				var bedColor = LookingDownOnBed ? theme.BedColor : theme.UnderBedColor;

				if (bedColor.Alpha0To1 < 1)
				{
					GL.Enable(EnableCap.Blend);
				}
				else
				{
					GL.Disable(EnableCap.Blend);
				}

				// Draw grid background with active BedColor
				GL.Color4(bedColor);
				GL.Begin(BeginMode.TriangleStrip);
				GL.Vertex3(-width, -width, 0);
				GL.Vertex3(-width, width, 0);
				GL.Vertex3(width, -width, 0);
				GL.Vertex3(width, width, 0);
				GL.End();

				GL.Disable(EnableCap.Texture2D);
				GL.Disable(EnableCap.Blend);

				GL.Begin(BeginMode.Lines);
				{
					GL.Color4(theme.BedGridColors.Line);

					for (int i = -width; i <= width; i += 50)
					{
						GL.Vertex3(i, width, 0);
						GL.Vertex3(i, -width, 0);

						GL.Vertex3(width, i, 0);
						GL.Vertex3(-width, i, 0);
					}

					// X axis
					GL.Color4(theme.BedGridColors.Red);
					GL.Vertex3(width, 0, 0);
					GL.Vertex3(-width, 0, 0);

					// Y axis
					GL.Color4(theme.BedGridColors.Green);
					GL.Vertex3(0, width, 0);
					GL.Vertex3(0, -width, 0);

					// Z axis
					GL.Color4(theme.BedGridColors.Blue);
					GL.Vertex3(0, 0, 10);
					GL.Vertex3(0, 0, -10);
				}
				GL.End();
			}
		}

		public AxisAlignedBoundingBox GetWorldspaceAABB()
		{
			if (!sceneContext.RendererOptions.RenderBed)
			{
				return AxisAlignedBoundingBox.Empty();
			}
			else if (editorType == Object3DControlsLayer.EditorType.Printer)
			{
				AxisAlignedBoundingBox box = sceneContext.Mesh != null ? sceneContext.Mesh.GetAxisAlignedBoundingBox() : AxisAlignedBoundingBox.Empty();

				if (sceneContext.PrinterShape != null)
				{
					box = AxisAlignedBoundingBox.Union(box, sceneContext.PrinterShape.GetAxisAlignedBoundingBox());
				}

				if (sceneContext.BuildVolumeMesh != null && sceneContext.RendererOptions.RenderBuildVolume)
				{
					box = AxisAlignedBoundingBox.Union(box, sceneContext.BuildVolumeMesh.GetAxisAlignedBoundingBox());
				}

				return box;
			}
			else
			{
				return new AxisAlignedBoundingBox(-GridSize, -GridSize, 0, GridSize, GridSize, 0);
			}
		}		

		private void Settings_SettingChanged(object sender, StringEventArgs e)
		{
			string settingsKey = e.Data;

			// Invalidate bed textures on related settings change
			if (settingsKey == SettingsKey.t0_inset
				|| settingsKey == SettingsKey.t1_inset
				|| settingsKey == SettingsKey.bed_size
				|| settingsKey == SettingsKey.print_center
				|| settingsKey == SettingsKey.extruder_count
				|| settingsKey == SettingsKey.bed_shape
				|| settingsKey == SettingsKey.build_height)
			{
				activeBedToolClippingImage = int.MinValue;

				// Force texture rebuild, don't clear allowing redraws of the stale data until rebuilt
				bedTextures = null;
			}
		}

		private void SetActiveTexture(ImageBuffer bedTexture)
		{
			foreach (var texture in printer.Bed.Mesh.FaceTextures)
			{
				texture.Value.image = bedTexture;
			}

			printer.Bed.Mesh.PropertyBag.Clear();

			ApplicationController.Instance.MainView.Invalidate();
		}		

		public void Dispose()
		{
			// Unregister listeners
			sceneContext.Printer.Settings.SettingChanged -= this.Settings_SettingChanged;
		}
	}
}
