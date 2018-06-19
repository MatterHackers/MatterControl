// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class ThematicBreakX : AutoFit{ }

	public class AggThematicBreakRenderer : AggObjectRenderer<ThematicBreakBlock>
    {
        protected override void Write(AggRenderer renderer, ThematicBreakBlock obj)
        {
            //var line = new System.Windows.Shapes.Line { X2 = 1 };
            //line.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.ThematicBreakStyleKey);

            //var paragraph = new Paragraph
            //{
            //    Inlines = { new InlineUIContainer(line) }
            //};

			renderer.WriteBlock(new ThematicBreakX()); // paragraph);
        }
    }
}
