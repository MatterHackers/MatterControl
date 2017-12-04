/*
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
using MatterHackers.Agg.Platform;
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
			return CreateToggleSwitch(initialState, ActiveTheme.Instance.PrimaryTextColor);
		}

		public static CheckBox CreateToggleSwitch(bool initialState, Color textColor, bool useStandardLabels = true)
		{
			return CreateToggleSwitch(initialState, textColor, 60 * GuiWidget.DeviceScale, 24 * GuiWidget.DeviceScale, useStandardLabels);
		}

		public static CheckBox CreateToggleSwitch(bool initialState, Color textColor, double pixelWidth, double pixelHeight, bool useStandardLabels = true)
		{
			string on = "On".Localize();
			string off = "Off".Localize();

			if (!useStandardLabels)
			{
				on = "";
				off = "";
			}

			return new CheckBox(
				new ToggleSwitchView(
					on,
					off,
					pixelWidth,
					pixelHeight,
					ActiveTheme.Instance.PrimaryBackgroundColor,
					new Color(220, 220, 220),
					ActiveTheme.Instance.PrimaryAccentColor,
					textColor,
					new Color(textColor, 70)))
			{
				Checked = initialState,
			};
		}

		public Button Generate(ImageBuffer normalImage, ImageBuffer hoverImage, ImageBuffer pressedImage = null, ImageBuffer disabledImage = null)
		{
			if(hoverImage == null)
			{
				hoverImage = normalImage;
			}

			if (pressedImage == null)
			{
				pressedImage = hoverImage;
			}

			if (disabledImage == null)
			{
				disabledImage = normalImage;
			}

			var buttonViewWidget = new ButtonViewStates(
				new ImageWidget(normalImage),
				new ImageWidget(hoverImage),
				new ImageWidget(pressedImage),
				new ImageWidget(disabledImage)
			);

			//Create button based on view container widget
			return new Button(0, 0, buttonViewWidget)
			{
				Margin = new BorderDouble(0),
				Padding = new BorderDouble(0)
			};
		}
	}
}