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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.CustomWidgets;

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
		public RGBA_Bytes normalFillColor => Options.NormalFillColor;
		public RGBA_Bytes hoverFillColor => Options.HoverFillColor;
		public RGBA_Bytes pressedFillColor => Options.PressedFillColor;
		public RGBA_Bytes disabledFillColor => Options.DisabledFillColor;

		public RGBA_Bytes normalBorderColor => Options.NormalBorderColor;
		public RGBA_Bytes hoverBorderColor => Options.HoverBorderColor;
		public RGBA_Bytes pressedBorderColor => Options.PressedBorderColor;
		public RGBA_Bytes disabledBorderColor  => Options.DisabledBorderColor;

		public RGBA_Bytes checkedBorderColor => Options.CheckedBorderColor;

		public RGBA_Bytes normalTextColor  => Options.NormalTextColor;
		public RGBA_Bytes hoverTextColor  => Options.HoverTextColor;
		public RGBA_Bytes pressedTextColor  => Options.PressedTextColor;
		public RGBA_Bytes disabledTextColor  => Options.DisabledTextColor;

		public double fontSize => Options.FontSize;
		public double borderWidth => Options.BorderWidth;
		public bool invertImageLocation => Options.InvertImageLocation;
		private FlowDirection flowDirection => Options.FlowDirection;
		public double FixedWidth => Options.FixedWidth;
		public double FixedHeight => Options.FixedHeight;
		public double ImageSpacing => Options.ImageSpacing;

		public GuiWidget GenerateGroupBoxLabelWithEdit(TextWidget textWidget, out Button editButton)
		{
			editButton = GenerateIconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme));
			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = VAnchor.Bottom;

			textWidget.VAnchor = VAnchor.Bottom;

			var groupLableAndEditControl = new FlowLayoutWidget();
			groupLableAndEditControl.AddChild(textWidget);
			groupLableAndEditControl.AddChild(editButton);

			return groupLableAndEditControl;
		}

		public Button GenerateIconButton(ImageBuffer icon)
		{
			return new IconButton(icon, ApplicationController.Instance.Theme);
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

		public Button Generate(string label, double fixedWidth = -1)
		{
			// Create button based on view container widget
			var buttonViewWidget = new ButtonViewStates(
				new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, null, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, null, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, null, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, null, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing)
			);

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
			return new ButtonViewStates(
				new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, normalImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, hoverImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, pressedImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing),
				new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, disabledImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, imageSpacing: ImageSpacing)
			);
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

			return new CheckBoxViewStates(normal, normalHover, switchNormalToPressed, pressed, pressedHover, switchPressedToNormal, disabled);
		}

		public RadioButton GenerateRadioButton(string label, ImageBuffer iconImage)
		{
			BorderDouble internalMargin = new BorderDouble(0);
			TextImageWidget nomalState = new TextImageWidget(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget hoverState = new TextImageWidget(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget checkingState = new TextImageWidget(label, hoverFillColor, checkedBorderColor, hoverTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget checkedState = new TextImageWidget(label, pressedFillColor, checkedBorderColor, pressedTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			TextImageWidget disabledState = new TextImageWidget(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, internalMargin, iconImage, flowDirection: flowDirection, fontSize: this.fontSize, height: this.FixedHeight, width: this.FixedWidth);
			RadioButtonViewStates checkBoxButtonViewWidget = new RadioButtonViewStates(nomalState, hoverState, checkingState, checkedState, disabledState);

			return new RadioButton(checkBoxButtonViewWidget)
			{
				Margin = Margin
			};
		}

		public RadioButton GenerateRadioButton(string label, string iconImageName = null)
		{
			if (iconImageName != null)
			{
				return GenerateRadioButton(label, AggContext.StaticData.LoadIcon(iconImageName));
			}

			return GenerateRadioButton(label, (ImageBuffer)null);
		}
	}

	public class ButtonFactoryOptions
	{
		public RGBA_Bytes NormalFillColor { get; set; }
		public RGBA_Bytes NormalBorderColor { get; set; }
		public RGBA_Bytes NormalTextColor { get; set; }

		public RGBA_Bytes HoverFillColor { get; set; }
		public RGBA_Bytes HoverBorderColor { get; set; }
		public RGBA_Bytes HoverTextColor { get; set; }

		public RGBA_Bytes PressedFillColor { get; set; }
		public RGBA_Bytes PressedBorderColor { get; set; }
		public RGBA_Bytes PressedTextColor { get; set; }

		public RGBA_Bytes DisabledFillColor { get; set; }
		public RGBA_Bytes DisabledBorderColor { get; set; }
		public RGBA_Bytes DisabledTextColor { get; set; }

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

			this.NormalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.NormalFillColor = new RGBA_Bytes(0, 0, 0, 30);
			this.NormalBorderColor = new RGBA_Bytes(255, 255, 255, 0);

			this.HoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.HoverFillColor = new RGBA_Bytes(0, 0, 0, 80);
			this.HoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);

			this.PressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.PressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
			this.PressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);

			this.DisabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.DisabledFillColor = new RGBA_Bytes(255, 255, 255, 50);
			this.DisabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		}

		public ButtonFactoryOptions(ButtonFactoryOptions cloneSource)
		{
			this.AllowThemeToAdjustImage = cloneSource.AllowThemeToAdjustImage;
			this.BorderWidth = cloneSource.BorderWidth;
			this.CheckedBorderColor = cloneSource.CheckedBorderColor;
			this.FixedHeight = cloneSource.FixedHeight;
			this.FixedWidth = cloneSource.FixedWidth;
			this.FlowDirection = cloneSource.FlowDirection;
			this.FontSize = cloneSource.FontSize;
			this.ImageSpacing = cloneSource.ImageSpacing;
			this.InvertImageLocation = cloneSource.InvertImageLocation;
			this.Margin = cloneSource.Margin;

			this.NormalTextColor = cloneSource.NormalTextColor;
			this.NormalFillColor = cloneSource.NormalFillColor;
			this.NormalBorderColor = cloneSource.NormalBorderColor;

			this.HoverTextColor = cloneSource.HoverTextColor;
			this.HoverFillColor = cloneSource.HoverFillColor;
			this.HoverBorderColor = cloneSource.HoverBorderColor;

			this.PressedTextColor = cloneSource.PressedTextColor;
			this.PressedFillColor = cloneSource.PressedFillColor;
			this.PressedBorderColor = cloneSource.PressedBorderColor;

			this.DisabledTextColor = cloneSource.DisabledTextColor;
			this.DisabledFillColor = cloneSource.DisabledFillColor;
			this.DisabledBorderColor = cloneSource.DisabledBorderColor;
		}
	}
}