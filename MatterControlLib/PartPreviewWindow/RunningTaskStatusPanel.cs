﻿/*
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
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RunningTaskStatusPanel : GuiWidget
	{
		internal RunningTaskDetails taskDetails;

		private ProgressBar progressBar;
		private TextWidget textWidget;

		public RunningTaskStatusPanel(string title, RunningTaskDetails taskDetails, ThemeConfig theme)
		{
			this.taskDetails = taskDetails;
			this.Padding = new BorderDouble(3, 0);

			this.AddChild(new ImageWidget(StaticData.Instance.LoadIcon("wait.png", 14, 14).GrayToColor(theme.TextColor))
			{
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Left
			});

			this.AddChild(textWidget = new TextWidget(!string.IsNullOrWhiteSpace(title) ? title : taskDetails.Title, pointSize: theme.FontSize8, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 16),
				AutoExpandBoundsToText = true
			});

			progressBar = new ProgressBar()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute | VAnchor.Center,
				Height = 2 * GuiWidget.DeviceScale,
				FillColor = theme.PrimaryAccentColor,
				BorderColor = Color.Transparent,
				Margin = new BorderDouble(left: 16, bottom: 3, top: 15)
			};
			this.AddChild(progressBar);

			taskDetails.ProgressChanged += TaskDetails_ProgressChanged;
		}

		public Color ProgressBackgroundColor
		{
			get => progressBar.BackgroundColor = this.BorderColor;
			set => progressBar.BackgroundColor = value;
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			taskDetails.ProgressChanged -= TaskDetails_ProgressChanged;
		}

        private void TaskDetails_ProgressChanged(object sender, (double progress0To1, string status) e)
        {
			if (e.status != null
				&& e.status.StartsWith("[[send to terminal]]"))
			{
                // strip of the prefix
                e.status = e.status.Substring("[[send to terminal]]".Length);
                if (sender is RunningTaskDetails details
					&& details.Owner is PrinterConfig printer)
				{
                    // only write it to the terminal
                    throw new NotImplementedException();
                    return;
				}
			}

			if (textWidget.Text != e.status
				&& !string.IsNullOrEmpty(e.status)
				&& !textWidget.Text.Contains(e.status, StringComparison.OrdinalIgnoreCase))
			{
				textWidget.Text = e.status.Contains(taskDetails.Title, StringComparison.OrdinalIgnoreCase) ? e.status : $"{taskDetails.Title} - {e.status}";
			}

			double ratio = ((int)(e.progress0To1 * Width)) / this.Width;
			if (progressBar.RatioComplete != ratio)
			{
				progressBar.RatioComplete = ratio;
				this.Invalidate();
			}
		}
	}
}
