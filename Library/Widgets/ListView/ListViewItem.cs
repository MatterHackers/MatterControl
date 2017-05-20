/*
Copyright (c) 2017, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListViewItem
	{
		public ILibraryItem Model { get; }
		public ListView ListView { get; }
		public string Text { get; internal set; }

		public GuiWidget ProgressTarget { get; internal set; }

		public ListViewItemBase ViewWidget { get; set; }

		ProgressControl processingProgressControl;

		internal void ProgressReporter(double progress0To1, string processingState, out bool continueProcessing)
		{
			continueProcessing = true;

			if (processingProgressControl == null)
			{
				return;
			}

			processingProgressControl.Visible = progress0To1 != 0;
			processingProgressControl.RatioComplete = progress0To1;
			processingProgressControl.ProcessType = processingState;

			if (progress0To1 == 1)
			{
				EndProgress();
			}
		}

		public ListViewItem(ILibraryItem listItemData, ListView dragConsumer)
		{
			this.ListView = dragConsumer;
			this.Model = listItemData;
		}

		public event EventHandler<MouseEventArgs> DoubleClick;

		internal void OnDoubleClick()
		{
			DoubleClick?.Invoke(this, null);
		}

		public void StartProgress()
		{
			processingProgressControl = new ProgressControl("Loading...".Localize(), RGBA_Bytes.Black, ActiveTheme.Instance.SecondaryAccentColor, (int)(100 * GuiWidget.DeviceScale), 5, 0)
			{
				PointSize = 8,
				Margin = 0,
				Visible = true
			};
			
			ProgressTarget?.AddChild(processingProgressControl);
		}

		public void EndProgress()
		{
			UiThread.RunOnIdle(() =>
			{
				if (processingProgressControl == null)
				{
					return;
				}

				processingProgressControl.Close();
				processingProgressControl = null;
			}, 1);
		}
	}
}