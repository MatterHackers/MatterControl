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
//#define WITH_WRAPPER

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RunningTasksWidget : GuiWidget
	{
		private FlowLayoutWidget pendingTasksList;
		private ThemeConfig theme;

		public RunningTasksWidget(ThemeConfig theme)
		{
			this.theme = theme;
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;

#if WITH_WRAPPER
			pendingTasksList = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			};

			var pendingTasksContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(top: 100, left: 5),
				Padding = new BorderDouble(4),
				BackgroundColor = new Color(0, 0, 0, theme.OverlayAlpha),
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Top | VAnchor.Fit,
				MinimumSize = new Vector2(250, 0)
			};
			view3DContainer.AddChild(pendingTasksContainer);

			pendingTasksPanel = new SectionWidget("Running".Localize() + "...", ActiveTheme.Instance.PrimaryTextColor, pendingTasksList)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(6, 0)
			};
			pendingTasksContainer.AddChild(pendingTasksPanel);

			pendingTasksList.Padding = new BorderDouble(0, 6);
#else
			pendingTasksList = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = theme.InteractionLayerOverlayColor,
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(325, 0)
			};
			this.AddChild(pendingTasksList);
#endif

			var tasks = ApplicationController.Instance.Tasks;

			tasks.TasksChanged += (s, e) =>
			{
				var rows = pendingTasksList.Children.OfType<RunningTaskRow>().ToList();
				var displayedTasks = new HashSet<RunningTaskDetails>(rows.Select(taskRow => taskRow.taskDetails));
				var runningTasks = tasks.RunningTasks;

				// Remove expired items
				foreach (var row in rows)
				{
					if (!runningTasks.Contains(row.taskDetails))
					{
						row.Close();
					}
				}

				// Add new items
				foreach (var taskItem in tasks.RunningTasks.Where(t => !displayedTasks.Contains(t)))
				{
					var taskRow = new RunningTaskRow("", taskItem, theme)
					{
						HAnchor = HAnchor.Stretch
					};

					pendingTasksList.AddChild(taskRow);
				}

				pendingTasksList.Invalidate();
			};

		}

		private Task LoadRunningTaskExample(IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
		{
			var reportDetails = new ProgressStatus();

			var timer = Stopwatch.StartNew();
			while (timer.Elapsed.TotalMinutes < 0.5)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				reportDetails.Progress0To1 = timer.Elapsed.TotalSeconds / 30d;

				reportDetails.Status = reportDetails.Progress0To1 < .5 ? "first half" : "second half";

				progress.Report(reportDetails);

				Thread.Sleep(100);
			}

			return Task.CompletedTask;
		}
	}
}
