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
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI")]
	public class UITests
	{
		private static bool saveImagesForDebug = true;

		void RemoveAllFromQueue(AutomationRunner testRunner)
		{
			Assert.IsTrue(testRunner.ClickByName("Queue... Menu", secondsToWait: 2));
			Assert.IsTrue(testRunner.ClickByName(" Remove All Menu Item", secondsToWait: 2));
		}

		void CloseMatterControl(AutomationRunner testRunner)
		{
			SystemWindow mcWindowLocal = MatterControlApplication.Instance;
			Assert.IsTrue(testRunner.ClickByName("File Menu", secondsToWait: 2));
			Assert.IsTrue(testRunner.ClickByName("Exit Menu Item", secondsToWait: 2));
			testRunner.Wait(.2);
			if (mcWindowLocal.Parent != null)
			{
				mcWindowLocal.CloseOnIdle();
			}
		}
	
		[Test]
		[RequiresSTA]
		public void ClearQueueTests()
		{
			// Run a copy of MatterControl
			MatterControlApplication.AfterFirstDraw = () =>
			{
				Task.Run(() =>
				{
					AutomationRunner testRunner = new AutomationRunner("");

					// Now do the actions specific to this test. (replace this for new tests)
					{
						RemoveAllFromQueue(testRunner);
					}

					CloseMatterControl(testRunner);
				});
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			SystemWindow mcWindow = MatterControlApplication.Instance;
		}

		/// <summary>
		/// Bug #94150618 - Left margin is not applied on GuiWidget
		/// </summary>
		[Test]
		public void TopToBottomContainerAppliesExpectedMarginToToggleView()
		{
			int marginSize = 40;
			int dimensions = 300;

			GuiWidget outerContainer = new GuiWidget(dimensions, dimensions);

			FlowLayoutWidget topToBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			outerContainer.AddChild(topToBottomContainer);

			CheckBox toggleBox = ImageButtonFactory.CreateToggleSwitch(true);
			toggleBox.HAnchor = HAnchor.ParentLeftRight;
			toggleBox.VAnchor = VAnchor.ParentBottomTop;
			toggleBox.Margin = new BorderDouble(marginSize);
			toggleBox.BackgroundColor = RGBA_Bytes.Red;
			toggleBox.DebugShowBounds = true;

			topToBottomContainer.AddChild(toggleBox);
			topToBottomContainer.AnchorAll();
			topToBottomContainer.PerformLayout();

			outerContainer.DoubleBuffer = true;
			outerContainer.BackBuffer.NewGraphics2D().Clear(RGBA_Bytes.White);
			outerContainer.OnDraw(outerContainer.NewGraphics2D());

			// For troubleshooting or visual validation
			OutputImages(outerContainer, outerContainer);

			var bounds = toggleBox.BoundsRelativeToParent;
			Assert.IsTrue(bounds.Left == marginSize, "Left margin is incorrect");
			Assert.IsTrue(bounds.Right == dimensions - marginSize, "Right margin is incorrect");
			Assert.IsTrue(bounds.Top == dimensions - marginSize, "Top margin is incorrect");
			Assert.IsTrue(bounds.Bottom == marginSize, "Bottom margin is incorrect");
		}

		private void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		private void OutputImage(GuiWidget widgetToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				OutputImage(widgetToOutput.BackBuffer, fileName);
			}
		}

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			OutputImage(control, "image-control.tga");
			OutputImage(test, "image-test.tga");
		}
	}
}