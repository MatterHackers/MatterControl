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
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.CreatorPlugins
{
    
    public class PluginChooserWindow : SystemWindow
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        List<GuiWidget> listWithValues = new List<GuiWidget>();

        ImageBuffer LoadImage(string imageName)
        {
            string path = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, imageName);
            ImageBuffer buffer = new ImageBuffer(10, 10, 32, new BlenderBGRA());
            ImageIO.LoadImageData(path, buffer);
            return buffer;
        }

        public PluginChooserWindow()
            : base(360, 300)
        {
			Title = LocalizedString.Get("Installed Plugins");            

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);

            {
				string elementHeaderLblBeg = LocalizedString.Get("Select a Design Tool");
				string elementHeaderLblFull = string.Format("{0}:", elementHeaderLblBeg);
				string elementHeaderLbl = elementHeaderLblFull;
				TextWidget elementHeader = new TextWidget(string.Format(elementHeaderLbl), pointSize: 14);
                elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                elementHeader.HAnchor = HAnchor.ParentLeftRight;
                elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

                headerRow.AddChild(elementHeader);
            }

            topToBottom.AddChild(headerRow);

            GuiWidget presetsFormContainer = new GuiWidget();
            //ListBox printerListContainer = new ListBox();
            {
                presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
                presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
                presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            }

            FlowLayoutWidget pluginRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            pluginRowContainer.AnchorAll();
            presetsFormContainer.AddChild(pluginRowContainer);

            foreach(CreatorInformation creatorInfo in RegisteredCreators.Instance.Creators)
            {
                ClickWidget pluginRow = new ClickWidget();         
                pluginRow.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                pluginRow.Height = 38;
			    pluginRow.BackgroundColor = RGBA_Bytes.White;
                pluginRow.Padding = new BorderDouble(3);
                pluginRow.Margin = new BorderDouble(6,0,6,6);                

                GuiWidget overlay = new GuiWidget();
                overlay.AnchorAll();
                overlay.Cursor = Cursors.Hand;
                
                FlowLayoutWidget macroRow = new FlowLayoutWidget();
                macroRow.AnchorAll();
                macroRow.BackgroundColor = RGBA_Bytes.White;

                if (creatorInfo.iconPath != "")
                {
                    ImageBuffer imageBuffer = LoadImage(creatorInfo.iconPath);
                    ImageWidget imageWidget = new ImageWidget(imageBuffer);
                    macroRow.AddChild(imageWidget);
                }

                TextWidget buttonLabel = new TextWidget(creatorInfo.description, pointSize: 14);

                buttonLabel.Margin = new BorderDouble(left: 10);
                buttonLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;                
                macroRow.AddChild(buttonLabel);

                FlowLayoutWidget hSpacer = new FlowLayoutWidget();
                hSpacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                macroRow.AddChild(hSpacer);

                CreatorInformation callCorrectFunctionHold = creatorInfo;
                pluginRow.Click += (sender, e) =>
                {
                    if (RegisteredCreators.Instance.Creators.Count > 0)
                    {
                        callCorrectFunctionHold.functionToLaunchCreator(null, null);
                    }
                    UiThread.RunOnIdle(CloseOnIdle);
                };
                pluginRow.AddChild(macroRow);
                pluginRow.AddChild(overlay);
                pluginRowContainer.AddChild(pluginRow);
            }

            topToBottom.AddChild(presetsFormContainer);
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;


            ShowAsSystemWindow();
			MinimumSize = new Vector2(360, 300);

			Button cancelPresetsButton = textImageButtonFactory.Generate(LocalizedString.Get("Cancel"));
            cancelPresetsButton.Click += (sender, e) => {
                UiThread.RunOnIdle(CloseOnIdle);            
            };

            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Padding = new BorderDouble(0, 3);

            GuiWidget hButtonSpacer = new GuiWidget();
            hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;
            
            buttonRow.AddChild(hButtonSpacer);
            buttonRow.AddChild(cancelPresetsButton);

            topToBottom.AddChild(buttonRow);

            AddChild(topToBottom);
        }

        void CloseOnIdle(object state)
        {
            Close();
        }
    }
}