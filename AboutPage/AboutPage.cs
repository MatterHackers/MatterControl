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

namespace MatterHackers.MatterControl
{
	public class AboutPage : GuiWidget
	{
		private RGBA_Bytes aboutTextColor = ActiveTheme.Instance.PrimaryTextColor;
		private FlowLayoutWidget currentRowUnderConstruction;
		private FlowLayoutWidget currentATagUnderConstruction = null;
		private string htmlContent = null;

		private GuiWidget htmlWidget;
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public AboutPage()
		{
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.ParentTop;

			this.Padding = new BorderDouble(5);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			linkButtonFactory.fontSize = 12;
			linkButtonFactory.textColor = aboutTextColor;

			textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

			FlowLayoutWidget customInfoTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			customInfoTopToBottom.Name = "AboutPageCustomInfo";
			customInfoTopToBottom.HAnchor = HAnchor.ParentLeftRight;
			customInfoTopToBottom.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
			customInfoTopToBottom.Padding = new BorderDouble(5, 10, 5, 0);

			customInfoTopToBottom.AddChild(new UpdateControlView());
			//AddMatterHackersInfo(customInfoTopToBottom);
			customInfoTopToBottom.AddChild(new GuiWidget(1, 10));

			HtmlParser htmlParser = new HtmlParser();

			if (htmlContent == null)
			{
				string aboutHtmlFile = Path.Combine("OEMSettings", "AboutPage.html");
				htmlContent = StaticData.Instance.ReadAllText(aboutHtmlFile);
			}

			//htmlContent = File.ReadAllText("C:/Users/LarsBrubaker/Downloads/test.html");

			htmlWidget = new FlowLayoutWidget(FlowDirection.TopToBottom);
			htmlWidget.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
			htmlWidget.HAnchor |= HAnchor.ParentCenter;

			htmlParser.ParseHtml(htmlContent, AddContent, CloseContent);

			customInfoTopToBottom.AddChild(htmlWidget);

			this.AddChild(customInfoTopToBottom);
		}

		public static void DeleteCacheData()
		{
			// delete everything in the GCodeOutputPath
			//   AppData\Local\MatterControl\data\gcode
			// delete everything in the temp data that is not in use
			//   AppData\Local\MatterControl\data\temp
			//     plateImages
			//     project-assembly
			//     project-extract
			//     stl
			// delete all unreference models in Library
			//   AppData\Local\MatterControl\Library
			// delete all old update downloads
			//   AppData\updates

			// start cleaning out unused data
			// MatterControl\data\gcode
			RemoveDirectory(DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath);

			string userDataPath = DataStorage.ApplicationDataStorage.Instance.ApplicationUserDataPath;
			RemoveDirectory(Path.Combine(userDataPath, "updates"));

			HashSet<string> referencedPrintItemsFilePaths = new HashSet<string>();
			HashSet<string> referencedThumbnailFiles = new HashSet<string>();
			// Get a list of all the stl and amf files referenced in the queue or library.
			foreach (PrintItemWrapper printItem in QueueData.Instance.PrintItems)
			{
				string fileLocation = printItem.FileLocation;
				if (!referencedPrintItemsFilePaths.Contains(fileLocation))
				{
					referencedPrintItemsFilePaths.Add(fileLocation);
					referencedThumbnailFiles.Add(PartThumbnailWidget.GetImageFilenameForItem(printItem));
				}
			}

			foreach (PrintItemWrapper printItem in LibraryData.Instance.PrintItems)
			{
				string fileLocation = printItem.FileLocation;
				if (!referencedPrintItemsFilePaths.Contains(fileLocation))
				{
					referencedPrintItemsFilePaths.Add(fileLocation);
					referencedThumbnailFiles.Add(PartThumbnailWidget.GetImageFilenameForItem(printItem));
				}
			}

			// If the count is less than 0 then we have never run and we need to populate the library and queue still. So don't delete anything yet.
			if (referencedPrintItemsFilePaths.Count > 0)
			{
				CleanDirectory(userDataPath, referencedPrintItemsFilePaths, referencedThumbnailFiles);
			}
		}

		public string CreateCenteredButton(string content)
		{
			throw new NotImplementedException();
		}

		public string CreateLinkButton(string content)
		{
			throw new NotImplementedException();
		}

		public string DoToUpper(string content)
		{
			throw new NotImplementedException();
		}

		public string DoTranslate(string content)
		{
			throw new NotImplementedException();
		}

		public string GetBuildString(string content)
		{
			return VersionInfo.Instance.BuildVersion;
		}

		public string GetVersionString(string content)
		{
			return VersionInfo.Instance.ReleaseVersion;
		}

		private static int CleanDirectory(string path, HashSet<string> referencedPrintItemsFilePaths, HashSet<string> referencedThumbnailFiles)
		{
			int contentCount = 0;
			foreach (string directory in Directory.EnumerateDirectories(path))
			{
				int directoryContentCount = CleanDirectory(directory, referencedPrintItemsFilePaths, referencedThumbnailFiles);
				if (directoryContentCount == 0)
				{
					try
					{
						Directory.Delete(directory);
					}
					catch (Exception)
					{
					}
				}
				else
				{
					// it has a directory that has content
					contentCount++;
				}
			}

			foreach (string file in Directory.EnumerateFiles(path, "*.*"))
			{
				switch (Path.GetExtension(file).ToUpper())
				{
					case ".STL":
					case ".AMF":
					case ".GCODE":
						if (referencedPrintItemsFilePaths.Contains(file))
						{
							contentCount++;
						}
						else
						{
							try
							{
								File.Delete(file);
							}
							catch (Exception)
							{
							}
						}
						break;

					case ".PNG":
					case ".TGA":
						if (referencedThumbnailFiles.Contains(file))
						{
							contentCount++;
						}
						else
						{
							try
							{
								File.Delete(file);
							}
							catch (Exception)
							{
							}
						}
						break;

					case ".JSON":
						// may want to clean these up eventually
						contentCount++; // if we delete these we should not incement this
						break;

					default:
						// we have something in the directory that we are not going to delete
						contentCount++;
						break;
				}
			}

			return contentCount;
		}

		private static void RemoveDirectory(string directoryToRemove)
		{
			try
			{
				if (Directory.Exists(directoryToRemove))
				{
					Directory.Delete(directoryToRemove, true);
				}
			}
			catch (Exception)
			{
			}
		}

		public class ImageWidgetDelayedLoad : ImageWidget
		{
			bool startedLoad = false;
			string uriToLoad;

			public ImageWidgetDelayedLoad(ImageBuffer image, string uriToLoad)
				: base(image)
			{
				this.uriToLoad = uriToLoad;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				if (!startedLoad)
				{
					try
					{
						startedLoad = true;
						WebClient client = new WebClient();
						client.DownloadDataCompleted += client_DownloadDataCompleted;
						client.DownloadDataAsync(new Uri(uriToLoad));
					}
					catch (Exception)
					{
					}
				}

				base.OnDraw(graphics2D);
			}

			void client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
			{
				byte[] raw = e.Result;
				Stream stream = new MemoryStream(raw);
				ImageBuffer unScaledImage = new ImageBuffer(10, 10, 32, new BlenderBGRA());
				ImageIO.LoadImageData(stream, unScaledImage);
				while (unScaledImage.Width > Image.Width * 2)
				{
					ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, new BlenderBGRA());
					halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
					unScaledImage = halfImage;
				}
				Image.NewGraphics2D().Render(unScaledImage, 0, 0, 0, Image.Width / (double)unScaledImage.Width, Image.Height / (double)unScaledImage.Height);
				Image.MarkImageChanged();
			}
		}

		private void AddContent(HtmlParser htmlParser, string htmlContent)
		{
			ElementState elementState = htmlParser.CurrentElementState;
			string decodedHtml = HtmlParser.UrlDecode(htmlContent);
			switch (elementState.TypeName)
			{
				case "p":
					break;

				case "div":
					break;

				case "!DOCTYPE":
					break;

				case "body":
					break;

				case "img":
					{
						ImageBuffer image = new ImageBuffer(elementState.SizeFixed.x, elementState.SizeFixed.y, 32, new BlenderBGRA());
						ImageWidgetDelayedLoad imageWidget = new ImageWidgetDelayedLoad(image, elementState.src);
						// put the image into the widget when it is done downloading.
						
						if (currentATagUnderConstruction != null)
						{
							Button linkButton = new Button(0, 0, imageWidget);
							linkButton.Cursor = Cursors.Hand;
							linkButton.Click += (sender, mouseEvent) =>
							{
								MatterControlApplication.Instance.LaunchBrowser(elementState.Href);
							};
							currentATagUnderConstruction.AddChild(linkButton);
						}
						else
						{
							currentRowUnderConstruction.AddChild(imageWidget);
						}
					}
					break;

				case "a":
					{
						if (currentATagUnderConstruction == null)
						{
							currentATagUnderConstruction = new FlowLayoutWidget();
						}

						if (decodedHtml != null && decodedHtml != "")
						{
							Button linkButton = linkButtonFactory.Generate(decodedHtml);
							StyledTypeFace styled = new StyledTypeFace(LiberationSansFont.Instance, elementState.PointSize);
							double descentInPixels = styled.DescentInPixels;
							linkButton.OriginRelativeParent = new VectorMath.Vector2(linkButton.OriginRelativeParent.x, linkButton.OriginRelativeParent.y + descentInPixels);
							linkButton.Click += (sender, mouseEvent) =>
							{
								MatterControlApplication.Instance.LaunchBrowser(elementState.Href);
							};
							currentATagUnderConstruction.AddChild(linkButton);
						}
					}
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
							createdButton.Click += (sender, mouseEvent) => { ContactFormWindow.Open(); };
						}
						else if (elementState.Id == "clearCache")
						{
							createdButton.Click += (sender, mouseEvent) => { DeleteCacheData(); };
						}
					}

					if (elementState.VerticalAlignment == ElementState.VerticalAlignType.top)
					{
						widgetToAdd.VAnchor = VAnchor.ParentTop;
					}

					currentRowUnderConstruction.AddChild(widgetToAdd);
					break;

				case "tr":
					currentRowUnderConstruction = new FlowLayoutWidget();
					if (elementState.SizePercent.y == 100)
					{
						currentRowUnderConstruction.VAnchor = VAnchor.ParentBottomTop;
					}
					if (elementState.Alignment == ElementState.AlignType.center)
					{
						currentRowUnderConstruction.HAnchor |= HAnchor.ParentCenter;
					}
					break;

				default:
					throw new NotImplementedException("Don't know what to do with {0}".FormatWith(elementState.TypeName));
			}
		}

		private void CloseContent(HtmlParser htmlParser, string htmlContent)
		{
			ElementState elementState = htmlParser.CurrentElementState;
			switch (elementState.TypeName)
			{
				case "a":
					currentRowUnderConstruction.AddChild(currentATagUnderConstruction);
					currentATagUnderConstruction = null;
					break;

				case "table":
					break;

				case "span":
					break;

				case "tr":
					htmlWidget.AddChild(currentRowUnderConstruction);
					currentRowUnderConstruction = null;
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

	public class AboutWindow : SystemWindow
	{
		private static AboutWindow aboutWindow = null;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public AboutWindow()
			: base(500, 640)
		{
			GuiWidget aboutPage = new AboutPage();
			aboutPage.AnchorAll();
			this.AddChild(aboutPage);

			FlowLayoutWidget buttonRowContainer = new FlowLayoutWidget();
			buttonRowContainer.HAnchor = HAnchor.ParentLeftRight;
			buttonRowContainer.Padding = new BorderDouble(0, 3);
			AddChild(buttonRowContainer);

			Button cancelButton = textImageButtonFactory.Generate("Close");
			cancelButton.Click += (sender, e) => { CancelButton_Click(); };
			buttonRowContainer.AddChild(new HorizontalSpacer());
			buttonRowContainer.AddChild(cancelButton);

			this.Title = LocalizedString.Get("About MatterControl");
			this.ShowAsSystemWindow();
		}

		public static void Show()
		{
			if (aboutWindow == null)
			{
				aboutWindow = new AboutWindow();
				aboutWindow.Closed += (parentSender, e) =>
				{
					aboutWindow = null;
				};
			}
			else
			{
				aboutWindow.BringToFront();
			}
		}

		private void CancelButton_Click()
		{
			UiThread.RunOnIdle((state) =>
				{
					aboutWindow.Close();
				});
		}
	}
}