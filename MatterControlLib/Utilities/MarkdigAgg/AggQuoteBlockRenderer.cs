// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class QuoteBlockX : AutoFit{ }

	public class AggQuoteBlockRenderer : AggObjectRenderer<QuoteBlock>
    {
        /// <inheritdoc/>
        protected override void Write(AggRenderer renderer, QuoteBlock obj)
        {
           // var section = new Section();

			renderer.Push(new QuoteBlockX()); // section);
            renderer.WriteChildren(obj);
            //section.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.QuoteBlockStyleKey);
            renderer.Pop();
        }
    }
}
