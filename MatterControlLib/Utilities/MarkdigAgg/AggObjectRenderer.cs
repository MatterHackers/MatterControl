// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

namespace Markdig.Renderers.Agg
{
	/// <summary>
	/// A base class for Agg rendering <see cref="Block"/> and <see cref="Markdig.Syntax.Inlines.Inline"/> Markdown objects.
	/// </summary>
	/// <typeparam name="TObject">The type of the object.</typeparam>
	/// <seealso cref="Markdig.Renderers.IMarkdownObjectRenderer" />
	public abstract class AggObjectRenderer<TObject> : MarkdownObjectRenderer<AggRenderer, TObject>
		where TObject : MarkdownObject
	{
	}
}
