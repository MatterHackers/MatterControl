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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
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
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			operationText = new TextWidget(startingTextMessage)
			{
				HAnchor = HAnchor.Left,
				AutoExpandBoundsToText = true
			};
			this.AddChild(operationText);

			progressControl = new ProgressControl("", RGBA_Bytes.Black, RGBA_Bytes.Black)
			{
				HAnchor = HAnchor.Left
			};
			this.AddChild(progressControl);
		}
	}

	public class SlicePopupMenu : PopupButton
	{
		private TextImageButtonFactory buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;
		private PrinterConfig printer;
		private PrinterTabPage printerTabPage;
		private bool activelySlicing = false;

		public SlicePopupMenu(PrinterConfig printer, ThemeConfig theme, PrinterTabPage printerTabPage)
		{
			this.printerTabPage = printerTabPage;
			this.printer = printer;
			this.Name = "SlicePopupMenu";
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.PopupContent = new IgnoredPopupWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
				Padding = 15,
				MinimumSize = new VectorMath.Vector2(300, 65),
			};

			this.AddChild(new TextButton("Slice".Localize().ToUpper(), theme)
			{
				//Name = "Slice Dropdown Button",
				Name = "Generate Gcode Button",
				BackgroundColor = theme.ButtonFactory.normalFillColor,
				HoverColor = theme.ButtonFactory.hoverFillColor,
				Margin = theme.ButtonSpacing,
			});
		}

		protected override void BeforeShowPopup()
		{
			this.PopupContent.CloseAllChildren();
			this.SliceBedplate().ConfigureAwait(false);
			base.BeforeShowPopup();
		}

		private async Task SliceBedplate()
		{
			if (activelySlicing)
			{
				return;
			}

			if (printer.Settings.PrinterSelected)
			{
				var printItem = printer.Bed.printItem;

				if (printer.Settings.IsValid() && printItem != null)
				{
					activelySlicing = true;

					try
					{
						var sliceProgressReporter = new SliceProgressReporter(this.PopupContent, printer);

						sliceProgressReporter.StartReporting();

						// Save any pending changes before starting the print
						await printerTabPage.modelViewer.PersistPlateIfNeeded();

						await SlicingQueue.SliceFileAsync(printItem, sliceProgressReporter);
						sliceProgressReporter.EndReporting();

						var gcodeLoadCancellationTokenSource = new CancellationTokenSource();

						this.printer.Bed.LoadGCode(printItem.GetGCodePathAndFileName(), gcodeLoadCancellationTokenSource.Token, printerTabPage.modelViewer.gcodeViewer.LoadProgress_Changed);

						printerTabPage.ViewMode = PartViewMode.Layers3D;

						// HACK: directly fire method which previously ran on SlicingDone event on PrintItemWrapper
						UiThread.RunOnIdle(() => printerTabPage.modelViewer.gcodeViewer.CreateAndAddChildren(printer));
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error slicing file: " + ex.Message);
					}

					activelySlicing = false;
				};
			}
			else
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(null, "Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
				});
			}
		}
	}

	public class SliceProgressReporter : IProgress<string>
	{
		public SliceStageProgressControl partProcessingInfo;
		private double currentValue = 0;
		private double destValue = 10;
		private GuiWidget progressReportContainer;
		private string lastOutputLine = "";

		private PrinterConfig printer;

		public SliceProgressReporter(GuiWidget progressReportContainer, PrinterConfig printer)
		{
			this.progressReportContainer = progressReportContainer;
			this.printer = printer;

			partProcessingInfo = new SliceStageProgressControl("start");
			partProcessingInfo.VAnchor = VAnchor.Center;
			partProcessingInfo.operationText.TextColor = ApplicationController.Instance.Theme.ButtonFactory.normalTextColor;

			progressReportContainer.AddChild(partProcessingInfo);
		}

		private Stopwatch timer = Stopwatch.StartNew();

		private string progressSection = "";

		public void Report(string value)
		{
			bool foundProgressNumbers = false;


			if (GCodeFile.GetFirstNumberAfter("", value, ref currentValue)
				&& GCodeFile.GetFirstNumberAfter("/", value, ref destValue))
			{
				if (destValue == 0)
				{
					destValue = 1;
				}

				foundProgressNumbers = true;

				if (!partProcessingInfo.progressControl.Visible)
				{
					int pos = value.IndexOf(currentValue.ToString());
					if (pos != -1)
					{
						progressSection = value.Substring(0, pos);
					}
					else
					{
						progressSection = value;
					}

					timer.Restart();
					partProcessingInfo.progressControl.PercentComplete = 0;
					partProcessingInfo.progressControl.Visible = true;
				}
			}
			else
			{
				if (partProcessingInfo.progressControl.Visible)
				{
					printer.Connection.TerminalLog.WriteLine(string.Format("{0}: {1:#.##}s", progressSection.Trim(), timer.Elapsed.TotalSeconds));
					partProcessingInfo.progressControl.Visible = false;
				}
				else
				{
					printer.Connection.TerminalLog.WriteLine(value);
				}
			}

			int lengthBeforeNumber = value.IndexOfAny("0123456789".ToCharArray()) - 1;
			lengthBeforeNumber = lengthBeforeNumber < 0 ? lengthBeforeNumber = value.Length : lengthBeforeNumber;
			if (lastOutputLine != value.Substring(0, lengthBeforeNumber))
			{
				lastOutputLine = value.Substring(0, lengthBeforeNumber);
				partProcessingInfo.progressControl.Visible = foundProgressNumbers;
			}

			if (foundProgressNumbers)
			{
				int percentComplete = Math.Min(100, Math.Max(0, (int)(100 * currentValue / destValue + .5)));
				partProcessingInfo.progressControl.PercentComplete = percentComplete;
			}

			partProcessingInfo.operationText.Text = value;
		}

		public void StartReporting()
		{
			partProcessingInfo.operationText.Text = "Loading File...";
		}

		public void EndReporting()
		{
			partProcessingInfo.progressControl.PercentComplete = 100;
			partProcessingInfo.operationText.Text = "Done!";

			UiThread.RunOnIdle(() =>
			{
				progressReportContainer.Parents<PopupWidget>().FirstOrDefault()?.Close();
			}, 1);
		}
	}
}