// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using MatterHackers.Agg;
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

	public class ParagraphX : FlowLeftRightWithWrapping, IHardBreak
	{
		public ParagraphX(bool bottomMargin)
		{
			// Adding HAnchor and initial fixed width properties to resolve excess vertical whitespace added during collapse to width 0
			//
			// TODO: Revise impact to FlowLeftRightWithWrapping
			this.HAnchor = HAnchor.Stretch;
			this.Width = 5000;
			if (bottomMargin)
			{
				Margin = new BorderDouble(0, 0, 0, 12);
			}
		}
	}

	//public class ParagraphRenderer : 
	public class AggParagraphRenderer : AggObjectRenderer<ParagraphBlock>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, ParagraphBlock obj)
		{
			var bottomMargin = false;
			if (obj.Parent is MarkdownDocument document
				&& obj.Parent.Count > 1
				&& obj.Line != 0)
            {
				bottomMargin = true;
			}

			var paragraph = new ParagraphX(bottomMargin)
			{
				RowMargin = 0,
				RowPadding = 3
			};

			renderer.Push(paragraph);
			renderer.WriteLeafInline(obj);
			renderer.Pop();
		}
	}
}
