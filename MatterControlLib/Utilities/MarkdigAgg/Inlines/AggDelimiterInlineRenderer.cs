// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class DelimiterInlineX : AutoFit{ }

	/// <summary>
	/// A Agg renderer for a <see cref="DelimiterInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.DelimiterInline}" />
	public class AggDelimiterInlineRenderer : AggObjectRenderer<DelimiterInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, DelimiterInline obj)
		{
			renderer.WriteText(obj.ToLiteral());
			renderer.WriteChildren(obj);
		}
	}
}
