﻿/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class ImageButtonFactory
	{
		public bool InvertImageColor { get; set; } = true;

		public static CheckBox CreateToggleSwitch(bool initialState)
		{
			string on = "On";
			string off = "Off";
			if (StaticData.Instance != null)
			{
				on = on.Localize();
				off = off.Localize();
			}
			ToggleSwitchView toggleView = new ToggleSwitchView(on, off,
				60, 24,
				ActiveTheme.Instance.PrimaryBackgroundColor,
				new RGBA_Bytes(220, 220, 220),
				ActiveTheme.Instance.PrimaryAccentColor,
				ActiveTheme.Instance.PrimaryTextColor);
			CheckBox toggleBox = new CheckBox(toggleView);
			toggleBox.Checked = initialState;
			return toggleBox;
		}

		public Button Generate(string normalImageName, string hoverImageName, string pressedImageName = null, string disabledImageName = null)
		{
			if (pressedImageName == null)
			{
				pressedImageName = hoverImageName;
			}

			if (disabledImageName == null)
			{
				disabledImageName = normalImageName;
			}

			ImageBuffer normalImage = StaticData.Instance.LoadIcon(normalImageName);
			ImageBuffer pressedImage = StaticData.Instance.LoadIcon(pressedImageName);
			ImageBuffer hoverImage = StaticData.Instance.LoadIcon(hoverImageName);
			ImageBuffer disabledImage = StaticData.Instance.LoadIcon(disabledImageName);

			if (!ActiveTheme.Instance.IsDarkTheme && InvertImageColor)
			{
				InvertLightness.DoInvertLightness(normalImage);
				InvertLightness.DoInvertLightness(pressedImage);
				InvertLightness.DoInvertLightness(hoverImage);
				InvertLightness.DoInvertLightness(disabledImage);
			}

			if (UserSettings.Instance.IsTouchScreen)
			{
				//normalImage.NewGraphics2D().Line(0, 0, normalImage.Width, normalImage.Height, RGBA_Bytes.Violet);
				RoundedRect rect = new RoundedRect(pressedImage.GetBounds(), 0);
				pressedImage.NewGraphics2D().Render(new Stroke(rect, 3), ActiveTheme.Instance.PrimaryTextColor);
			}

			ButtonViewStates buttonViewWidget = new ButtonViewStates(
				new ImageWidget(normalImage),
				new ImageWidget(hoverImage),
				new ImageWidget(pressedImage),
				new ImageWidget(disabledImage)
			);

			//Create button based on view container widget
			Button imageButton = new Button(0, 0, buttonViewWidget);
			imageButton.Margin = new BorderDouble(0);
			imageButton.Padding = new BorderDouble(0);
			return imageButton;
		}

		public Button Generate(ImageBuffer normalImage, ImageBuffer hoverImage, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null)
		{
			if (pressedImage == null)
			{
				pressedImage = hoverImage;
			}

			if (disabledImage == null)
			{
				disabledImage = normalImage;
			}

			if (!ActiveTheme.Instance.IsDarkTheme && InvertImageColor)
			{
				InvertLightness.DoInvertLightness(normalImage);
				InvertLightness.DoInvertLightness(pressedImage);
				InvertLightness.DoInvertLightness(hoverImage);
				InvertLightness.DoInvertLightness(disabledImage);
			}

			if (UserSettings.Instance.IsTouchScreen)
			{
				//normalImage.NewGraphics2D().Line(0, 0, normalImage.Width, normalImage.Height, RGBA_Bytes.Violet);
				RoundedRect rect = new RoundedRect(pressedImage.GetBounds(), 0);
				pressedImage.NewGraphics2D().Render(new Stroke(rect, 3), ActiveTheme.Instance.PrimaryTextColor);
			}

			ButtonViewStates buttonViewWidget = new ButtonViewStates(
				new ImageWidget(normalImage),
				new ImageWidget(hoverImage),
				new ImageWidget(pressedImage),
				new ImageWidget(disabledImage)
			);

			//Create button based on view container widget
			Button imageButton = new Button(0, 0, buttonViewWidget);
			imageButton.Margin = new BorderDouble(0);
			imageButton.Padding = new BorderDouble(0);
			return imageButton;
		}
	}
}