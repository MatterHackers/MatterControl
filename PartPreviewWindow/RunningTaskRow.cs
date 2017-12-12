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

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RunningTaskRow : FlowLayoutWidget
	{
		private ProgressBar progressBar;

		private ExpandCheckboxButton expandButton;
		private TextWidget mainTitle;

		internal RunningTaskDetails taskDetails;

		public RunningTaskRow(string title, RunningTaskDetails taskDetails, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.taskDetails = taskDetails;

			this.MinimumSize = new Vector2(100, 20);

			var detailsPanel = new GuiWidget()
			{
				MinimumSize = new Vector2(280, 20)
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
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				BorderColor = Color.Transparent,
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
				Margin = new BorderDouble(32, 4, theme.ButtonHeight * 2 + 5, 0),
			};
			rowContainer.AddChild(progressBar);

			mainTitle = new TextWidget("", pointSize: 7, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.Fit | HAnchor.Center,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Margin = new BorderDouble(top: 3),
				AutoExpandBoundsToText = true
			};
			rowContainer.AddChild(mainTitle);

			expandButton = new ExpandCheckboxButton(title, 10)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Fit,
				Checked = false,
				Padding = 0
			};
			expandButton.CheckedStateChanged += async (s, e) =>
			{
				progressBar.Visible = !expandButton.Checked;
				detailsPanel.Visible = expandButton.Checked;
			};
			topRow.AddChild(expandButton);

			topRow.AddChild(new HorizontalSpacer());

			var pauseButton = theme.ButtonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("fa-pause_12.png", IconColor.Theme));
			pauseButton.Margin = theme.ButtonSpacing;
			pauseButton.Enabled = false;
			pauseButton.Click += (s, e) =>
			{
				taskDetails.CancelTask();
			};
			topRow.AddChild(pauseButton);

			var stopButton = theme.ButtonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("fa-stop_12.png", IconColor.Theme));
			stopButton.Margin = theme.ButtonSpacing;
			stopButton.Click += (s, e) =>
			{
				taskDetails.CancelTask();
			};
			topRow.AddChild(stopButton);

			taskDetails.ProgressChanged += TaskDetails_ProgressChanged;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			base.OnClosed(e);
			taskDetails.ProgressChanged -= TaskDetails_ProgressChanged;
		}

		private void TaskDetails_ProgressChanged(object sender, ProgressStatus e)
		{
			if (expandButton.Text != e.Status 
				&& !string.IsNullOrEmpty(e.Status))
			{
				if (this.mainTitle.Text == "")
				{
					mainTitle.Text = expandButton.Text;
				}
				expandButton.Text = e.Status;
			}

			progressBar.RatioComplete = e.Progress0To1;
			this.Invalidate();
		}
	}
}
