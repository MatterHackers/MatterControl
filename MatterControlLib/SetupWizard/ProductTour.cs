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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.Tour
{
	public class ProductTour
	{
		private List<TourLocation> tourLocations;
		private SystemWindow systemWindow;
		private ThemeConfig theme;
		private int _activeIndex = -1;

		public ProductTour(SystemWindow topWindow, List<TourLocation> tourLocations, ThemeConfig theme)
		{
			this.tourLocations = tourLocations;
			this.systemWindow = topWindow;
			this.Count = tourLocations.Count;
			this.theme = theme;
		}

		public IReadOnlyList<TourLocation> Locations => tourLocations;

		public static async void StartTour()
		{
			var topWindow = ApplicationController.Instance.MainView.TopmostParent() as SystemWindow;

			var tourLocations = await ApplicationController.Instance.LoadProductTour();

			// Finding matching widgets by name
			var visibleTourWidgets = topWindow.FindDescendants(tourLocations.Select(t => t.WidgetName));

			// Filter to on-screen items
			var visibleTourItems = tourLocations.Where(t =>
			{
				var widget = visibleTourWidgets.FirstOrDefault(w => w.Name == t.WidgetName && w.Widget.ActuallyVisibleOnScreen());

				// Update widget reference on tour object
				t.Widget = widget?.Widget;

				return widget != null;
			});

			var productTour = new ProductTour(topWindow, visibleTourItems.ToList(), ApplicationController.Instance.Theme);
			productTour.ShowNext();
		}

		public void ShowNext()
		{
			this.ActiveIndex = (this.ActiveIndex >= this.Count) ? 0 : this.ActiveIndex + 1;
		}

		public void ShowPrevious()
		{
			this.ActiveIndex = (this.ActiveIndex > 0) ? this.ActiveIndex - 1 : this.Count - 1;
		}

		public int Count { get; }

		public int ActiveIndex
		{
			get =>_activeIndex;
			set
			{
				if (_activeIndex != value)
				{
					// Constrain to valid range
					if (value < 0 || value >= this.Count)
					{
						value = 0;
					}

					_activeIndex = value;
					this.ActiveItem = tourLocations[_activeIndex];

					var tourOverlay = new TourOverlay(systemWindow, this, theme);
					systemWindow.AddChild(tourOverlay);
				}
			}
		}

		public TourLocation ActiveItem { get; private set; }
	}
}