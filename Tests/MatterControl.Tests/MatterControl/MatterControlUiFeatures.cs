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
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI")]
	public class MatterControlUiFeatures
	{
		private static bool saveImagesForDebug = true;

		/// <summary>
		/// Bug #94150618 - Left margin is not applied on GuiWidget
		/// </summary>
		[Test]
		public void TopToBottomContainerAppliesExpectedMarginToToggleView()
		{
			TestContext.CurrentContext.SetCompatibleWorkingDirectory();

			int marginSize = 40;
			int dimensions = 300;

			GuiWidget outerContainer = new GuiWidget(dimensions, dimensions);

			FlowLayoutWidget topToBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			outerContainer.AddChild(topToBottomContainer);

			CheckBox toggleBox = new CheckBox("test");
			toggleBox.HAnchor = HAnchor.Stretch;
			toggleBox.VAnchor = VAnchor.Stretch;
			toggleBox.Margin = new BorderDouble(marginSize);
			toggleBox.BackgroundColor = Color.Red;
			toggleBox.DebugShowBounds = true;

			topToBottomContainer.AddChild(toggleBox);
			topToBottomContainer.AnchorAll();
			topToBottomContainer.PerformLayout();

			outerContainer.DoubleBuffer = true;
			outerContainer.BackBuffer.NewGraphics2D().Clear(Color.White);
			outerContainer.OnDraw(outerContainer.NewGraphics2D());

			// For troubleshooting or visual validation
			OutputImages(outerContainer, outerContainer);

			var bounds = toggleBox.BoundsRelativeToParent;
			Assert.IsTrue(bounds.Left == marginSize, "Left margin is incorrect");
			Assert.IsTrue(bounds.Right == dimensions - marginSize, "Right margin is incorrect");
			Assert.IsTrue(bounds.Top == dimensions - marginSize, "Top margin is incorrect");
			Assert.IsTrue(bounds.Bottom == marginSize, "Bottom margin is incorrect");
		}

		private static void OutputImage(ImageBuffer imageToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(imageToOutput, fileName);
			}
		}

		private static void OutputImage(GuiWidget widgetToOutput, string fileName)
		{
			if (saveImagesForDebug)
			{
				OutputImage(widgetToOutput.BackBuffer, fileName);
			}
		}

		private static void OutputImages(GuiWidget control, GuiWidget test)
		{
			OutputImage(control, "image-control.tga");
			OutputImage(test, "image-test.tga");
		}
	}
}