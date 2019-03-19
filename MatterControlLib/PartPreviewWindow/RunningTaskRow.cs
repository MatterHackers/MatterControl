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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RunningTaskRow : FlowLayoutWidget
	{
		internal RunningTaskDetails taskDetails;

		private ProgressBar progressBar;
		private ExpandCheckboxButton expandButton;
		private ThemeConfig theme;

		public RunningTaskRow(string title, RunningTaskDetails taskDetails, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.taskDetails = taskDetails;
			this.theme = theme;

			this.MinimumSize = new Vector2(100, 20);

			var detailsPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			var rowContainer = new GuiWidget()
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
			};
			this.AddChild(rowContainer);

			var topRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = 0,
			};
			rowContainer.AddChild(topRow);

			progressBar = new ProgressBar()
			{
				HAnchor = HAnchor.Stretch,
				Height = 2,
				VAnchor = VAnchor.Absolute | VAnchor.Bottom,
				FillColor = theme.PrimaryAccentColor,
				BorderColor = Color.Transparent,
				Margin = new BorderDouble(32, 7, theme.ButtonHeight * 2 + 14, 0),
				Visible = !taskDetails.IsExpanded
			};
			rowContainer.AddChild(progressBar);

			expandButton = new ExpandCheckboxButton(!string.IsNullOrWhiteSpace(title) ? title : taskDetails.Title, theme, 10)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				Checked = false,
				Padding = 0,
				AlwaysShowArrow = true
			};
			expandButton.CheckedStateChanged += (s, e) =>
			{
				taskDetails.IsExpanded = expandButton.Checked;
				SetExpansionMode(theme, detailsPanel, expandButton.Checked);
			};
			topRow.AddChild(expandButton);

			IconButton resumeButton = null;

			var pauseButton = new IconButton(AggContext.StaticData.LoadIcon("fa-pause_12.png", theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				Enabled = taskDetails.Options?.PauseAction != null,
				ToolTipText = taskDetails.Options?.PauseToolTip ?? "Pause".Localize()
			};
			if(taskDetails.Options?.IsPaused != null)
			{
				RunningInterval runningInterval = null;
				runningInterval = UiThread.SetInterval(() =>
				{
					if(taskDetails.Options.IsPaused())
					{
						pauseButton.Visible = false;
						resumeButton.Visible = true;
					}
					else
					{
						pauseButton.Visible = true;
						resumeButton.Visible = false;
					}
					if (this.HasBeenClosed)
					{
						UiThread.ClearInterval(runningInterval);
					}
				}, .2);

			}
			pauseButton.Click += (s, e) =>
			{
				taskDetails.Options?.PauseAction();
				pauseButton.Visible = false;
				resumeButton.Visible = true;
			};
			topRow.AddChild(pauseButton);

			resumeButton = new IconButton(AggContext.StaticData.LoadIcon("fa-play_12.png", theme.InvertIcons), theme)
			{
				Visible = false,
				Margin = theme.ButtonSpacing,
				ToolTipText = taskDetails.Options?.ResumeToolTip ?? "Resume".Localize(),
				Name = "Resume Task Button"
			};
			resumeButton.Click += (s, e) =>
			{
				taskDetails.Options?.ResumeAction();
				pauseButton.Visible = true;
				resumeButton.Visible = false;
			};
			topRow.AddChild(resumeButton);

			var stopButton = new IconButton(AggContext.StaticData.LoadIcon("fa-stop_12.png", theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				Name = "Stop Task Button",
				ToolTipText = taskDetails.Options?.StopToolTip ?? "Cancel".Localize()
			};
			stopButton.Click += (s, e) =>
			{
				var stopAction = taskDetails.Options?.StopAction;
				if (stopAction == null)
				{
					taskDetails.CancelTask();
				}
				else
				{
					stopAction.Invoke(() =>
					{
						stopButton.Enabled = true;
					});
				}

				stopButton.Enabled = false;
			};
			topRow.AddChild(stopButton);

			this.AddChild(detailsPanel);

			// Add rich progress controls
			if (taskDetails.Options?.RichProgressWidget?.Invoke() is GuiWidget guiWidget)
			{
				detailsPanel.AddChild(guiWidget);
			}
			else
			{
				expandButton.Expandable = false;
			}

			if (taskDetails.Options?.ReadOnlyReporting == true)
			{
				stopButton.Visible = false;
				pauseButton.Visible = false;
				resumeButton.Visible = false;

				// Ensure the top row is as big as it would be with buttons
				topRow.MinimumSize = new Vector2(0, resumeButton.Height);
			}

			SetExpansionMode(theme, detailsPanel, taskDetails.IsExpanded);

			taskDetails.ProgressChanged += TaskDetails_ProgressChanged;
		}

		public Color ProgressBackgroundColor
		{
			get => progressBar.BackgroundColor = this.BorderColor;
			set => progressBar.BackgroundColor = value;
		}

		private void SetExpansionMode(ThemeConfig theme, GuiWidget detailsPanel, bool isExpanded)
		{
			expandButton.Checked = isExpanded;
			progressBar.FillColor = isExpanded ? theme.Shade : theme.PrimaryAccentColor;
			detailsPanel.Visible = isExpanded;
			progressBar.Visible = !isExpanded;
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			taskDetails.ProgressChanged -= TaskDetails_ProgressChanged;
		}

		private void TaskDetails_ProgressChanged(object sender, ProgressStatus e)
		{
			if (expandButton.Text != e.Status
				&& !string.IsNullOrEmpty(e.Status)
				&& !expandButton.Text.Contains(e.Status, StringComparison.OrdinalIgnoreCase))
			{
				expandButton.Text = e.Status.Contains(taskDetails.Title, StringComparison.OrdinalIgnoreCase) ? e.Status : $"{taskDetails.Title} - {e.Status}";
			}

			double ratio = ((int)(e.Progress0To1 * Width))/this.Width;
			if (progressBar.RatioComplete != ratio)
			{
				progressBar.RatioComplete = ratio;
				this.Invalidate();
			}
		}
	}

	public static class StringExtensions
	{
		public static bool Contains(this string text, string value, StringComparison stringComparison)
		{
			return text.IndexOf(value, stringComparison) >= 0;
		}
	}
}
