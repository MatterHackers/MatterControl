/*
Copyright (c) 2015, Lars Brubaker
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
#if __ANDROID__
#else
#define USE_SYSTEM_WINDOW
#endif

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl
{
	public class PerformanceResultsMCOverlay : FlowLayoutWidget, IPerformanceResults
	{
        public static IPerformanceResults CreateResultsSystemWindow(string name)
        {
            return new PerformanceResultsMCOverlay(name);
        }

        private class PannelsWidget : FlowLayoutWidget
		{
			public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
			{
				childToAdd.BoundsChanged += (sender, e) =>
				{
					GuiWidget child = sender as GuiWidget;
					if(child != null)
					{
						child.MinimumSize = new VectorMath.Vector2(Math.Max(child.MinimumSize.x, child.Width), 0);
					}
				};

				base.AddChild(childToAdd, indexInChildrenList);
			}
		}
		static PannelsWidget pannels = null;

		Dictionary<string, TextWidget> timers = new Dictionary<string, TextWidget>();

		private event EventHandler unregisterEvents;

		FlowLayoutWidget bottomToTop = new FlowLayoutWidget(FlowDirection.BottomToTop);

        private PerformanceResultsMCOverlay(string name)
			: base(FlowDirection.TopToBottom)
		{
			Margin = new BorderDouble(5);
			Padding = new BorderDouble(3);
			VAnchor |= VAnchor.ParentTop;

			if (pannels == null)
			{
				pannels = new PannelsWidget();
				pannels.Selectable = false;
				pannels.HAnchor |= HAnchor.ParentLeft;
				pannels.VAnchor |= VAnchor.ParentTop;
                UiThread.RunOnIdle(() => MatterControlApplication.Instance.AddChild(pannels));
			}

            // add in the column title
            {
                TextWidget titleWidget = new TextWidget(name, pointSize: 14)
                {
                    BackgroundColor = new RGBA_Bytes(),
                    TextColor = new RGBA_Bytes(20, 120, 20),
                };
                titleWidget.Printer.DrawFromHintedCache = true;
                AddChild(titleWidget);
            }

			AddChild(bottomToTop);

			pannels.AddChild(this);

			BackgroundColor = new RGBA_Bytes(RGBA_Bytes.White, 180);
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
		}

		public void SetTime(string name, double elapsedSeconds, int recursionCount)
		{
			if (!timers.ContainsKey(name))
			{
				TextWidget newTimeWidget = new TextWidget("waiting")
				{
					AutoExpandBoundsToText = true,
					TextColor = new RGBA_Bytes(120, 20, 20),
					HAnchor = HAnchor.ParentLeft,
				};
				newTimeWidget.Printer.DrawFromHintedCache = true;
				timers.Add(name, newTimeWidget);

				bottomToTop.AddChild(newTimeWidget);
			}

			timers[name].Margin = new BorderDouble(recursionCount * 5, 0, 0, 0);
			string outputText = "{0:0.00} ms - {1}".FormatWith(elapsedSeconds * 1000, name);
			if(recursionCount > 0)
			{
				if(recursionCount == 1)
				{
					outputText = "|_" + outputText;
				}
				else
				{
					outputText = new string(' ', recursionCount-1) + "|_" + outputText;
				}
			}

            // TODO: put this is a pre-draw variable to set next time we are going to draw
            // Doing it here causes an invalidate and endlelss drawing.
			timers[name].Text = outputText;
		}
	}
}
