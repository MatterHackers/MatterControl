// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class HeadingRowX : FlowLeftRightWithWrapping
	{
		public HeadingRowX()
		{
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch;
			this.Margin = new BorderDouble(0, 4, 0, 12);
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{
				//textWidget.TextColor = new Color("#036ac3");
				textWidget.PointSize = 14;
			}

			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class AggHeadingRenderer : AggObjectRenderer<HeadingBlock>
	{
		protected override void Write(AggRenderer renderer, HeadingBlock obj)
		{
			//var paragraph = new Paragraph();
			//ComponentResourceKey styleKey = null;

			//switch (obj.Level)
			//{
			//    case 1: styleKey = Styles.Heading1StyleKey; break;
			//    case 2: styleKey = Styles.Heading2StyleKey; break;
			//    case 3: styleKey = Styles.Heading3StyleKey; break;
			//    case 4: styleKey = Styles.Heading4StyleKey; break;
			//    case 5: styleKey = Styles.Heading5StyleKey; break;
			//    case 6: styleKey = Styles.Heading6StyleKey; break;
			//}

			//if (styleKey != null)
			//{
			//    paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, styleKey);
			//}

			renderer.Push(new HeadingRowX()); // paragraph);
			renderer.WriteLeafInline(obj);
			renderer.Pop();
		}
	}
}
