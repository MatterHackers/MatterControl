// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license. 
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Renderers.Agg.Inlines
{
	public class CodeInlineX : FlowLayoutWidget
	{
		private ThemeConfig theme;

		public CodeInlineX(ThemeConfig theme)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Padding = 4;
			this.BackgroundColor = theme.MinimalShade;
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	public class AggCodeInlineRenderer : AggObjectRenderer<CodeInline>
	{
		private ThemeConfig theme;

		public AggCodeInlineRenderer(ThemeConfig theme)
		{
			this.theme = theme;
		}

		protected override void Write(AggRenderer renderer, CodeInline obj)
		{
			//var run = new Run(obj.Content);
			//run.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeStyleKey);

			//renderer.WriteInline(new CodeInlineX());
			renderer.Push(new CodeInlineX(theme));
			renderer.WriteText(obj.Content);
			renderer.Pop();
		}
	}
}
