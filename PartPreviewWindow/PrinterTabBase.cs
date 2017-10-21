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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabBase : TabPage
	{
		internal View3DWidget view3DWidget;

		protected ViewControls3D viewControls3D;

		protected BedConfig sceneContext;
		protected ThemeConfig theme;

		protected GuiWidget view3DContainer;
		protected FlowLayoutWidget topToBottom;
		protected FlowLayoutWidget leftToRight;

		public PrinterTabBase(PrinterConfig printer, BedConfig sceneContext, ThemeConfig theme, string tabTitle)
			: base (tabTitle)
		{
			this.sceneContext = sceneContext;
			this.theme = theme;
			this.BackgroundColor = theme.TabBodyBackground;
			this.Padding = 0;

			viewControls3D = new ViewControls3D(theme, sceneContext.Scene.UndoBuffer)
			{
				BackgroundColor = new RGBA_Bytes(0, 0, 0, theme.OverlayAlpha),
				PartSelectVisible = false,
				VAnchor = VAnchor.Top | VAnchor.Fit | VAnchor.Absolute,
				HAnchor = HAnchor.Left | HAnchor.Absolute,
				Visible = true,
				Margin = new BorderDouble(0, 0, 0, 41)
			};
			viewControls3D.ResetView += (sender, e) =>
			{
				if (view3DWidget.Visible)
				{
					this.view3DWidget.ResetView();
				}
			};
			viewControls3D.OverflowMenu.DynamicPopupContent = () =>
			{
				return this.GetViewControls3DOverflowMenu();
			};

			bool isPrinterType = this.GetType() == typeof(PrinterTabPage);

			// The 3D model view
			view3DWidget = new View3DWidget(
				printer,
				sceneContext,
				View3DWidget.AutoRotate.Disabled,
				viewControls3D,
				theme,
				this,
				editorType: (isPrinterType) ? MeshViewerWidget.EditorType.Printer : MeshViewerWidget.EditorType.Part);

			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			this.AddChild(topToBottom);

			leftToRight = new FlowLayoutWidget();
			leftToRight.Name = "View3DContainerParent";
			leftToRight.AnchorAll();
			topToBottom.AddChild(leftToRight);

			view3DContainer = new GuiWidget();
			view3DContainer.AnchorAll();
			view3DContainer.AddChild(view3DWidget);

			view3DContainer.AddChild(PrintProgressWidget(printer));

			leftToRight.AddChild(view3DContainer);

			view3DContainer.BoundsChanged += (s, e) =>
			{
				viewControls3D.Width = view3DWidget.Width;
			};

			view3DWidget.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

			if (sceneContext.World.RotationMatrix == Matrix4X4.Identity)
			{
				this.view3DWidget.ResetView();
			}

			this.AddChild(viewControls3D);

			this.AnchorAll();
		}

		private GuiWidget PrintProgressWidget(PrinterConfig printer)
		{
			var bodyRow = new GuiWidget(300, 450)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 128),
				Selectable = false
			};

			// Progress section
			var expandingContainer = new HorizontalSpacer()
			{
				VAnchor = VAnchor.Fit | VAnchor.Center
			};
			bodyRow.AddChild(expandingContainer);

			var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(50, 0),
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Center | HAnchor.Fit,
			};
			expandingContainer.AddChild(progressContainer);

			var progressDial = new ProgressDial()
			{
				HAnchor = HAnchor.Center,
				Height = 200 * DeviceScale,
				Width = 200 * DeviceScale
			};
			progressContainer.AddChild(progressDial);

			var timeContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Center | HAnchor.Fit,
				Margin = 3
			};
			progressContainer.AddChild(timeContainer);

			var timeImage = AggContext.StaticData.LoadImage(Path.Combine("Images", "Screensaver", "time.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				timeImage.InvertLightness();
			}

			timeContainer.AddChild(new ImageWidget(timeImage));

			var timeWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				Margin = new BorderDouble(10, 0, 0, 0),
				VAnchor = VAnchor.Center,
			};

			timeContainer.AddChild(timeWidget);

			Action updatePrintProgress = null;
			updatePrintProgress = () =>
			{
				int secondsPrinted = printer.Connection.SecondsPrinted;
				int hoursPrinted = (int)(secondsPrinted / (60 * 60));
				int minutesPrinted = (secondsPrinted / 60 - hoursPrinted * 60);
				secondsPrinted = secondsPrinted % 60;

				// TODO: Consider if the consistency of a common time format would look and feel better than changing formats based on elapsed duration
				timeWidget.Text = (hoursPrinted <= 0) ? $"{minutesPrinted}:{secondsPrinted:00}" : $"{hoursPrinted}:{minutesPrinted:00}:{secondsPrinted:00}";

				progressDial.LayerCount = printer.Connection.CurrentlyPrintingLayer;
				progressDial.LayerCompletedRatio = printer.Connection.RatioIntoCurrentLayer;
				progressDial.CompletedRatio = printer.Connection.PercentComplete / 100;

				if (!HasBeenClosed)
				{
					switch (printer.Connection.CommunicationState)
					{
						case CommunicationStates.PreparingToPrint:
						case CommunicationStates.Printing:
						case CommunicationStates.Paused:
							bodyRow.Visible = true;
							break;

						default:
							bodyRow.Visible = false;
							break;
					}

					UiThread.RunOnIdle(updatePrintProgress, 1);
				}
			};

			UiThread.RunOnIdle(updatePrintProgress, 1);

			bodyRow.Visible = false;

			return bodyRow;
		}

		protected virtual GuiWidget GetViewControls3DOverflowMenu()
		{
			return view3DWidget.ShowOverflowMenu();
		}

		public override void OnLoad(EventArgs args)
		{
			ApplicationController.Instance.ActiveView3DWidget = view3DWidget;

			base.OnLoad(args);
		}
	}
}
