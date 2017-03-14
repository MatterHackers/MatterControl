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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class TextImageWidget : GuiWidget
	{
		private ImageBuffer image;
		protected RGBA_Bytes fillColor = new RGBA_Bytes(0, 0, 0, 0);
		protected RGBA_Bytes borderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
		protected double borderWidth = 1;
		protected double borderRadius = 0;

		public TextImageWidget(string label, RGBA_Bytes fillColor, RGBA_Bytes borderColor, RGBA_Bytes textColor, double borderWidth, BorderDouble margin, ImageBuffer image = null, double fontSize = 12, FlowDirection flowDirection = FlowDirection.LeftToRight, double height = 40, double width = 0, bool centerText = false, double imageSpacing = 0)
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
				imageWidget.Margin = new BorderDouble(right: imageSpacing);
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
				RectangleDouble borderRectangle = LocalBounds;

				if (borderWidth > 0)
				{
					if (borderWidth == 1)
					{
						graphics2D.Rectangle(borderRectangle, borderColor);
					}
					else
					{
						//boarderRectangle.Inflate(-borderWidth / 2);
						RoundedRect rectBorder = new RoundedRect(borderRectangle, this.borderRadius);

						graphics2D.Render(new Stroke(rectBorder, borderWidth), borderColor);
					}
				}
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
		public double fontSize = 12;
		public double borderWidth = 1;
		public bool invertImageLocation = false;
		public bool AllowThemeToAdjustImage = true;
		private FlowDirection flowDirection;
		public double FixedWidth = 0;
		public double FixedHeight = 40;
		public double ImageSpacing = 0;

		public Button GenerateTooltipButton(string label, string normalImageName = null, string hoverImageName = null, string pressedImageName = null, string disabledImageName = null)
		{
			//Create button based on view container widget
			ButtonViewStates buttonViewWidget = getButtonView(label, normalImageName, hoverImageName, pressedImageName, disabledImageName);

			Button textImageButton = new Button(0, 0, buttonViewWidget);
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

			return textImageButton;
		}

		public Button GenerateTooltipButton(string label, ImageBuffer normalImageName, ImageBuffer hoverImageName = null, ImageBuffer pressedImageName = null, ImageBuffer disabledImageName = null)
		{
			//Create button based on view container widget
			ButtonViewStates buttonViewWidget = getButtonView(label, normalImageName, hoverImageName, pressedImageName, disabledImageName);

			Button textImageButton = new Button(0, 0, buttonViewWidget);
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

			return textImageButton;
		}

		public GuiWidget GenerateGroupBoxLabelWithEdit(TextWidget textWidget, out Button editButton)
		{
			FlowLayoutWidget groupLableAndEditControl = new FlowLayoutWidget();

			editButton = GetThemedEditButton();

			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = Agg.UI.VAnchor.ParentBottom;
			textWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;
			groupLableAndEditControl.AddChild(textWidget);
			groupLableAndEditControl.AddChild(editButton);

			return groupLableAndEditControl;
		}

		public static Button GetThemedEditButton()
		{
			ImageBuffer normalImage = StaticData.Instance.LoadIcon("icon_edit.png", 16, 16);

			Button editButton;
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				editButton = new Button(0, 0, new ButtonViewThreeImage(
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(.8).GetAsRGBA_Bytes()),
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(.9).GetAsRGBA_Bytes()),
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(1).GetAsRGBA_Bytes())));
			}
			else
			{
				editButton = new Button(0, 0, new ButtonViewThreeImage(
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(.4).GetAsRGBA_Bytes()),
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(.2).GetAsRGBA_Bytes()),
					SetToColor.CreateSetToColor(normalImage, RGBA_Bytes.White.AdjustLightness(0).GetAsRGBA_Bytes())));
			}

			return editButton;
		}

		public GuiWidget GenerateGroupBoxLabelWithEdit(string label, out Button editButton)
		{
			FlowLayoutWidget groupLableAndEditControl = new FlowLayoutWidget();

			editButton = GetThemedEditButton();

			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = Agg.UI.VAnchor.ParentBottom;
			TextWidget textLabel = new TextWidget(label, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 12);
			textLabel.VAnchor = Agg.UI.VAnchor.ParentBottom;
			groupLableAndEditControl.AddChild(textLabel);
			groupLableAndEditControl.AddChild(editButton);

			return groupLableAndEditControl;
		}

		public CheckBox GenerateCheckBoxButton(string label, ImageBuffer normalImage, ImageBuffer normalToPressedImage = null, ImageBuffer pressedImage = null, ImageBuffer pressedToNormalImage = null, string pressedLabel = null)
		{
			if (pressedImage == null) pressedImage = normalImage;
			if (pressedToNormalImage == null) pressedToNormalImage = normalToPressedImage;

			CheckBoxViewStates checkBoxButtonViewWidget = getCheckBoxButtonView(label, normalImage, normalToPressedImage, pressedImage, pressedToNormalImage, pressedLabel);

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

		public Button Generate(string label, ImageBuffer normalImage, ImageBuffer hoverImage = null, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null, bool centerText = false)
		{
			//Create button based on view container widget
			ButtonViewStates buttonViewWidget = getButtonView(label, normalImage, hoverImage, pressedImage, disabledImage, centerText);
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
			ImageBuffer normalImage = null;
			ImageBuffer pressedImage = null;
			ImageBuffer hoverImage = null;
			ImageBuffer disabledImage = null;

			if (normalImageName != null)
			{
				normalImage = new ImageBuffer();
				StaticData.Instance.LoadIcon(normalImageName, normalImage);
			}

			if (hoverImageName != null)
			{
				hoverImage = new ImageBuffer();
				StaticData.Instance.LoadIcon(hoverImageName, hoverImage);
			}

			if (pressedImageName != null)
			{
				pressedImage = new ImageBuffer();
				StaticData.Instance.LoadIcon(pressedImageName, pressedImage);
			}

			if (disabledImageName != null)
			{
				disabledImage = new ImageBuffer();
				StaticData.Instance.LoadIcon(disabledImageName, disabledImage);
			}

			return getButtonView(label, normalImage, hoverImage, pressedImage, disabledImage, centerText);
		}

		private ButtonViewStates getButtonView(string label, ImageBuffer normalImage = null, ImageBuffer hoverImage = null, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null, bool centerText = false)
		{
			if (hoverImage == null && normalImage != null)
			{
				hoverImage = new ImageBuffer(normalImage);
			}

			if (pressedImage == null && hoverImage != null)
			{
				pressedImage = new ImageBuffer(hoverImage);
			}

			if (disabledImage == null && normalImage != null)
			{
				// Generate the disabled image by lowering the alpha
				disabledImage = normalImage.Multiply(new RGBA_Bytes(255, 255, 255, 150));
			}

			if (ActiveTheme.Instance.IsDarkTheme
				&& AllowThemeToAdjustImage)
			{
				if (normalImage != null)
				{
					// make copies so we don't change source data
					normalImage = new ImageBuffer(normalImage);
					normalImage.InvertLightness();
				}
				if (pressedImage != null)
				{
					pressedImage.InvertLightness();
					pressedImage = new ImageBuffer(pressedImage);
				}
				if (hoverImage != null)
				{
					hoverImage = new ImageBuffer(hoverImage);
					hoverImage.InvertLightness();
				}
				if (disabledImage != null)
				{
					disabledImage = new ImageBuffer(disabledImage);
					disabledImage.InvertLightness();
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
				new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText, imageSpacing: ImageSpacing),
				new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, hoverImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText, imageSpacing: ImageSpacing),
				new TextImageWidget(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText, imageSpacing: ImageSpacing),
				new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, disabledImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, centerText: centerText, imageSpacing: ImageSpacing)
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
				StaticData.Instance.LoadIcon(normalImageName, normalImage);

				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					normalImage.InvertLightness();
				}
			}

			if (pressedImageName != null)
			{
				StaticData.Instance.LoadIcon(pressedImageName, pressedImage);
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					pressedImage.InvertLightness();
				}
			}

			if (normalToPressedImageName != null)
			{
				StaticData.Instance.LoadIcon(normalToPressedImageName, normalToPressedImage);
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					normalToPressedImage.InvertLightness();
				}
			}

			if (pressedToNormalImageName != null)
			{
				StaticData.Instance.LoadIcon(pressedToNormalImageName, pressedToNormalImage);
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					pressedToNormalImage.InvertLightness();
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

		private CheckBoxViewStates getCheckBoxButtonView(string label, ImageBuffer normalImage = null,
			ImageBuffer pressedImage = null,
			ImageBuffer normalToPressedImage = null,
			ImageBuffer pressedToNormalImage = null,
			string pressedLabel = null)
		{
			
			string pressedText = pressedLabel;

			if (pressedLabel == null)
			{
				pressedText = label;
			}

			if (normalImage != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					normalImage.InvertLightness();
				}
			}

			if (pressedImage != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					pressedImage.InvertLightness();
				}
			}

			if (normalToPressedImage != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					normalToPressedImage.InvertLightness();
				}
			}

			if (pressedToNormalImage != null)
			{
				if (!ActiveTheme.Instance.IsDarkTheme && AllowThemeToAdjustImage)
				{
					pressedToNormalImage.InvertLightness();
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

		public RadioButton GenerateRadioButton(string label, ImageBuffer iconImage)
		{
			if (iconImage != null )
			{
				iconImage.InvertLightness();
				if (ActiveTheme.Instance.IsDarkTheme
					&& AllowThemeToAdjustImage)
				{
					iconImage.InvertLightness();
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

		public RadioButton GenerateRadioButton(string label, string iconImageName = null)
		{
			if (iconImageName != null)
			{
				return GenerateRadioButton(label, StaticData.Instance.LoadIcon(iconImageName));
			}

			return GenerateRadioButton(label, (ImageBuffer)null);
		}
	}
}