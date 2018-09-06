// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	/// <summary>
	/// A Agg renderer for a <see cref="LiteralInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.LiteralInline}" />
	public class AggLiteralInlineRenderer : AggObjectRenderer<LiteralInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, LiteralInline obj)
		{
			if (obj.Content.IsEmpty)
				return;

			renderer.WriteText(ref obj.Content);
		}
	}
}
