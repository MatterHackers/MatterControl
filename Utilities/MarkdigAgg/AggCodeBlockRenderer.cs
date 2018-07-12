// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Renderers.Agg
{
	public class CodeBlockX : FlowLeftRightWithWrapping
	{
		private ThemeConfig theme;

		public CodeBlockX(ThemeConfig theme)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 12;
			this.Padding = 6;
			this.BackgroundColor = theme.MinimalShade;
		}
	}

	public class AggCodeBlockRenderer : AggObjectRenderer<CodeBlock>
    {
		private ThemeConfig theme;

		public AggCodeBlockRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

        protected override void Write(AggRenderer renderer, CodeBlock obj)
        {
            //var paragraph = new Paragraph();
            //paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);

            renderer.Push(new CodeBlockX(theme));
            renderer.WriteLeafRawLines(obj);
            renderer.Pop();
        }
    }
}
