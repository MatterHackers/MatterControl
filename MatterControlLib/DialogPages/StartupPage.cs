/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.Tour;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
    public class StartupPage : DialogPage
	{
		public StartupPage()
			: base("Close".Localize())
		{
			this.WindowTitle = "Setup".Localize() + " " + ApplicationController.Instance.ProductName;
			this.MinimumSize = new Vector2(480 * GuiWidget.DeviceScale, 250 * GuiWidget.DeviceScale);
			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 300 * GuiWidget.DeviceScale);

			contentRow.BackgroundColor = Color.Transparent;

			headerRow.Visible = false;

			contentRow.AddChild(
				new WrappedTextWidget(
					"Welcome to MatterControl! What would you like to do today?".Localize(),
					pointSize: theme.DefaultFontSize,
					textColor: theme.TextColor)
				{
					Margin = new BorderDouble(0, 15)
				});

			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Center | HAnchor.Fit
			};

			var size = (int)(128 * DeviceScale);
			var borderImage = new ImageBuffer(size, size);
			var graphics = borderImage.NewGraphics2D();
			var bounds = borderImage.GetBounds();
			var radius = size * 10 / 100;
			var stroke = DeviceScale * 4;
			var margin = stroke * 2;
			bounds.Inflate((int)(-stroke / 2));
			//graphics.FillRectangle(bounds, theme.BackgroundColor);
			graphics.Render(new Stroke(new RoundedRect(bounds, radius), stroke), 0, 0, theme.TextColor);

			GuiWidget AddButtonText(GuiWidget button, string text)
            {
				var content = new FlowLayoutWidget(FlowDirection.TopToBottom);
				content.AddChild(button);
				content.AddChild(new TextWidget(text,
					pointSize: theme.DefaultFontSize,
					textColor: theme.TextColor)
                {
					HAnchor = HAnchor.Center
				});

				return content;
            }

			var pulseImage = new ImageBuffer(borderImage);
			var pulseWord = StaticData.Instance.LoadIcon("pulse_word.png").GrayToColor(theme.TextColor);
			var wordWidth = bounds.Width * .8;
			graphics = pulseImage.NewGraphics2D();
			graphics.ImageRenderQuality = Graphics2D.TransformQuality.Best;
			graphics.RenderMaxSize(pulseWord, new Vector2(pulseImage.Width / 2 - wordWidth / 2, margin), new Vector2(wordWidth, bounds.Height));
			var pulseLogo = StaticData.Instance.LoadIcon("pulse_logo.png").GrayToColor(theme.TextColor);
			var logoWidth = bounds.Width * .5;
			graphics = pulseImage.NewGraphics2D();
			graphics.ImageRenderQuality = Graphics2D.TransformQuality.Best;
			graphics.RenderMaxSize(pulseLogo,
				new Vector2(pulseImage.Width / 2 - logoWidth / 2, pulseImage.Height * .42),
                new Vector2(logoWidth, bounds.Height));
			ThemedIconButton lastButton = null;
			buttonRow.AddChild(AddButtonText(lastButton = new ThemedIconButton(pulseImage, theme)
            {
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(15)
			}, "Setup New Pulse".Localize()));
			lastButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowPulseModels));
				this.DialogWindow.Close();
			});

			ImageBuffer CreateButtonImage(string iconFile)
			{
				var printerImage = new ImageBuffer(borderImage);
				graphics = printerImage.NewGraphics2D();
				graphics.ImageRenderQuality = Graphics2D.TransformQuality.Best;
				var imageWidth = bounds.Width * .8;
				var printerIcon = StaticData.Instance.LoadIcon(iconFile).CropToVisible().GrayToColor(theme.TextColor);
				var offset = pulseImage.Width / 2 - imageWidth / 2;
				graphics.RenderMaxSize(printerIcon, new Vector2(offset, offset), new Vector2(imageWidth, bounds.Height));

				return printerImage;
			}

			buttonRow.AddChild(AddButtonText(lastButton = new ThemedIconButton(CreateButtonImage("3d_printer.png"), theme)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(15)
			}, "Setup New Printer".Localize()));
			lastButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowMakeModel));
				this.DialogWindow.Close();
			});

			buttonRow.AddChild(AddButtonText(lastButton = new ThemedIconButton(CreateButtonImage("edit_design.png"), theme)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(15),
				Name = "Start New Design"
			}, "Start New Design".Localize()));
			lastButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.MainView.CreateNewDesignTab(true);
				this.DialogWindow.Close();

				// If we have not cancled the show welcome message and there is a window open
				if (UserSettings.Instance.get(UserSettingsKey.ShownWelcomeMessage) != "false"
					&& ApplicationController.Instance.Workspaces.Count > 0)
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show<WelcomePage>();
					});
				}
			});

			contentRow.AddChild(buttonRow);
		}
	}
}