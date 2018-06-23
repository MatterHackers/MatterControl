// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class AutoLinkInlineX : AutoFit{ }

	/// <summary>
	/// A Agg renderer for a <see cref="AutolinkInline"/>.
	/// </summary>
	/// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.AutolinkInline}" />
	public class AggAutolinkInlineRenderer : AggObjectRenderer<AutolinkInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, AutolinkInline link)
		{
			var url = link.Url;

			if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
			{
				url = "#";
			}

			//var hyperlink = new Hyperlink
			//{
			//	Command = Commands.Hyperlink,
			//	CommandParameter = url,
			//	NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
			//	ToolTip = url,
			//};

			renderer.WriteInline(new AutoLinkInlineX());
		}
	}
}
