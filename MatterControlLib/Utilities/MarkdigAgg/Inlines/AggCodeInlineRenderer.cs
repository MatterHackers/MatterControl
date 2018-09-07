// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class CodeInlineX : AutoFit{ }

	public class AggCodeInlineRenderer : AggObjectRenderer<CodeInline>
	{
		protected override void Write(AggRenderer renderer, CodeInline obj)
		{
			//var run = new Run(obj.Content);
			//run.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeStyleKey);

			renderer.WriteInline(new CodeInlineX());
		}
	}
}
