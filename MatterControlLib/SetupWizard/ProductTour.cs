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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace MatterControlLib.SetupWizard
{
	public class ProductTour
	{
		private SystemWindow topWindow;
		private int nextLocationIndex = 0;

		public ProductTour(SystemWindow topWindow)
		{
			this.topWindow = topWindow;
		}

		public static void StartTour()
		{
			var topWindow = ApplicationController.Instance.MainView.TopmostParent();
			ShowLocation(topWindow, new ProductTour(topWindow as SystemWindow), 0);
		}

		public void ShowNext()
		{
			nextLocationIndex += 1;
			ShowLocation(topWindow, this, nextLocationIndex);
		}

		public void ShowPrevious()
		{
			nextLocationIndex -= 1;
			ShowLocation(topWindow, this, nextLocationIndex);
		}

		public int Count { get; }

		public int ActiveIndex { get; }

		public TourLocation ActiveItem { get; }

		private static async void ShowLocation(GuiWidget window, ProductTour productTour, int locationIndex, int direction = 1)
		{
			var tourLocations = await ApplicationController.Instance.LoadProductTour();

			if (locationIndex >= tourLocations.Count)
			{
				locationIndex -= tourLocations.Count;
			}

			// Find the widget on screen to show
			GuiWidget GetLocationWidget(ref int findLocationIndex, out int displayIndex2, out int displayCount2)
			{
				displayIndex2 = 0;
				displayCount2 = 0;

				int checkLocation = 0;
				GuiWidget tourLocationWidget = null;
				while (checkLocation < tourLocations.Count)
				{
					var foundChildren = window.FindDescendants(tourLocations[checkLocation].WidgetName);

					GuiWidget foundLocation = null;
					foreach (var widgetAndPosition in foundChildren)
					{
						if (widgetAndPosition.widget.ActuallyVisibleOnScreen())
						{
							foundLocation = widgetAndPosition.widget;
							// we have found a target that is visible on screen, count it up
							displayCount2++;
							break;
						}
					}

					checkLocation++;

					// if we have not found the target yet
					if (checkLocation >= findLocationIndex
						&& tourLocationWidget == null)
					{
						tourLocationWidget = foundLocation;
						// set the index to the count when we found the widget we want
						displayIndex2 = displayCount2 - 1;
						findLocationIndex = (checkLocation < tourLocations.Count) ? checkLocation : 0;
					}
				}

				return tourLocationWidget;
			}

			int displayIndex;
			int displayCount;
			GuiWidget targetWidget = GetLocationWidget(ref locationIndex, out displayIndex, out displayCount);

			if (targetWidget != null)
			{
				var tourOverlay = new TourOverlay(
					window,
					productTour,
					targetWidget,
					tourLocations[locationIndex].Description,
					ApplicationController.Instance.Theme,
					locationIndex + 1,
					displayIndex,
					displayCount);
				window.AddChild(tourOverlay);
			}
		}
	}
}