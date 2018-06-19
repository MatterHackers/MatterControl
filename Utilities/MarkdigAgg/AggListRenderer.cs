// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Renderers.Agg
{

	public class ListX : FlowLayoutWidget
	{
		public ListX()
			: base (FlowDirection.TopToBottom)
		{
			var theme = ApplicationController.Instance.Theme;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(0, 4, 0, 12);
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				textWidget.TextColor = new Color("#036ac3");
				textWidget.PointSize = 11;
			}

			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class ListItemX : FlowLayoutWidget
	{
		private ImageBuffer icon = AggContext.StaticData.LoadIcon("dot.png");
		public ListItemX()
		{
			var theme = ApplicationController.Instance.Theme;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit | HAnchor.Left;
			this.Margin = new BorderDouble(0, 4, 0, 12);

			this.AddChild(new ImageWidget(icon)
			{
				Margin = 3
			});
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			// Anything required...?

			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class AggListRenderer : AggObjectRenderer<ListBlock>
    {
        protected override void Write(AggRenderer renderer, ListBlock listBlock)
        {
            //var list = new List();

            //if (listBlock.IsOrdered)
            //{
            //    list.MarkerStyle = TextMarkerStyle.Decimal;

            //    if (listBlock.OrderedStart != null && (listBlock.DefaultOrderedStart != listBlock.OrderedStart))
            //    {
            //        list.StartIndex = int.Parse(listBlock.OrderedStart);
            //    }
            //}
            //else
            //{
            //    list.MarkerStyle = TextMarkerStyle.Disc;
            //}

			renderer.Push(new ListX()); // list);

            foreach (var item in listBlock)
            {
				renderer.Push(new ListItemX());
                renderer.WriteChildren(item as ListItemBlock);
                renderer.Pop();
            }

            renderer.Pop();
        }
    }
}
