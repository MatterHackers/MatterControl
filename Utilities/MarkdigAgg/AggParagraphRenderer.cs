// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	public class AutoFit : GuiWidget
	{
		public AutoFit()
		{
			this.HAnchor = HAnchor.Fit | HAnchor.Left;
			this.VAnchor = VAnchor.Fit;
		}
	}

	public class ParagraphX : FlowLeftRightWithWrapping
	{
		public ParagraphX()
		{
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;
		}
	}

	//public class ParagraphRenderer : 
	public class AggParagraphRenderer : AggObjectRenderer<ParagraphBlock>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, ParagraphBlock obj)
		{
			var paragraph = new ParagraphX();

			renderer.Push(paragraph);
			renderer.WriteLeafInline(obj);
			renderer.Pop();
		}
	}
}
