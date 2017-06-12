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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl
{
	public class WidescreenPanel : FlowLayoutWidget
	{
		private TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
		private RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

		private EventHandler unregisterEvents;

		public static RootedObjectEventHandler PreChangePanels = new RootedObjectEventHandler();

		public WidescreenPanel()
		{
		}

		public override void Initialize()
		{
			base.Initialize();

			this.AnchorAll();
			this.Name = "WidescreenPanel";

			var library3DViewSplitter = new Splitter()
			{
				Padding = new BorderDouble(4),
				SplitterDistance = 410,
				SplitterWidth = 10,
				SplitterBackground = ApplicationController.Instance.Theme.SplitterBackground
			};
			library3DViewSplitter.AnchorAll();
			AddChild(library3DViewSplitter);

			// put in the left column
			library3DViewSplitter.Panel1.AddChild(new AdvancedControlsPanel()
			{
				Name = "For - CompactSlidePanel"
			});

			// put in the right column
			library3DViewSplitter.Panel2.AddChild(new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, View3DWidget.WindowMode.Embeded, View3DWidget.AutoRotate.Disabled)
			{
				VAnchor = VAnchor.ParentBottom | VAnchor.ParentTop,
				HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight
			});

			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => UiThread.RunOnIdle(ReloadAdvancedControlsPanel), ref unregisterEvents);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void ReloadAdvancedControlsPanel()
		{
			PreChangePanels.CallEvents(this, null);
		}
	}

	public class UpdateNotificationMark : GuiWidget
	{
		public UpdateNotificationMark()
			: base(12, 12)
		{
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Circle(Width / 2, Height / 2, Width / 2, RGBA_Bytes.White);
			graphics2D.Circle(Width / 2, Height / 2, Width / 2 - 1, RGBA_Bytes.Red);
			graphics2D.FillRectangle(Width / 2 - 1, Height / 2 - 3, Width / 2 + 1, Height / 2 + 3, RGBA_Bytes.White);
			base.OnDraw(graphics2D);
		}
	}
}