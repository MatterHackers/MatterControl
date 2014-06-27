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

using System;
using System.IO;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.HtmlParsing;

namespace MatterHackers.MatterControl
{
    public class AboutWindow : SystemWindow
    {
        public AboutWindow()
			: base(500, 640)
        {
            
            GuiWidget aboutPage = new AboutPage();
            aboutPage.AnchorAll();
            this.AddChild(aboutPage);

            this.Title = LocalizedString.Get("About MatterControl");
            this.ShowAsSystemWindow();
        }

        static AboutWindow aboutWindow = null;
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
    }
    
    
    public class AboutPage : GuiWidget
    {
        string htmlContent = null;

        GuiWidget htmlWidget;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes aboutTextColor = ActiveTheme.Instance.PrimaryTextColor;

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
                string aboutHtmlFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "AboutPage.html");
                htmlContent = File.ReadAllText(aboutHtmlFile);
            }
            
            htmlWidget = new FlowLayoutWidget(FlowDirection.TopToBottom);
            htmlWidget.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
            htmlWidget.HAnchor |= HAnchor.ParentCenter;

            htmlParser.ParseHtml(htmlContent, AddContent, CloseContent);

            customInfoTopToBottom.AddChild(htmlWidget);

            this.AddChild(customInfoTopToBottom);
        }

        FlowLayoutWidget currentRow;
        private void AddContent(HtmlParser htmlParser, string htmlContent)
        {
            ElementState elementState = htmlParser.CurrentElementState;
            string decodedHtml = HtmlParser.UrlDecode(htmlContent);
            switch (elementState.TypeName)
            {
                case "a":
                    {
                        Button linkButton = linkButtonFactory.Generate(decodedHtml);
                        StyledTypeFace styled = new StyledTypeFace(LiberationSansFont.Instance, elementState.PointSize);
                        double descentInPixels = styled.DescentInPixels;
                        linkButton.OriginRelativeParent = new VectorMath.Vector2(linkButton.OriginRelativeParent.x, linkButton.OriginRelativeParent.y + descentInPixels);
                        linkButton.Click += (sender, mouseEvent) => 
                        {
                            System.Diagnostics.Process.Start(elementState.Href); 
                        };
                        currentRow.AddChild(linkButton);
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

                    currentRow.AddChild(widgetToAdd);
                    break;

                case "tr":
                    currentRow = new FlowLayoutWidget();
                    if (elementState.HeightPercent == 100)
                    {
                        currentRow.VAnchor = VAnchor.ParentBottomTop;
                    }
                    if (elementState.Alignment == ElementState.AlignType.center)
                    {
                        currentRow.HAnchor |= HAnchor.ParentCenter;
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
                    break;

                case "table":
                    break;

                case "span":
                    break;

                case "tr":
                    htmlWidget.AddChild(currentRow);
                    currentRow = null;
                    break;

                case "td":
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public string DoTranslate(string content)
        {
            throw new NotImplementedException();
        }

        public string DoToUpper(string content)
        {
            throw new NotImplementedException();
        }

        public string GetVersionString(string content)
        {
            return VersionInfo.Instance.ReleaseVersion;
        }

        public string GetBuildString(string content)
        {
            return VersionInfo.Instance.BuildVersion;
        }

        public string CreateLinkButton(string content)
        {
            throw new NotImplementedException();
        }

        public string CreateCenteredButton(string content)
        {
            throw new NotImplementedException();
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

            // first AppData\Local\MatterControl\data\gcode
            string gcodeOutputPath = DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath;
            try
            {
                Directory.Delete(gcodeOutputPath, true);
            }
            catch (Exception)
            {
            }
        }

#if false // kevin code 2014 04 22
        System.Windows.Forms.WebBrowser browser;
        private void openBrowser(Uri url)
        {
            //SystemWindow browser = new SystemWindow(600,600);

            System.Windows.Forms.Form test = new System.Windows.Forms.Form();
            test.Icon = new System.Drawing.Icon(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "application.ico"));
            test.Height = 480;
            test.Width = 640;
            test.Text = "MatterControl";

            browser = new System.Windows.Forms.WebBrowser();
            browser.DocumentCompleted += browser_DocumentCompleted;
            browser.Navigate(url);
            browser.Dock = System.Windows.Forms.DockStyle.Fill;

            test.Controls.Add(browser);
            test.Show();
            //browser.AddChild(br);
            //browser.ShowAsSystemWindow();
        }

        void browser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {            
            if (browser.Url == e.Url)
            {                
                Console.WriteLine("Navigated to {0}", e.Url);
            }
            browser.Show();
        }
#endif
    }
}
