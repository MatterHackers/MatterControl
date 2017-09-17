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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewGcodeBasic : GuiWidget
	{
		private TextWidget gcodeProcessingStateInfoText;
		
		private GuiWidget gcodeDisplayWidget;

		private ColorGradientWidget gradientWidget;

		private EventHandler unregisterEvents;

		private string gcodeLoading = "Loading G-Code".Localize();
		private string slicingErrorMessage = "Slicing Error.\nPlease review your slice settings.".Localize();
		private string fileNotFoundMessage = "File not found on disk.".Localize();
		private string fileTooBigToLoad = "GCode file too big to preview ({0}).".Localize();

		private BedConfig sceneContext;
		private PrinterConfig printer;

		private ViewControls3D viewControls3D;

		public ViewGcodeBasic(PrinterConfig printer, BedConfig sceneContext, ViewControls3D viewControls3D)
		{
			this.printer = printer;
			this.sceneContext = sceneContext;
			this.viewControls3D = viewControls3D;

			CreateAndAddChildren(printer);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == "extruder_offset")
					{
						printer.Bed.GCodeRenderer?.Clear3DGCode();
					}
				}
			}, ref unregisterEvents);

			// TODO: Why do we clear GCode on AdvancedControlsPanelReloading - assume some slice settings should invalidate. If so, code should be more specific and bound to slice settings changed
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => printer?.Bed.GCodeRenderer?.Clear3DGCode(), ref unregisterEvents);
		}

		internal void CreateAndAddChildren(PrinterConfig printer)
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

			if (!File.Exists(sceneContext.GCodePath))
			{ 
				SetProcessingMessage($"{fileNotFoundMessage}\n'{sceneContext.GCodePath}'");
			}

			mainContainerTopToBottom.AddChild(gcodeDisplayWidget);

			this.AddChild(mainContainerTopToBottom);

			// *************** AddGCodeFileControls ***************
			SetProcessingMessage("");
			if (sceneContext.LoadedGCode == null)
			{
				SetProcessingMessage($"{fileNotFoundMessage}\n'{sceneContext.GCodePath}'");
			}

			if (sceneContext.LoadedGCode?.LineCount > 0)
			{
				gradientWidget = new ColorGradientWidget(sceneContext.LoadedGCode)
				{
					Margin = new BorderDouble(top: 55, left: 11),
					HAnchor = HAnchor.Fit | HAnchor.Left,
					VAnchor = VAnchor.Top,
					Visible = sceneContext.RendererOptions.RenderSpeeds
				};
				AddChild(gradientWidget);

				var gcodeDetails = new GCodeDetails(printer, printer.Bed.LoadedGCode);

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
