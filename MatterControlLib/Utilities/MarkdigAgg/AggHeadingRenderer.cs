// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

namespace Markdig.Renderers.Agg
{
	public class AggHeadingRenderer : AggObjectRenderer<HeadingBlock>
	{
		protected override void Write(AggRenderer renderer, HeadingBlock obj)
		{
			// var paragraph = new Paragraph();
			// ComponentResourceKey styleKey = null;

			// switch (obj.Level)
			// {
			//    case 1: styleKey = Styles.Heading1StyleKey; break;
			//    case 2: styleKey = Styles.Heading2StyleKey; break;
			//    case 3: styleKey = Styles.Heading3StyleKey; break;
			//    case 4: styleKey = Styles.Heading4StyleKey; break;
			//    case 5: styleKey = Styles.Heading5StyleKey; break;
			//    case 6: styleKey = Styles.Heading6StyleKey; break;
			// }

			// if (styleKey != null)
			// {
			//    paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, styleKey);
			// }

			renderer.Push(new HeadingRowX()); // paragraph);
			renderer.WriteLeafInline(obj);
			renderer.Pop();
		}
	}
}
