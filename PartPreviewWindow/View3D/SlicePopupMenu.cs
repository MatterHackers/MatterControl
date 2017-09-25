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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SliceStageProgressControl : FlowLayoutWidget
	{
		internal TextWidget operationText;
		internal ProgressControl progressControl;

		internal SliceStageProgressControl(string startingTextMessage)
			: base(FlowDirection.TopToBottom)
		{
			operationText = new TextWidget(startingTextMessage)
			{
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true
			};
			AddChild(operationText);

			progressControl = new ProgressControl("", RGBA_Bytes.Black, RGBA_Bytes.Black)
			{
				HAnchor = HAnchor.Left
			};
			AddChild(progressControl);
		}
	}

	public class SlicePopupMenu : PopupButton
	{
		private TextImageButtonFactory buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;
		private DisableablePanel disableablePanel;
		private PrinterConfig printer;
		private PrinterTabPage printerTabPage;

		public SlicePopupMenu(PrinterConfig printer, PrinterTabPage printerTabPage)
		{
			this.printerTabPage = printerTabPage;
			this.printer = printer;
			Name = "SlicePopupMenu";
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;

			var sliceButton =  buttonFactory.Generate("Slice".Localize().ToUpper());
			sliceButton.Name = "Slice Dropdown Button";
			this.AddChild(sliceButton);
		}

		public override void OnLoad(EventArgs args)
		{
			// Wrap popup content in a DisableablePanel
			disableablePanel = new DisableablePanel(this.GetPopupContent(), printer.Connection.PrinterIsConnected, alpha: 140)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};
			disableablePanel.Enabled = true;

			// Set as popup
			this.PopupContent = disableablePanel;

			base.OnLoad(args);
		}

		protected GuiWidget GetPopupContent()
		{
			var widget = new IgnoredPopupWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = RGBA_Bytes.White,
				Padding = new BorderDouble(12, 5, 12, 0)
			};

			var progressContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				MinimumSize = new VectorMath.Vector2(400, 500)
			};

			widget.AddChild(progressContainer);

			var sliceButton = buttonFactory.Generate("Slice".Localize().ToUpper());
			widget.AddChild(sliceButton);

			sliceButton.ToolTipText = "Slice Parts".Localize();
			sliceButton.Name = "Generate Gcode Button";
			//sliceButton.Margin = defaultMargin;
			sliceButton.Click += async (s, e) =>
			{
				if (printer.Settings.PrinterSelected)
				{
					var printItem = printer.Bed.printItem;

					if (printer.Settings.IsValid() && printItem != null)
					{
						sliceButton.Enabled = false;

						try
						{
							var sliceProgressReporter = new SliceProgressReporter(progressContainer);

							sliceProgressReporter.StartReporting();

							// Save any pending changes before starting the print
							await ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded();

							await SlicingQueue.SliceFileAsync(printItem, sliceProgressReporter);
							sliceProgressReporter.EndReporting();

							var gcodeLoadCancellationTokenSource = new CancellationTokenSource();

							ApplicationController.Instance.ActivePrinter.Bed.LoadGCode(printItem.GetGCodePathAndFileName(), gcodeLoadCancellationTokenSource.Token, printerTabPage.modelViewer.gcodeViewer.LoadProgress_Changed);

							printerTabPage.ViewMode = PartViewMode.Layers3D;

							// HACK: directly fire method which previously ran on SlicingDone event on PrintItemWrapper
							UiThread.RunOnIdle(() => printerTabPage.modelViewer.gcodeViewer.CreateAndAddChildren(printer));
						}
						catch (Exception ex)
						{
							Console.WriteLine("Error slicing file: " + ex.Message);
						}

						sliceButton.Enabled = true;
					};
				}
				else
				{
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowMessageBox(null, "Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
					});
				}
			};

			return widget;
		}
	}

	public class SliceProgressReporter : IProgress<string>
	{
		public SliceStageProgressControl partProcessingInfo;
		private double currentValue = 0;
		private double destValue = 10;
		private GuiWidget progressReportContainer;
		string lastOutputLine = "";

		public SliceProgressReporter(GuiWidget progressReportContainer)
		{
			progressReportContainer.CloseAllChildren();
			this.progressReportContainer = progressReportContainer;
		}

		public void EndReporting()
		{
			if (partProcessingInfo != null)
			{
				partProcessingInfo.progressControl.PercentComplete = 100;
			}

			progressReportContainer.AddChild(new TextWidget("Done!"));
		}

		public void Report(string value)
		{
			UiThread.RunOnIdle(() =>
			{
				bool foundProgressNumbers = false;

				if (GCodeFile.GetFirstNumberAfter("", value, ref currentValue))
				{
					if (GCodeFile.GetFirstNumberAfter("/", value, ref destValue))
					{
						if (destValue == 0)
						{
							destValue = 1;
						}
						foundProgressNumbers = true;
					}
				}

				int lengthBeforeNumber = value.IndexOfAny("0123456789".ToCharArray()) - 1;
				lengthBeforeNumber = lengthBeforeNumber < 0 ? lengthBeforeNumber = value.Length : lengthBeforeNumber;
				if (lastOutputLine != value.Substring(0, lengthBeforeNumber))
				{
					lastOutputLine = value.Substring(0, lengthBeforeNumber);

					if (foundProgressNumbers)
					{
						partProcessingInfo = new SliceStageProgressControl("start");
						progressReportContainer.AddChild(partProcessingInfo);
					}
					else
					{
						progressReportContainer.AddChild(new TextWidget(value));
					}
				}

				if (partProcessingInfo != null
					&& foundProgressNumbers)
				{
					int percentComplete = Math.Min(100, Math.Max(0, (int)(100 * currentValue / destValue + .5)));
					partProcessingInfo.progressControl.PercentComplete = percentComplete;
					partProcessingInfo.operationText.Text = value;
				}
			});
		}

		public void StartReporting()
		{
			progressReportContainer.AddChild(new TextWidget("Loading File..."));
		}
	}
}