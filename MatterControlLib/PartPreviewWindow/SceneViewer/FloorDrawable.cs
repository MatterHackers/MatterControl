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

		private static int GetActiveToolIndex(IObject3D selectedItem)
		{
			if (selectedItem == null)
			{
				return -1;
			}

			// HACK: hard-coded index for unioned T0/T1 limits
			if (selectedItem?.OutputType == PrintOutputTypes.WipeTower)
			{
				return 2;
			}

			int worldMaterialIndex;

			var materials = new HashSet<int>(selectedItem.DescendantsAndSelf().Select(i => i.WorldMaterialIndex()));
			if (materials.Count == 1)
			{
				worldMaterialIndex = materials.First();
			}
			else
			{
				// TODO: More work needed here to choose a correct index. For now, considering count > 1 to be tools 1 & 2
				worldMaterialIndex = 2;
			}

			// Convert default material (-1) to T0
			if (worldMaterialIndex == -1)
			{
				worldMaterialIndex = 0;
			}

			return worldMaterialIndex;
		}

		private void GenerateToolLimitsTexture(PrinterConfig printer, int toolIndex, ImageBuffer bedplateImage)
		{
			var xScale = bedplateImage.Width / printer.Settings.BedBounds.Width;
			var yScale = bedplateImage.Height / printer.Settings.BedBounds.Height;

			int alpha = 80;

			var graphics = bedplateImage.NewGraphics2D();

			RectangleDouble toolBounds;

			if (toolIndex == 2)
			{
				var tool0Bounds = printer.Settings.ToolBounds[0];
				var tool1Bounds = printer.Settings.ToolBounds[1];

				tool0Bounds.IntersectWithRectangle(tool1Bounds);

				toolBounds = tool0Bounds;
			}
			else
			{
				toolBounds = printer.Settings.ToolBounds[toolIndex];
			}

			// move relative to the texture origin, move to the bed lower left position
			var bedBounds = printer.Settings.BedBounds;

			toolBounds.Offset(-bedBounds.Left, -bedBounds.Bottom);

			// Scale toolBounds into textures units
			toolBounds = new RectangleDouble(
				toolBounds.Left * xScale,
				toolBounds.Bottom * yScale,
				toolBounds.Right * xScale,
				toolBounds.Top * yScale);

			var imageBounds = bedplateImage.GetBounds();

			var dimRegion = new VertexStorage();
			dimRegion.MoveTo(imageBounds.Left, imageBounds.Bottom);
			dimRegion.LineTo(imageBounds.Right, imageBounds.Bottom);
			dimRegion.LineTo(imageBounds.Right, imageBounds.Top);
			dimRegion.LineTo(imageBounds.Left, imageBounds.Top);

			var targetRect = new VertexStorage();
			targetRect.MoveTo(toolBounds.Right, toolBounds.Bottom);
			targetRect.LineTo(toolBounds.Left, toolBounds.Bottom);
			targetRect.LineTo(toolBounds.Left, toolBounds.Top);
			targetRect.LineTo(toolBounds.Right, toolBounds.Top);
			targetRect.ClosePolygon();

			var overlayMinusTargetRect = new CombinePaths(dimRegion, targetRect);
			graphics.Render(overlayMinusTargetRect, new Color(Color.Black, alpha));

			string toolTitle = string.Format("{0} {1}", "Tool".Localize(), toolIndex + 1);

			if (toolIndex == 2)
			{
				toolTitle = "Tools".Localize() + " 1 & 2";
			}

			var stringPrinter = new TypeFacePrinter(toolTitle, theme.DefaultFontSize, bold: true);
			var printerBounds = stringPrinter.GetBounds();

			int textPadding = 8;

			var textBounds = printerBounds;
			textBounds.Inflate(textPadding);

			var cornerRect = new RectangleDouble(toolBounds.Right - textBounds.Width, toolBounds.Top - textBounds.Height, toolBounds.Right, toolBounds.Top);

			graphics.Render(
				new RoundedRectShape(cornerRect, bottomLeftRadius: 6),
				theme.PrimaryAccentColor);

			graphics.DrawString(
				toolTitle,
				toolBounds.Right - textPadding,
				cornerRect.Bottom + (cornerRect.Height / 2 - printerBounds.Height / 2) + 1,
				theme.DefaultFontSize,
				justification: Justification.Right,
				baseline: Baseline.Text,
				color: Color.White,
				bold: true);

			graphics.Render(new Stroke(targetRect, 1), theme.PrimaryAccentColor);
		}

		public void Dispose()
		{
			// Unregister listeners
			sceneContext.Printer.Settings.SettingChanged -= this.Settings_SettingChanged;
		}
	}
}
