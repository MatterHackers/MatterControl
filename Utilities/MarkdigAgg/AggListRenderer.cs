// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Renderers.Agg
{
	public class ListX : FlowLayoutWidget
	{
		public ListX()
			: base(FlowDirection.TopToBottom)
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
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
		private FlowLayoutWidget content;
		public ListItemX(ThemeConfig theme)
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;

			base.AddChild(new ImageWidget(AggContext.StaticData.LoadIcon("bullet.png", theme.InvertIcons))
			{
				Margin = new BorderDouble(top: 1, left: 10),
				VAnchor = VAnchor.Top,
			});

			base.AddChild(content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			});
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			// TODOD: Anything else required for list children?
			content.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class AggListRenderer : AggObjectRenderer<ListBlock>
	{
		private ThemeConfig theme;

		public AggListRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

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
				renderer.Push(new ListItemX(theme));
				renderer.WriteChildren(item as ListItemBlock);
				renderer.Pop();
			}

			renderer.Pop();
		}
	}
}
