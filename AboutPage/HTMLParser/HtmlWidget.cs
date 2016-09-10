/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.HtmlParsing;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl
{
	public class HtmlWidget : FlowLayoutWidget
	{
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private Stack<GuiWidget> elementsUnderConstruction = new Stack<GuiWidget>();
		HtmlParser htmlParser = new HtmlParser();

		public HtmlWidget(string htmlContent, RGBA_Bytes aboutTextColor)
			: base(FlowDirection.TopToBottom)
		{
			this.Name = "HtmlWidget";
			elementsUnderConstruction.Push(this);
			linkButtonFactory.fontSize = 12;
			linkButtonFactory.textColor = aboutTextColor;

			textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

			htmlParser.ParseHtml(htmlContent, AddContent, CloseContent);

			VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
			HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
		}

		public class WrappingTextWidget : GuiWidget
		{
			private String unwrappedMessage;
			private TextWidget messageContainer;

			public WrappingTextWidget(string text, double pointSize = 12, Justification justification = Justification.Left, RGBA_Bytes textColor = new RGBA_Bytes(), bool ellipsisIfClipped = true, bool underline = false, RGBA_Bytes backgroundColor = new RGBA_Bytes())
			{
				unwrappedMessage = text;
				messageContainer = new TextWidget(text, 0, 0, pointSize, justification, textColor, ellipsisIfClipped, underline);
				this.BackgroundColor = backgroundColor;
				messageContainer.AutoExpandBoundsToText = true;
				messageContainer.HAnchor = HAnchor.ParentLeft;
				messageContainer.VAnchor = VAnchor.ParentBottom;
				this.HAnchor = HAnchor.ParentLeftRight;
				this.VAnchor = VAnchor.FitToChildren;

				AddChild(messageContainer);
			}

			public override void OnBoundsChanged(EventArgs e)
			{
				AdjustTextWrap();
				base.OnBoundsChanged(e);
			}

			private void AdjustTextWrap()
			{
				if (messageContainer != null)
				{
					double wrappingSize = this.Width - this.Padding.Width;
					if (wrappingSize > 0)
					{
						EnglishTextWrapping wrapper = new EnglishTextWrapping(messageContainer.Printer.TypeFaceStyle.EmSizeInPoints);
						string wrappedMessage = wrapper.InsertCRs(unwrappedMessage, wrappingSize);
						messageContainer.Text = wrappedMessage;
					}
				}
			}
		}

		// Replace multiple white spaces with single whitespace
		private static readonly Regex replaceMultipleWhiteSpacesWithSingleWhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

		private void AddContent(HtmlParser htmlParser, string htmlContent)
		{
			ElementState elementState = htmlParser.CurrentElementState;
			htmlContent = replaceMultipleWhiteSpacesWithSingleWhitespaceRegex.Replace(htmlContent, " ");
			string decodedHtml = HtmlParser.UrlDecode(htmlContent);
			switch (elementState.TypeName)
			{
				case "a":
					{
						elementsUnderConstruction.Push(new FlowLayoutWidget());
						elementsUnderConstruction.Peek().Name = "a";

						if (decodedHtml != null && decodedHtml != "")
						{
							Button linkButton = linkButtonFactory.Generate(decodedHtml.Replace("\r\n", "\n"));
							StyledTypeFace styled = new StyledTypeFace(LiberationSansFont.Instance, elementState.PointSize);
							double descentInPixels = styled.DescentInPixels;
							linkButton.OriginRelativeParent = new VectorMath.Vector2(linkButton.OriginRelativeParent.x, linkButton.OriginRelativeParent.y + descentInPixels);
							linkButton.Click += (sender, mouseEvent) =>
							{
								MatterControlApplication.Instance.LaunchBrowser(elementState.Href);
							};
							elementsUnderConstruction.Peek().AddChild(linkButton);
						}
					}
					break;

				case "h1":
				case "p":
					{
						elementsUnderConstruction.Push(new FlowLayoutWidget());
						elementsUnderConstruction.Peek().Name = "p";
						elementsUnderConstruction.Peek().HAnchor = HAnchor.ParentLeftRight;

						if (decodedHtml != null && decodedHtml != "")
						{
							WrappingTextWidget content = new WrappingTextWidget(decodedHtml, pointSize: elementState.PointSize, textColor: ActiveTheme.Instance.PrimaryTextColor);
							//content.VAnchor = VAnchor.ParentTop;
							elementsUnderConstruction.Peek().AddChild(content);
						}
					}
					break;

				case "div":
					{
						elementsUnderConstruction.Push(new FlowLayoutWidget());
						elementsUnderConstruction.Peek().Name = "div";

						if (decodedHtml != null && decodedHtml != "")
						{
							TextWidget content = new TextWidget(decodedHtml, pointSize: elementState.PointSize, textColor: ActiveTheme.Instance.PrimaryTextColor);
							elementsUnderConstruction.Peek().AddChild(content);
						}
					}
					break;

				case "!DOCTYPE":
					break;

				case "body":
					break;

				case "img":
					{
						ImageBuffer image = new ImageBuffer(elementState.SizeFixed.x, elementState.SizeFixed.y, 32, new BlenderBGRA());
						ImageWidget imageWidget = new ImageWidget(image);
						imageWidget.Load += (s, e) => StaticData.DownloadToImageAsync(image, elementState.src);
						// put the image into the widget when it is done downloading.

						if (elementsUnderConstruction.Peek().Name == "a")
						{
							Button linkButton = new Button(0, 0, imageWidget);
							linkButton.Cursor = Cursors.Hand;
							linkButton.Click += (sender, mouseEvent) =>
							{
								MatterControlApplication.Instance.LaunchBrowser(elementState.Href);
							};
							elementsUnderConstruction.Peek().AddChild(linkButton);
						}
						else
						{
							elementsUnderConstruction.Peek().AddChild(imageWidget);
						}
					}
					break;

				case "input":
					break;

				case "table":
					break;

				case "td":
				case "span":
					GuiWidget widgetToAdd;

					if (elementState.Classes.Contains("translate"))
					{
						decodedHtml = decodedHtml.Localize();
					}
					if (elementState.Classes.Contains("toUpper"))
					{
						decodedHtml = decodedHtml.ToUpper();
					}
					if (elementState.Classes.Contains("versionNumber"))
					{
						decodedHtml = VersionInfo.Instance.ReleaseVersion;
					}
					if (elementState.Classes.Contains("buildNumber"))
					{
						decodedHtml = VersionInfo.Instance.BuildVersion;
					}

					Button createdButton = null;
					if (elementState.Classes.Contains("centeredButton"))
					{
						createdButton = textImageButtonFactory.Generate(decodedHtml);
						widgetToAdd = createdButton;
					}
					else if (elementState.Classes.Contains("linkButton"))
					{
						double oldFontSize = linkButtonFactory.fontSize;
						linkButtonFactory.fontSize = elementState.PointSize;
						createdButton = linkButtonFactory.Generate(decodedHtml);
						StyledTypeFace styled = new StyledTypeFace(LiberationSansFont.Instance, elementState.PointSize);
						double descentInPixels = styled.DescentInPixels;
						createdButton.OriginRelativeParent = new VectorMath.Vector2(createdButton.OriginRelativeParent.x, createdButton.OriginRelativeParent.y + descentInPixels);
						widgetToAdd = createdButton;
						linkButtonFactory.fontSize = oldFontSize;
					}
					else
					{
						TextWidget content = new TextWidget(decodedHtml, pointSize: elementState.PointSize, textColor: ActiveTheme.Instance.PrimaryTextColor);
						widgetToAdd = content;
					}

					if (createdButton != null)
					{
						if (elementState.Id == "sendFeedback")
						{
							createdButton.Click += (s, e) =>  ContactFormWindow.Open();
						}
						else if (elementState.Id == "clearCache")
						{
							createdButton.Click += (s, e) => AboutWidget.DeleteCacheData(0);
						}
					}

					if (elementState.VerticalAlignment == ElementState.VerticalAlignType.top)
					{
						widgetToAdd.VAnchor = VAnchor.ParentTop;
					}

					elementsUnderConstruction.Peek().AddChild(widgetToAdd);
					break;

				case "tr":
					elementsUnderConstruction.Push(new FlowLayoutWidget());
					elementsUnderConstruction.Peek().Name = "tr";
					if (elementState.SizePercent.y == 100)
					{
						elementsUnderConstruction.Peek().VAnchor = VAnchor.ParentBottomTop;
					}
					if (elementState.Alignment == ElementState.AlignType.center)
					{
						elementsUnderConstruction.Peek().HAnchor |= HAnchor.ParentCenter;
					}
					break;

				default:
					throw new NotImplementedException("Don't know what to do with '{0}'".FormatWith(elementState.TypeName));
			}
		}

		private void CloseContent(HtmlParser htmlParser, string htmlContent)
		{
			ElementState elementState = htmlParser.CurrentElementState;
			switch (elementState.TypeName)
			{
				case "a":
					GuiWidget aWidget = elementsUnderConstruction.Pop();
					if (aWidget.Name != "a")
					{
						throw new Exception("Should have been 'a'.");
					}
					elementsUnderConstruction.Peek().AddChild(aWidget);
					break;

				case "body":
					break;

				case "h1":
				case "p":
					GuiWidget pWidget = elementsUnderConstruction.Pop();
					if (pWidget.Name != "p")
					{
						throw new Exception("Should have been 'p'.");
					}
					elementsUnderConstruction.Peek().AddChild(pWidget);
					break;

				case "div":
					GuiWidget divWidget = elementsUnderConstruction.Pop();
					if (divWidget.Name != "div")
					{
						throw new Exception("Should have been 'div'.");
					}
					elementsUnderConstruction.Peek().AddChild(divWidget);
					break;

				case "input":
					break;

				case "table":
					break;

				case "span":
					break;

				case "tr":
					GuiWidget trWidget = elementsUnderConstruction.Pop();
					if (trWidget.Name != "tr")
					{
						throw new Exception("Should have been 'tr'.");
					}
					elementsUnderConstruction.Peek().AddChild(trWidget);
					break;

				case "td":
					break;

				case "img":
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}