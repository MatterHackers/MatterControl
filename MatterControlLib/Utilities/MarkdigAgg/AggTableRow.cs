// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// Copyright (c) 2022, John Lewin
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System.Linq;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AggTableRow: FlowLayoutWidget
	{
		public AggTableRow()
		{
			this.VAnchor = VAnchor.Fit;
			this.Margin = new BorderDouble(3, 4, 0, 12);

			// Hack to force content on-screen (seemingly not working when set late/after constructor)
			VAnchor = VAnchor.Absolute;
			Height = 25;
		}

        public bool IsHeadingRow { get; set; }

		// Override AddChild to push styles to child elements when table rows are resolved to the tree
		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				// textWidget.TextColor = new Color("#036ac3");
				if (this.IsHeadingRow)
				{
					textWidget.Bold = true;
				}
			}
			else if (childToAdd is TextLinkX textLink)
			{
				foreach (var childTextWidget in childToAdd.Children.OfType<TextWidget>())
				{
					if (this.IsHeadingRow)
					{
						childTextWidget.Bold = true;
					}
				}
			}

			return base.AddChild(childToAdd, indexInChildrenList);
		}
	}
}
