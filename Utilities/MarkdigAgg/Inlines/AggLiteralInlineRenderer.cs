// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	// TODO: Placeholder only - figure out how to use WriteText
	public class LiteralInlineX : AutoFit{ }

	/// <summary>
	/// A WPF renderer for a <see cref="LiteralInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.WpfObjectRenderer{Markdig.Syntax.Inlines.LiteralInline}" />
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
