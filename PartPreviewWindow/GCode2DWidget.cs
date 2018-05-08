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
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.GCodeVisualizer;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCode2DWidget : GuiWidget
	{
		public enum ETransformState { Move, Scale };

		public ETransformState TransformState { get; set; }

		private Vector2 lastMousePosition = new Vector2(0, 0);
		private Vector2 mouseDownPosition = new Vector2(0, 0);

		private double layerScale { get; set; } = 1;
		private Vector2 gridSizeMm;
		private Vector2 gridCenterMm;

		private Vector2 unscaledRenderOffset = new Vector2(0, 0);
		private GCodeFile loadedGCode => printer.Bed.LoadedGCode;
		private View3DConfig options;
		private PrinterConfig printer;

		private static Color gridColor = new Color(190, 190, 190, 255);
		private EventHandler unregisterEvents;
		private ImageBuffer bedImage;

		public GCode2DWidget(PrinterConfig printer)
		{
			this.printer = printer;
			options = printer.Bed.RendererOptions;

			var theme = ApplicationController.Instance.Theme;

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			this.LocalBounds = new RectangleDouble(0, 0, 100, 100);
			this.AnchorAll();

			printer.Bed.LoadedGCodeChanged += LoadedGCodeChanged;

			// make sure we have good settings

			ActiveSliceSettings.SettingChanged.RegisterEvent(Printer_SettingChanged, ref unregisterEvents);
			Printer_SettingChanged(this, null);

			this.gridSizeMm = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size);
			this.gridCenterMm = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

			bedImage = BedMeshGenerator.CreatePrintBedImage(printer).Multiply(theme.TabBodyBackground);
		}

		private void Printer_SettingChanged(object sender, EventArgs e)
		{
			if (e is StringEventArgs stringEvent)
			{
				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					this.gridSizeMm = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size);
					this.gridCenterMm = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

					bedImage = BedMeshGenerator.CreatePrintBedImage(printer);
				}
			}
		}

		private Affine scalingTransform => Affine.NewScaling(layerScale, layerScale);

		private Affine totalTransform => Affine.NewTranslation(unscaledRenderOffset) * scalingTransform * Affine.NewTranslation(Width / 2, Height / 2);

		private void LoadedGCodeChanged(object sender, EventArgs e)
		{
			if (loadedGCode == null)
			{
				// TODO: Display an overlay for invalid GCode
			}
			else
			{
				CenterPartInView();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (loadedGCode != null)
			{
				if(layerScale == 0)
				{
					CenterPartInView();
				}
				//using (new PerformanceTimer("GCode Timer", "Total"))
				{
					Affine transform = totalTransform;

					if (this.options.RenderBed)
					{
						//using (new PerformanceTimer("GCode Timer", "Render Grid"))
						{
							double gridLineWidths = 0.2 * layerScale;

							DrawBedImage(graphics2D, transform);
						}
					}

					if (printer.Bed.RenderInfo is GCodeRenderInfo options)
					{
						var renderInfo = new GCodeRenderInfo(
							printer.Bed.ActiveLayerIndex,
							printer.Bed.ActiveLayerIndex,
							transform,
							layerScale,
							options.FeatureToStartOnRatio0To1,
							options.FeatureToEndOnRatio0To1,
							options.extruderOffsets,
							options.GetRenderType,
							options.GetMaterialColor);

						printer.Bed.GCodeRenderer?.Render(graphics2D, renderInfo);
					}
				}
			}

			base.OnDraw(graphics2D);
		}

		public void DrawBedImage(Graphics2D graphics2D, Affine transform)
		{
			Vector2 gridOffset = gridCenterMm - gridSizeMm / 2;

			Vector2 imageStart = Vector2.Zero + gridOffset;
			transform.transform(ref imageStart);
			Vector2 imageEnd = gridSizeMm + gridOffset;
			transform.transform(ref imageEnd);
			graphics2D.Render(bedImage, imageStart, imageEnd.X - imageStart.X, imageEnd.Y - imageStart.Y);
		}

		double startDistanceBetweenPoints = 1;
		double pinchStartScale = 1;
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (MouseCaptured)
			{
				if (mouseEvent.NumPositions == 1)
				{
					mouseDownPosition.X = mouseEvent.X;
					mouseDownPosition.Y = mouseEvent.Y;
				}
				else
				{
					Vector2 centerPosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
					mouseDownPosition = centerPosition;
				}

				lastMousePosition = mouseDownPosition;

				if (mouseEvent.NumPositions > 1)
				{
					startDistanceBetweenPoints = (mouseEvent.GetPosition(1) - mouseEvent.GetPosition(0)).Length;
					pinchStartScale = layerScale;
				}
			}
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			base.OnMouseWheel(mouseEvent);
			if (FirstWidgetUnderMouse) // TODO: find a good way to decide if you are what the wheel is trying to do
			{
				const double deltaFor1Click = 120;
				double scaleAmount = (mouseEvent.WheelDelta / deltaFor1Click) * .1;

				ScalePartAndFixPosition(mouseEvent, layerScale + layerScale * scaleAmount);

				Invalidate();
			}
		}

		void ScalePartAndFixPosition(MouseEventArgs mouseEvent, double scaleAmount)
		{
			Vector2 mousePreScale = new Vector2(mouseEvent.X, mouseEvent.Y);
			totalTransform.inverse_transform(ref mousePreScale);

			layerScale = scaleAmount;

			Vector2 mousePostScale = new Vector2(mouseEvent.X, mouseEvent.Y);
			totalTransform.inverse_transform(ref mousePostScale);

			unscaledRenderOffset += (mousePostScale - mousePreScale);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			Vector2 mousePos = new Vector2();
			if (mouseEvent.NumPositions == 1)
			{
				mousePos = new Vector2(mouseEvent.X, mouseEvent.Y);
			}
			else
			{
				Vector2 centerPosition = (mouseEvent.GetPosition(1) + mouseEvent.GetPosition(0)) / 2;
				mousePos = centerPosition;
			}
			if (MouseCaptured)
			{
				Vector2 mouseDelta = mousePos - lastMousePosition;
				switch (TransformState)
				{
					case ETransformState.Move:
						scalingTransform.inverse_transform(ref mouseDelta);

						unscaledRenderOffset += mouseDelta;
						break;

					case ETransformState.Scale:
						double zoomDelta = 1;
						if (mouseDelta.Y < 0)
						{
							zoomDelta = 1 - (-1 * mouseDelta.Y / 100);
						}
						else if (mouseDelta.Y > 0)
						{
							zoomDelta = 1 + (1 * mouseDelta.Y / 100);
						}

						Vector2 mousePreScale = mouseDownPosition;
						totalTransform.inverse_transform(ref mousePreScale);

						layerScale *= zoomDelta;

						Vector2 mousePostScale = mouseDownPosition;
						totalTransform.inverse_transform(ref mousePostScale);

						unscaledRenderOffset += (mousePostScale - mousePreScale);
						break;

					default:
						throw new NotImplementedException();
				}

				Invalidate();
			}
			lastMousePosition = mousePos;

			// check if we should do some scaling
			if (TransformState == ETransformState.Move
				&& mouseEvent.NumPositions > 1
				&& startDistanceBetweenPoints > 0)
			{
				double curDistanceBetweenPoints = (mouseEvent.GetPosition(1) - mouseEvent.GetPosition(0)).Length;

				double scaleAmount = pinchStartScale * curDistanceBetweenPoints / startDistanceBetweenPoints;
				ScalePartAndFixPosition(mouseEvent, scaleAmount);
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);

			printer.Bed.GCodeRenderer?.Dispose();
			printer.Bed.LoadedGCodeChanged -= LoadedGCodeChanged;

			base.OnClosed(e);
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				return base.LocalBounds;
			}
			set
			{
				double oldWidth = Width;
				double oldHeight = Height;
				base.LocalBounds = value;
				if (oldWidth > 0)
				{
					layerScale = layerScale * (Width / oldWidth);
				}
				else if (printer.Bed.GCodeRenderer != null)
				{
					CenterPartInView();
				}
			}
		}

		public void CenterPartInView()
		{
			if (loadedGCode != null)
			{
				RectangleDouble partBounds = loadedGCode.GetBounds();
				Vector2 weightedCenter = loadedGCode.GetWeightedCenter();

				unscaledRenderOffset = -weightedCenter;
				layerScale = Math.Min(Height / partBounds.Height, Width / partBounds.Width);

				Invalidate();
			}
		}
	}
}