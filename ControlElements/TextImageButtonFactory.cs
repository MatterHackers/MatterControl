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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class TextImageWidget : GuiWidget
    {
        ImageBuffer image;
        protected RGBA_Bytes fillColor = new RGBA_Bytes(0, 0, 0, 0);
        protected RGBA_Bytes borderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
        protected double borderWidth = 1;
        protected double borderRadius = 0;

        public TextImageWidget(string label, RGBA_Bytes fillColor, RGBA_Bytes borderColor, RGBA_Bytes textColor, double borderWidth, BorderDouble margin, ImageBuffer image = null, int fontSize = 12, FlowDirection flowDirection = FlowDirection.LeftToRight, double height = 40, double width = 0, bool centerText = false)
            : base()
        {
            this.image = image;
            this.fillColor = fillColor;
            this.borderColor = borderColor;
            this.borderWidth = borderWidth;
            this.Margin = new BorderDouble(0);
            this.Padding = new BorderDouble(0);

            TextWidget textWidget = new TextWidget(label, pointSize: fontSize);
            ImageWidget imageWidget;

            FlowLayoutWidget container = new FlowLayoutWidget(flowDirection);

            if (centerText)
            {
                // make sure the contents are centered
                GuiWidget leftSpace = new GuiWidget(0, 1);
                leftSpace.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                container.AddChild(leftSpace);
            }

            if (image != null && image.Width > 0)
            {                
                
                imageWidget = new ImageWidget(image);
                imageWidget.VAnchor = VAnchor.ParentCenter;
                container.AddChild(imageWidget);
            }

            if (label != "")
            {
                textWidget.VAnchor = VAnchor.ParentCenter;
                textWidget.TextColor = textColor;
                textWidget.Padding = new BorderDouble(3, 0);
                container.AddChild(textWidget);
            }

            if (centerText)
            {
                GuiWidget rightSpace = new GuiWidget(0, 1);
                rightSpace.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                container.AddChild(rightSpace);

                container.HAnchor = Agg.UI.HAnchor.ParentLeftRight | Agg.UI.HAnchor.FitToChildren;
            }
            container.VAnchor = Agg.UI.VAnchor.ParentCenter;

            container.MinimumSize = new Vector2(width, height);
            container.Margin = margin;
            this.AddChild(container);
            HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren;
            VAnchor = VAnchor.ParentCenter | Agg.UI.VAnchor.FitToChildren;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            if (borderColor.Alpha0To255 > 0)
            {
                RectangleDouble boarderRectangle = LocalBounds;
                //boarderRectangle.Inflate(-borderWidth / 2);
                RoundedRect rectBorder = new RoundedRect(boarderRectangle, this.borderRadius);

                graphics2D.Render(new Stroke(rectBorder, borderWidth), borderColor);
            }

            if (this.fillColor.Alpha0To255 > 0)
            {
                RectangleDouble insideBounds = LocalBounds;
                insideBounds.Inflate(-this.borderWidth);
                RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(this.borderRadius - this.borderWidth, 0));

                graphics2D.Render(rectInside, this.fillColor);
            }

            base.OnDraw(graphics2D);
        }
    }

    public class TextImageButtonFactory
    {
        public BorderDouble Margin = new BorderDouble(6, 0);
        public RGBA_Bytes normalFillColor = new RGBA_Bytes(0, 0, 0, 0);
        public RGBA_Bytes hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
        public RGBA_Bytes pressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
        public RGBA_Bytes disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);

        public RGBA_Bytes normalBorderColor = new RGBA_Bytes(255, 255, 255, 0);
        public RGBA_Bytes hoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);
        public RGBA_Bytes pressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);
        public RGBA_Bytes disabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
        public RGBA_Bytes checkedBorderColor = new RGBA_Bytes(255, 255, 255, 0);

        public RGBA_Bytes normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
        public RGBA_Bytes hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
        public RGBA_Bytes pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
        public RGBA_Bytes disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
        public int fontSize = 12;
        public double borderWidth = 1;
        public bool invertImageLocation = false;
        public bool AllowThemeToAdjustImage = true;
        FlowDirection flowDirection;
        public double FixedWidth = 0;
        public double FixedHeight = 40;

        public TooltipButton GenerateTooltipButton(string label, string normalImageName = null, string hoverImageName = null, string pressedImageName = null, string disabledImageName = null)
        {
            //Create button based on view container widget
            ButtonViewStates buttonViewWidget = getButtonView(label, normalImageName, hoverImageName, pressedImageName, disabledImageName);

            TooltipButton textImageButton = new TooltipButton(0, 0, buttonViewWidget);
            textImageButton.Margin = new BorderDouble(0);
            textImageButton.Padding = new BorderDouble(0);


            //Override the width if requested
            if (this.FixedWidth != 0)
            {
                buttonViewWidget.Width = this.FixedWidth;
                textImageButton.Width = this.FixedWidth;
            }

            //Override the height if requested
            buttonViewWidget.Height = this.FixedHeight;
            textImageButton.Height = this.FixedHeight;

            textImageButton.MouseEnterBounds += new EventHandler(onEnterTooltipButton);
            textImageButton.MouseLeaveBounds += new EventHandler(onExitTooltipButton);
            return textImageButton;
        }

        ImageBuffer LoadUpButtonImage(string imageName)
        {
            ImageBuffer buffer = new ImageBuffer(10, 10, 32, new BlenderBGRA());
            StaticData.Instance.LoadImage(Path.Combine("Icons", imageName), buffer);

            if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
            {
                InvertLightness.DoInvertLightness(buffer);
            }
            return buffer;
        }

        public Button GenerateEditButton()
        {
            Button editButton = new Button(0, 0, new ButtonViewThreeImage(LoadUpButtonImage("icon_edit_white.png"), LoadUpButtonImage("icon_edit_gray.png"), LoadUpButtonImage("icon_edit_black.png")));
            editButton.Margin = new BorderDouble(2, 2, 2, 0);
            editButton.VAnchor = Agg.UI.VAnchor.ParentTop;
            return editButton;
        }


        public GuiWidget GenerateGroupBoxLabelWithEdit(TextWidget textWidget, out Button editButton)
        {
            FlowLayoutWidget groupLableAndEditControl = new FlowLayoutWidget();

			editButton = new Button(0, 0, new ButtonViewThreeImage(LoadUpButtonImage("icon_edit_white.png"), LoadUpButtonImage("icon_edit_gray.png"), LoadUpButtonImage("icon_edit_black.png")));
            editButton.Margin = new BorderDouble(2, 2, 2, 0);
            editButton.VAnchor = Agg.UI.VAnchor.ParentBottom;
            textWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;
            groupLableAndEditControl.AddChild(textWidget);
            groupLableAndEditControl.AddChild(editButton);

            return groupLableAndEditControl;
            
        }

        public GuiWidget GenerateGroupBoxLabelWithEdit(string label, out Button editButton)
        {
            FlowLayoutWidget groupLableAndEditControl = new FlowLayoutWidget();

			editButton = new Button(0, 0, new ButtonViewThreeImage(LoadUpButtonImage("icon_edit_white.png"), LoadUpButtonImage("icon_edit_gray.png"), LoadUpButtonImage("icon_edit_black.png")));
            editButton.Margin = new BorderDouble(2, 2, 2, 0);
            editButton.VAnchor = Agg.UI.VAnchor.ParentBottom;
            TextWidget textLabel = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize:12);
            textLabel.VAnchor = Agg.UI.VAnchor.ParentBottom;
            groupLableAndEditControl.AddChild(textLabel);
            groupLableAndEditControl.AddChild(editButton);

            return groupLableAndEditControl;
        }

        private void onEnterTooltipButton(object sender, EventArgs e)
        {
            TooltipButton button = (TooltipButton)sender;
            HelpTextWidget.Instance.ShowHoverText(button.tooltipText);
        }

        private void onExitTooltipButton(object sender, EventArgs e)
        {
            HelpTextWidget.Instance.HideHoverText();
        }

        public CheckBox GenerateCheckBoxButton(string label, string normalImageName = null, string normalToPressedImageName = null, string pressedImageName = null, string pressedToNormalImageName = null, string pressedLabel = null)
        {
            CheckBoxViewStates checkBoxButtonViewWidget = getCheckBoxButtonView(label, normalImageName, normalToPressedImageName, pressedImageName, pressedToNormalImageName, pressedLabel);

            //Override the width if requested
            if (this.FixedWidth != 0)
            {
                checkBoxButtonViewWidget.Width = this.FixedWidth;
            }

            CheckBox textImageCheckBoxButton = new CheckBox(0, 0, checkBoxButtonViewWidget);
            textImageCheckBoxButton.Margin = new BorderDouble(0);
            textImageCheckBoxButton.Padding = new BorderDouble(0);

            return textImageCheckBoxButton;
        }

		public Button Generate(string label, string normalImageName = null, string hoverImageName = null, string pressedImageName = null, string disabledImageName = null, bool centerText = false)
        {
            //Create button based on view container widget
            ButtonViewStates buttonViewWidget = getButtonView(label, normalImageName, hoverImageName, pressedImageName, disabledImageName, centerText);
            Button textImageButton = new Button(0, 0, buttonViewWidget);

            textImageButton.Margin = new BorderDouble(0);
            textImageButton.Padding = new BorderDouble(0);

            //Override the width if requested
            if (this.FixedWidth != 0)
            {
                buttonViewWidget.Width = this.FixedWidth;
                textImageButton.Width = this.FixedWidth;
            }
            return textImageButton;
        }

        private ButtonViewStates getButtonView(string label, string normalImageName = null, string hoverImageName = null, string pressedImageName = null, string disabledImageName = null, bool centerText = false)
        {
            if (hoverImageName == null)
            {
                hoverImageName = normalImageName;
            }

            if (pressedImageName == null)
            {
                pressedImageName = hoverImageName;
            }

            if (disabledImageName == null)
            {
                disabledImageName = normalImageName;
            }

            ImageBuffer normalImage = new ImageBuffer();
            ImageBuffer pressedImage = new ImageBuffer();
            ImageBuffer hoverImage = new ImageBuffer();
            ImageBuffer disabledImage = new ImageBuffer();

            if (normalImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", normalImageName), normalImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(normalImage);
                }
            }

            if (hoverImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", pressedImageName), pressedImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(pressedImage);
                }
            }

            if (pressedImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", hoverImageName), hoverImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(hoverImage);
                }
            }

            if (disabledImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", disabledImageName), disabledImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(disabledImage);
                }
            }

            if (invertImageLocation)
            {
                flowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                flowDirection = FlowDirection.LeftToRight;
            }

            //Create the multi-state button view
            ButtonViewStates buttonViewWidget = new ButtonViewStates(
                new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText),
                new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, hoverImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText),
                new TextImageWidget(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText),
                new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, disabledImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText)
            );
            return buttonViewWidget;
        }

        private CheckBoxViewStates getCheckBoxButtonView(string label, string normalImageName = null, string normalToPressedImageName = null, string pressedImageName = null, string pressedToNormalImageName = null, string pressedLabel = null)
        {
            ImageBuffer normalImage = new ImageBuffer();
            ImageBuffer pressedImage = new ImageBuffer();
            ImageBuffer normalToPressedImage = new ImageBuffer();
            ImageBuffer pressedToNormalImage = new ImageBuffer();
            string pressedText = pressedLabel;

            if (pressedLabel == null)
            {
                pressedText = label;
            }

            if (normalToPressedImageName == null)
            {
                normalToPressedImageName = pressedImageName;
            }

            if (pressedImageName == null)
            {
                pressedImageName = normalToPressedImageName;
            }

            if (pressedToNormalImageName == null)
            {
                pressedToNormalImageName = normalImageName;
            }

            if (normalImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", normalImageName), normalImage);
                
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(normalImage);
                }
            }

            if (pressedImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", pressedImageName), pressedImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(pressedImage);
                }

            }

            if (normalToPressedImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", normalToPressedImageName), normalToPressedImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(normalToPressedImage);
                }
            }

            if (pressedToNormalImageName != null)
            {
                StaticData.Instance.LoadImage(Path.Combine("Icons", pressedToNormalImageName), pressedToNormalImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(pressedToNormalImage);
                }
            }

            if (invertImageLocation)
            {
                flowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                flowDirection = FlowDirection.LeftToRight;
            }

            //Create the multi-state button view
            GuiWidget normal = new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget normalHover = new TextImageWidget(label, hoverFillColor, normalBorderColor, hoverTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget switchNormalToPressed = new TextImageWidget(label, pressedFillColor, normalBorderColor, pressedTextColor, borderWidth, Margin, normalToPressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget pressed = new TextImageWidget(pressedText, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget pressedHover = new TextImageWidget(label, hoverFillColor, pressedBorderColor, hoverTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget switchPressedToNormal = new TextImageWidget(label, normalFillColor, pressedBorderColor, normalTextColor, borderWidth, Margin, pressedToNormalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);
            GuiWidget disabled = new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight);

            CheckBoxViewStates checkBoxButtonViewWidget = new CheckBoxViewStates(normal, normalHover, switchNormalToPressed, pressed, pressedHover, switchPressedToNormal, disabled);
            return checkBoxButtonViewWidget;
        }

        public RadioButton GenerateRadioButton(string label, string iconImageName = null)
        {
            ImageBuffer iconImage = null;

            if (iconImageName != null)
            {
                iconImage = new ImageBuffer();
                StaticData.Instance.LoadImage(Path.Combine("Icons", iconImageName), iconImage);
                if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
                {
                    InvertLightness.DoInvertLightness(iconImage);
                }
            }

            BorderDouble internalMargin = new BorderDouble(0);
            TextImageWidget nomalState = new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth, centerText: true);
            TextImageWidget hoverState = new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth, centerText: true);
            TextImageWidget checkingState = new TextImageWidget(label, hoverFillColor, checkedBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth, centerText: true);
            TextImageWidget checkedState = new TextImageWidget(label, pressedFillColor, checkedBorderColor, pressedTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth, centerText: true);
            TextImageWidget disabledState = new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth, centerText: true);
            RadioButtonViewStates checkBoxButtonViewWidget = new RadioButtonViewStates(nomalState, hoverState, checkingState, checkedState, disabledState);
            RadioButton radioButton = new RadioButton(checkBoxButtonViewWidget);
            radioButton.Margin = Margin;
            return radioButton;
        }
    }

    public class TooltipButton : Button
    {
        public string tooltipText = "";

        public TooltipButton(double x, double y, GuiWidget buttonView)
            :base(x, y, buttonView)
        {

        }
    }
}
