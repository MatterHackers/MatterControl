/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewGcodeBasic : GuiWidget
	{
		private TextWidget gcodeProcessingStateInfoText;
		private PrintItemWrapper printItem => ApplicationController.Instance.ActivePrintItem;
		
		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;

		public delegate Vector2 GetSizeFunction();

		private string gcodeLoading = "Loading G-Code".Localize();
		private string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private string fileNotFoundMessage = "File not found on disk.".Localize();
		private string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private Vector2 bedCenter;
		private Vector3 viewerVolume;

		private PartViewMode activeViewMode = PartViewMode.Layers3D;

		private PrinterConfig printer;
		private ViewControls3D viewControls3D;

		public ViewGcodeBasic(Vector3 viewerVolume, Vector2 bedCenter, BedShape bedShape, ViewControls3D viewControls3D)
		{
			printer = ApplicationController.Instance.Printer;

			this.viewControls3D = viewControls3D;
			this.viewerVolume = viewerVolume;
			this.bedCenter = bedCenter;

			RenderOpenGl.GLHelper.WireframeColor = ActiveTheme.Instance.PrimaryAccentColor;

			CreateAndAddChildren();

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == "extruder_offset")
					{
						printer.BedPlate.GCodeRenderer.Clear3DGCode();
					}
				}
			}, ref unregisterEvents);

			// TODO: Why do we clear GCode on AdvancedControlsPanelReloading - assume some slice settings should invalidate. If so, code should be more specific and bound to slice settings changed
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => printer.BedPlate.GCodeRenderer?.Clear3DGCode(), ref unregisterEvents);
		}

		internal GCodeFile loadedGCode => printer.BedPlate.LoadedGCode;

		internal void CreateAndAddChildren()
		{
			CloseAllChildren();

			gcodeProcessingStateInfoText = null;

			var mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				VAnchor = VAnchor.MaxFitOrStretch
			};

			gcodeDisplayWidget = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			if (printItem != null)
			{
				SetProcessingMessage("Loading G-Code...".Localize());

				bool isGCode = Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE";

				string gcodeFilePath = isGCode ? printItem.FileLocation : printItem.GetGCodePathAndFileName();
				if (!File.Exists(gcodeFilePath))
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, printItem.Name));
				}
			}

			mainContainerTopToBottom.AddChild(gcodeDisplayWidget);

			this.AddChild(mainContainerTopToBottom);

			// *************** AddGCodeFileControls ***************
			SetProcessingMessage("");
			if (loadedGCode == null)
			{
				// If we have finished loading the gcode and the source file exists but we don't have any loaded gcode it is because the loader decided to not load it.
				if (File.Exists(printItem.FileLocation))
				{
					SetProcessingMessage(string.Format(fileTooBigToLoad, printItem.Name));
				}
				else
				{
					SetProcessingMessage(string.Format("{0}\n'{1}'", fileNotFoundMessage, Path.GetFileName(printItem.FileLocation)));
				}
			}

			if (loadedGCode?.LineCount > 0)
			{
				gradientWidget = new ColorGradientWidget(loadedGCode)
				{
					Margin = new BorderDouble(top: 55, left: 11),
					HAnchor = HAnchor.Fit | HAnchor.Left,
					VAnchor = VAnchor.Top,
					Visible = printer.BedPlate.RendererOptions.RenderSpeeds
				};
				AddChild(gradientWidget);

				GCodeRenderer.ExtrusionColor = ActiveTheme.Instance.PrimaryAccentColor;

				var gcodeDetails = new GCodeDetails(this.loadedGCode);

				this.AddChild(new GCodeDetailsView(gcodeDetails)
				{
					Margin = new BorderDouble(0, 0, 35, 5),
					Padding = new BorderDouble(10),
					BackgroundColor = new RGBA_Bytes(0, 0, 0, ViewControlsBase.overlayAlpha),
					HAnchor = HAnchor.Right | HAnchor.Absolute,
					VAnchor = VAnchor.Top | VAnchor.Fit,
					Width = 150
				});
			}
		}

		internal void LoadProgress_Changed(double progress0To1, string processingState)
		{
			SetProcessingMessage(string.Format("{0} {1:0}%...", gcodeLoading, progress0To1 * 100));
		}

		private void SetProcessingMessage(string message)
		{
			if (gcodeProcessingStateInfoText == null)
			{
				gcodeProcessingStateInfoText = new TextWidget(message)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center,
					AutoExpandBoundsToText = true
				};

				var labelContainer = new GuiWidget();
				labelContainer.Selectable = false;
				labelContainer.AnchorAll();
				labelContainer.AddChild(gcodeProcessingStateInfoText);

				gcodeDisplayWidget.AddChild(labelContainer);
			}

			if (message == "")
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.Transparent;
			}
			else
			{
				gcodeProcessingStateInfoText.BackgroundColor = RGBA_Bytes.White;
			}

			gcodeProcessingStateInfoText.Text = message;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
