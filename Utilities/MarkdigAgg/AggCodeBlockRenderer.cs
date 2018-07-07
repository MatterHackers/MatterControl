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
		public CodeBlockX()
		{
			var theme = ApplicationController.Instance.Theme;
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Fit;
			Margin = 12;
			Padding = 6;
			BackgroundColor = theme.MinimalShade;
		}
	}

	public class AggCodeBlockRenderer : AggObjectRenderer<CodeBlock>
    {
        protected override void Write(AggRenderer renderer, CodeBlock obj)
        {
            //var paragraph = new Paragraph();
            //paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);

            renderer.Push(new CodeBlockX());
            renderer.WriteLeafRawLines(obj);
            renderer.Pop();
        }
    }
}
