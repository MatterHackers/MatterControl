/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.ImageProcessing;
using System;

namespace MatterHackers.MatterControl
{
	public class TextImageButtonFactory
	{
		public ButtonFactoryOptions Options { get; }

		public TextImageButtonFactory()
			: this(new ButtonFactoryOptions())
		{
		}

		public TextImageButtonFactory(ButtonFactoryOptions options)
		{
			this.Options = options;
		}

		// Private getters act as proxies to new options class
		public BorderDouble Margin => Options.Margin;
		public RGBA_Bytes normalFillColor => Options.Normal.FillColor;
		public RGBA_Bytes hoverFillColor => Options.Hover.FillColor;
		public RGBA_Bytes pressedFillColor => Options.Pressed.FillColor;
		public RGBA_Bytes disabledFillColor => Options.Disabled.FillColor;

		public RGBA_Bytes normalBorderColor => Options.Normal.BorderColor;
		public RGBA_Bytes hoverBorderColor => Options.Hover.BorderColor;
		public RGBA_Bytes pressedBorderColor => Options.Pressed.BorderColor;
		public RGBA_Bytes disabledBorderColor  => Options.Disabled.BorderColor;

		public RGBA_Bytes checkedBorderColor => Options.CheckedBorderColor;

		public RGBA_Bytes normalTextColor  => Options.Normal.TextColor;
		public RGBA_Bytes hoverTextColor  => Options.Hover.TextColor;
		public RGBA_Bytes pressedTextColor  => Options.Pressed.TextColor;
		public RGBA_Bytes disabledTextColor  => Options.Disabled.TextColor;

		public double fontSize => Options.FontSize;
		public double borderWidth => Options.BorderWidth;
		public bool invertImageLocation => Options.InvertImageLocation;
		public bool AllowThemeToAdjustImage => Options.AllowThemeToAdjustImage;
		private FlowDirection flowDirection => Options.FlowDirection;
		public double FixedWidth => Options.FixedWidth;
		public double FixedHeight => Options.FixedHeight;
		public double ImageSpacing => Options.ImageSpacing;

		public GuiWidget GenerateGroupBoxLabelWithEdit(TextWidget textWidget, out Button editButton)
		{
			FlowLayoutWidget groupLableAndEditControl = new FlowLayoutWidget();

			editButton = GenerateIconButton(StaticData.Instance.LoadIcon("icon_edit.png", 16, 16));

			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = VAnchor.Bottom;
			textWidget.VAnchor = VAnchor.Bottom;
			groupLableAndEditControl.AddChild(textWidget);
			groupLableAndEditControl.AddChild(editButton);

			return groupLableAndEditControl;
		}

		public Button GenerateIconButton(ImageBuffer icon, bool forceWhite = false)
		{
			if (ActiveTheme.Instance.IsDarkTheme || forceWhite)
			{
				icon.InvertLightness();
			}

			return new Button(0, 0,
				new ButtonViewThreeImage(
					icon.AjustAlpha(.7),
					icon.AjustAlpha(.9),
					icon.AjustAlpha(1),
					icon.AjustAlpha(.2)));
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

		public Button Generate(string label, ImageBuffer normalImage, ImageBuffer hoverImage = null, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null)
		{
			//Create button based on view container widget
			ButtonViewStates buttonViewWidget = getButtonView(label, normalImage, hoverImage, pressedImage, disabledImage);
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

		public Button Generate(string label, string normalImageName = null, string hoverImageName = null, string pressedImageName = null, string disabledImageName = null, bool centerText = false, double fixedWidth = -1)
		{
			// Create button based on view container widget
			ButtonViewStates buttonViewWidget = getButtonView(label, normalImageName, hoverImageName, pressedImageName, disabledImageName, centerText);
			Button textImageButton = new Button(0, 0, buttonViewWidget);

			textImageButton.Margin = new BorderDouble(0);
			textImageButton.Padding = new BorderDouble(0);

			// Allow fixedWidth parameter to override local .FixedWith property
			if (fixedWidth != -1)
			{
				if (fixedWidth > 0)
				{
					buttonViewWidget.Width = fixedWidth;
					textImageButton.Width = fixedWidth;
				}
			}
			else if (this.FixedWidth != 0)
			{
				//Override the width if requested
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

			return getButtonView(label, normalImage, hoverImage, pressedImage, disabledImage);
		}

		private ButtonViewStates getButtonView(string label, ImageBuffer normalImage = null, ImageBuffer hoverImage = null, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null)
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

			// TODO: This overrides users settings in a way that's completely unclear
			if (invertImageLocation)
			{
				Options.FlowDirection = FlowDirection.RightToLeft;
			}
			else
			{
				Options.FlowDirection = FlowDirection.LeftToRight;
			}

			//Create the multi-state button view
			ButtonViewStates buttonViewWidget = new ButtonViewStates(
				new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, hoverImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, disabledImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing)
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

			// TODO: This overrides users settings in a way that's completely unclear
			if (invertImageLocation)
			{
				Options.FlowDirection = FlowDirection.RightToLeft;
			}
			else
			{
				Options.FlowDirection = FlowDirection.LeftToRight;
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

			// TODO: This overrides users settings in a way that's completely unclear
			if (invertImageLocation)
			{
				Options.FlowDirection = FlowDirection.RightToLeft;
			}
			else
			{
				Options.FlowDirection = FlowDirection.LeftToRight;
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
			TextImageWidget nomalState = new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget hoverState = new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget checkingState = new TextImageWidget(label, hoverFillColor, checkedBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget checkedState = new TextImageWidget(label, pressedFillColor, checkedBorderColor, pressedTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget disabledState = new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
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

	public class ButtonOptionSection
	{
		public RGBA_Bytes FillColor { get; set; }
		public RGBA_Bytes BorderColor { get; set; }
		public RGBA_Bytes TextColor { get; set; }
	}

	public class ButtonFactoryOptions
	{
		public ButtonOptionSection Normal { get; set; }
		public ButtonOptionSection Hover { get; set; }
		public ButtonOptionSection Pressed { get; set; }
		public ButtonOptionSection Disabled { get; set; }

		public double FontSize { get; set; } = 12;
		public double BorderWidth { get; set; } = 1;
		public bool InvertImageLocation { get; set; } = false;
		public bool AllowThemeToAdjustImage { get; set; } = true;

		public double FixedWidth { get; set; } = 0;
		public double FixedHeight { get; set; } = 40;
		public double ImageSpacing { get; set; } = 0;

		public BorderDouble Margin { get; set; } = new BorderDouble(6, 0);
		public RGBA_Bytes CheckedBorderColor { get; set; } = new RGBA_Bytes(255, 255, 255, 0);
		public FlowDirection FlowDirection { get; set; }

		public ButtonFactoryOptions()
		{
			this.Margin = new BorderDouble(6, 0);

			this.Normal = new ButtonOptionSection()
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				FillColor = new RGBA_Bytes(0, 0, 0, 30),
				BorderColor = new RGBA_Bytes(255, 255, 255, 0)
			};

			this.Hover = new ButtonOptionSection()
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				FillColor = new RGBA_Bytes(0, 0, 0, 80),
				BorderColor = new RGBA_Bytes(0, 0, 0, 0)
			};

			this.Pressed = new ButtonOptionSection()
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				FillColor = new RGBA_Bytes(0, 0, 0, 0),
				BorderColor = new RGBA_Bytes(0, 0, 0, 0)
			};

			this.Disabled = new ButtonOptionSection()
			{

				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				FillColor = new RGBA_Bytes(255, 255, 255, 50),
				BorderColor = new RGBA_Bytes(0, 0, 0, 0)
			};
		}

		public ButtonFactoryOptions Clone(Action<ButtonFactoryOptions> callback)
		{
			var newItem = new ButtonFactoryOptions();

			newItem.AllowThemeToAdjustImage = this.AllowThemeToAdjustImage;
			newItem.BorderWidth = this.BorderWidth;
			newItem.CheckedBorderColor = this.CheckedBorderColor;
			newItem.FixedHeight = this.FixedHeight;
			newItem.FixedWidth = this.FixedWidth;
			newItem.FlowDirection = this.FlowDirection;
			newItem.FontSize = this.FontSize;
			newItem.ImageSpacing = this.ImageSpacing;
			newItem.InvertImageLocation = this.InvertImageLocation;
			newItem.Margin = this.Margin;

			newItem.Normal = new ButtonOptionSection()
			{
				TextColor = this.Normal.TextColor,
				FillColor = this.Normal.FillColor,
				BorderColor = this.Normal.BorderColor
			};

			newItem.Hover = new ButtonOptionSection()
			{
				TextColor = this.Hover.TextColor,
				FillColor = this.Hover.FillColor,
				BorderColor = this.Hover.BorderColor
			};

			newItem.Pressed = new ButtonOptionSection()
			{
				TextColor = this.Pressed.TextColor,
				FillColor = this.Pressed.FillColor,
				BorderColor = this.Pressed.BorderColor
			};

			newItem.Disabled = new ButtonOptionSection()
			{
				TextColor = this.Disabled.TextColor,
				FillColor = this.Disabled.FillColor,
				BorderColor = this.Disabled.BorderColor
			};

			callback(newItem);

			return newItem;
		}
	}
}