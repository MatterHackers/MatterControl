// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
	public class EmphasisInlineX : FlowLayoutWidget
	{
		private char delimiter;

		public EmphasisInlineX(char delimiter)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			this.delimiter = delimiter;
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (childToAdd is TextWidget textWidget)
			{

				switch (delimiter)
				{
					case '~':
						textWidget.StrikeThrough = true;
						break;
					case '*':
					default:
						textWidget.Bold = true;
						break;

					//	case '_': Italic();
					//	case '^': Styles.SuperscriptStyleKey
					//	case '+': Styles.InsertedStyleKey
					//	case '=': Styles.MarkedStyleKey
				}
			}

			base.AddChild(childToAdd, indexInChildrenList);
		}
	}

	/// <summary>
	/// A Agg renderer for an <see cref="EmphasisInline"/>.
	/// </summary>
	/// <seealso cref="EmphasisInline" />
	public class AggEmphasisInlineRenderer : AggObjectRenderer<EmphasisInline>
	{
		protected override void Write(AggRenderer renderer, EmphasisInline obj)
		{
			//Span span = null;

			//switch (obj.DelimiterChar)
			//{
			//	case '*':
			//	case '_':
			//		span = obj.IsDouble ? (Span)new Bold() : new Italic();
			//		break;
			//	case '~':
			//		span = new Span();
			//		span.SetResourceReference(FrameworkContentElement.StyleProperty, obj.IsDouble ? Styles.StrikeThroughStyleKey : Styles.SubscriptStyleKey);
			//		break;
			//	case '^':
			//		span = new Span();
			//		span.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.SuperscriptStyleKey);
			//		break;
			//	case '+':
			//		span = new Span();
			//		span.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.InsertedStyleKey);
			//		break;
			//	case '=':
			//		span = new Span();
			//		span.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.MarkedStyleKey);
			//		break;
			//}

			if (true) //span != null)
			{
				renderer.Push(new EmphasisInlineX(obj.DelimiterChar));
				renderer.WriteChildren(obj);
				renderer.Pop();
			}
			else
			{
				renderer.WriteChildren(obj);
			}
		}
	}
}
