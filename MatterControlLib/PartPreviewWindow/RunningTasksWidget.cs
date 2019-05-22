/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RunningTasksWidget : GuiWidget
	{
		private ThemeConfig theme;
		private Color borderColor;
		private FlowLayoutWidget pendingTasksList;
		private object owner;

		public RunningTasksWidget(ThemeConfig theme, object owner)
		{
			this.owner = owner;
			this.theme = theme;

			if (theme.IsDarkTheme)
			{
				borderColor = theme.AccentMimimalOverlay.Blend(Color.White, 0.2);
			}
			else
			{
				borderColor = theme.AccentMimimalOverlay.Blend(Color.Black, 0.16);
			}

			pendingTasksList = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = theme.InteractionLayerOverlayColor,
				HAnchor = HAnchor.Fit | HAnchor.Left,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(325, 0),
				Border = new BorderDouble(top: 1),
				BorderColor = borderColor,
			};
			this.AddChild(pendingTasksList);

			// Register listeners
			ApplicationController.Instance.Tasks.TasksChanged += this.Tasks_TasksChanged;

			this.RenderRunningTasks(theme, ApplicationController.Instance.Tasks);
		}

		private void Tasks_TasksChanged(object sender, EventArgs e)
		{
			this.RenderRunningTasks(theme, ApplicationController.Instance.Tasks);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			ApplicationController.Instance.Tasks.TasksChanged -= this.Tasks_TasksChanged;

			base.OnClosed(e);
		}

		private void RenderRunningTasks(ThemeConfig theme, RunningTasksConfig tasks)
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

			var progressBackgroundColor = new Color(borderColor, 35);

			// Add new items
			foreach (var taskItem in tasks.RunningTasks.Where(t => !displayedTasks.Contains(t)))
			{
				// show tasks that are unfiltered (owner == null) or are owned by us
				if (taskItem.Owner == null 
					|| taskItem.Owner == owner)
				{
					var taskRow = new RunningTaskRow("", taskItem, theme)
					{
						HAnchor = HAnchor.Stretch,
						BackgroundColor = theme.AccentMimimalOverlay,
						Border = new BorderDouble(1, 1, 1, 0),
						BorderColor = borderColor,
						ProgressBackgroundColor = progressBackgroundColor
					};

					pendingTasksList.AddChild(taskRow);
				}
				else
				{
					int a = 0;
				}
			}

			this.Visible = pendingTasksList.Children.Count > 0;

			pendingTasksList.Invalidate();
		}
	}
}
