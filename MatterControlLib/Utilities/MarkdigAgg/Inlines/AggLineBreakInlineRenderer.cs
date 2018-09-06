// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class LineBreakX : AutoFit, IHardBreak { }
	public class LineBreakSoftX : AutoFit{ }

	/// <summary>
	/// A Agg renderer for a <see cref="LineBreakInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.LineBreakInline}" />
	public class AggLineBreakInlineRenderer : AggObjectRenderer<LineBreakInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, LineBreakInline obj)
		{
			if (obj.IsHard)
			{
				renderer.WriteInline(new LineBreakX()); // new LineBreak());
			}
			else
			{
				// TODO: Remove soft - use WriteText
				renderer.WriteInline(new LineBreakSoftX()); // new LineBreak());

				// Soft line break.
				renderer.WriteText(" ");
			}
		}
	}
}

