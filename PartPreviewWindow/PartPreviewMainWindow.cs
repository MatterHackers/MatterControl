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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewMainWindow : SystemWindow
	{
		private EventHandler unregisterEvents;

		private PartPreviewContent partPreviewWidget;

		public PartPreviewMainWindow(PrintItemWrapper printItem, View3DWidget.AutoRotate autoRotate3DView, View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
			: base(750, 550)
		{
			UseOpenGL = true;
			string partPreviewTitle = "MatterControl".Localize();
			Title = string.Format("{0}: ", partPreviewTitle) + Path.GetFileName(printItem.Name);

			this.Name = "Part Preview Window";

			partPreviewWidget = new PartPreviewContent(printItem);
			partPreviewWidget.Closed += (sender, e) =>
			{
				Close();
			};

			this.AddChild(partPreviewWidget);

			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => this.Invalidate(), ref unregisterEvents);

			Width = 750;
			Height = 550;

			MinimumSize = new Vector2(400, 300);
			ShowAsSystemWindow();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}