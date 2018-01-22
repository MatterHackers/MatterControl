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

using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	/// <summary>
	/// A toolbar with an optional right anchored element and an ActionBar child to add actions to the bar
	/// </summary>
	public class Toolbar : GuiWidget
	{
		public Toolbar(GuiWidget rightAnchorItem = null)
		{
			this.ActionArea = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};

			base.AddChild(this.ActionArea, 0);
			this.SetRightAnchorItem(rightAnchorItem);
		}

		public FlowLayoutWidget ActionArea { get; }

		public GuiWidget RightAnchorItem { get; private set; }

		public void SetRightAnchorItem(GuiWidget rightAnchorItem)
		{
			if (rightAnchorItem != null)
			{
				rightAnchorItem.HAnchor |= HAnchor.Right;
				base.AddChild(rightAnchorItem);
			}

			this.RightAnchorItem = rightAnchorItem;
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			ActionArea.AddChild(childToAdd, indexInChildrenList);
		}
	}
}