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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace MatterHackers.TestRunner
{
	public class TestFramework
	{
		private string imageDirectory;

		public TestFramework(string imageDirectory)
		{
			this.imageDirectory = imageDirectory;
		}

		public enum ClickOrigin { LowerLeft, Center };

		public bool ClickByName(string widgetName, int xOffset = 0, int yOffset = 0, double upDelaySeconds = .2, ClickOrigin origin = ClickOrigin.Center)
		{
			foreach (SystemWindow window in SystemWindow.OpenWindows)
			{
				GuiWidget widgetToClick = window.FindNamedChildRecursive(widgetName);
				if (widgetToClick != null)
				{
					RectangleDouble childBounds = widgetToClick.TransformToParentSpace(window, widgetToClick.LocalBounds);

					if (origin == ClickOrigin.Center)
					{
						xOffset += (int)childBounds.Width / 2;
						yOffset += (int)childBounds.Height / 2;
					}

					Point2D screenPosition = new Point2D((int)childBounds.Left + xOffset, (int)window.Height - (int)(childBounds.Bottom + yOffset));

					screenPosition.x += WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.Location.X;
					screenPosition.y += WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.Location.Y + WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.TitleBarHeight;

					SetCursorPos(screenPosition.x, screenPosition.y);
					NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
					
					Wait(upDelaySeconds);
		
					NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

					return true;
				}
			}
			return false;
		}

		public Point2D CurrentMousPosition()
		{
			Point2D mousePos = new Point2D(System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y);
			return mousePos;
		}

		public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

		public double GetInterpolatedValue(double compleatedRatio0To1, InterpolationType interpolationType)
		{
			switch (interpolationType)
			{
				case InterpolationType.LINEAR:
					return compleatedRatio0To1;

				case InterpolationType.EASE_IN:
					return Math.Pow(compleatedRatio0To1, 3);

				case InterpolationType.EASE_OUT:
					return (Math.Pow(compleatedRatio0To1 - 1, 3) + 1);

				case InterpolationType.EASE_IN_OUT:
					if (compleatedRatio0To1 < .5)
					{
						return Math.Pow(compleatedRatio0To1 * 2, 3) / 2;
					}
					else
					{
						return (Math.Pow(compleatedRatio0To1 * 2 - 2, 3) + 2) / 2;
					}

				default:
					throw new NotImplementedException();
			}
		}

		public void SetCursorPos(int x, int y)
		{
			Vector2 start = new Vector2(CurrentMousPosition().x, CurrentMousPosition().y);
			Vector2 end = new Vector2(x, y);
			Vector2 delta = end - start;
			int steps = 50;
			for (int i = 0; i < steps; i++)
			{
				double ratio = i / (double)steps;
				ratio = GetInterpolatedValue(ratio, InterpolationType.EASE_IN_OUT);
				Vector2 current = start + delta * ratio;
				NativeMethods.SetCursorPos((int)current.x, (int)current.y);
				Thread.Sleep(20);
			}

			NativeMethods.SetCursorPos((int)end.x, (int)end.y);
		}

		public bool ClickImage(string imageName, int xOffset = 0, int yOffset = 0, double upDelaySeconds = .2, ClickOrigin origin = ClickOrigin.Center)
		{
			string pathToImage = Path.Combine(imageDirectory, imageName);

			if (File.Exists(pathToImage))
			{
				ImageBuffer imageToLookFor = new ImageBuffer();

				if (ImageIO.LoadImageData(pathToImage, imageToLookFor))
				{
					if (origin == ClickOrigin.Center)
					{
						xOffset += imageToLookFor.Width / 2;
						yOffset += imageToLookFor.Height / 2;
					}

					ImageBuffer currentScreen = NativeMethods.GetCurrentScreen();

					Vector2 matchPosition;
					double bestMatch;
					if (currentScreen.FindLeastSquaresMatch(imageToLookFor, out matchPosition, out bestMatch, 50))
					{
						// TODO: figure out which window the position is in
						Point2D screenPosition = new Point2D((int)matchPosition.x + xOffset, currentScreen.Height - (int)(matchPosition.y + yOffset));
						SetCursorPos(screenPosition.x, screenPosition.y);
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(upDelaySeconds);
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

						return true;
					}
				}
			}

			return false;
		}

		public ImageBuffer GetCurrentScreen()
		{
			return NativeMethods.GetCurrentScreen();
		}

		public bool ImageExists(ImageBuffer image)
		{
			return false;
		}

		public bool ImageExists(string imageFileName)
		{
			return false;
		}

		public bool NameExists(string widgetName)
		{
			foreach (SystemWindow window in SystemWindow.OpenWindows)
			{
				GuiWidget widgetToClick = window.FindNamedChildRecursive(widgetName);
				if (widgetToClick != null)
				{
					return true;
				}
			}

			return false;
		}

		public void Wait(double timeInSeconds)
		{
			Thread.Sleep((int)(timeInSeconds * 1000));
		}
	}
}