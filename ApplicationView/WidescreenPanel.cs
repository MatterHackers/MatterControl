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
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class WidescreenPanel : FlowLayoutWidget
	{
		private static readonly int ColumnOneFixedWidth = 590;
		private int lastNumberOfVisiblePanels = 0;

		private TextImageButtonFactory advancedControlsButtonFactory = new TextImageButtonFactory();
		private RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

		private FlowLayoutWidget ColumnOne;
		private FlowLayoutWidget ColumnTwo;
		private double Force1PanelWidth = 990 * GuiWidget.DeviceScale;
		private double Force2PanelWidth = 1590 * GuiWidget.DeviceScale;

		private GuiWidget leftBorderLine;

		private EventHandler unregisterEvents;

		public static RootedObjectEventHandler PreChangePanels = new RootedObjectEventHandler();

		public WidescreenPanel()
			: base(FlowDirection.LeftToRight)
		{
			Name = "WidescreenPanel";
			AnchorAll();
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			Padding = new BorderDouble(4);

			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => UiThread.RunOnIdle(ReloadAdvancedControlsPanel), ref unregisterEvents);
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			if (this.VisiblePanelCount != lastNumberOfVisiblePanels)
			{
				RecreateAllPanels();
			}

			base.OnBoundsChanged(e);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		AdvancedControlsPanel advancedControlsPanel;

		private void LoadCompactView()
		{
			ColumnOne.RemoveAllChildren();

			advancedControlsPanel = new AdvancedControlsPanel()
			{
				Name = "For - CompactSlidePanel"
			};

			ColumnOne.AddChild(advancedControlsPanel);
			ColumnOne.AnchorAll();
		}

		private void LoadColumnTwo()
		{
			PopOutManager.SaveIfClosed = false;
			ColumnTwo.CloseAllChildren();
			PopOutManager.SaveIfClosed = true;

			// HACK: Short term restore auto population of ActivePrintItem based on Queue Index0. Long term, persist Scene as needed before running operations that depend on ActivePrintItem
			if (PrinterConnectionAndCommunication.Instance.ActivePrintItem == null)
			{
				PrinterConnectionAndCommunication.Instance.ActivePrintItem = QueueData.Instance.GetPrintItemWrapper(0);
			}

			PartPreviewContent partViewContent = new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, View3DWidget.WindowMode.Embeded, View3DWidget.AutoRotate.Disabled);
			partViewContent.AnchorAll();

			ColumnTwo.AddChild(partViewContent);

			ColumnTwo.AnchorAll();
		}

		private int VisiblePanelCount => (this.Width < Force1PanelWidth) ? 1 : 2;

		public void RecreateAllPanels(object state = null)
		{
			if (Width == 0)
			{
				return;
			}

			int numberOfPanels = this.VisiblePanelCount;

			PreChangePanels.CallEvents(this, null);
			RemovePanelsAndCreateEmpties();

			LoadCompactView();

			// Load ColumnTwo if applicable - i.e. widescreen view
			if (numberOfPanels == 2)
			{
				LoadColumnTwo();
			}

			SetColumnVisibility();

			lastNumberOfVisiblePanels = numberOfPanels;
		}

		private void SetColumnVisibility(object state = null)
		{
			int numberOfPanels = this.VisiblePanelCount;

			switch (numberOfPanels)
			{
				case 1:
					{
						ColumnTwo.Visible = false;
						ColumnOne.Visible = true;

						Padding = new BorderDouble(0);

						leftBorderLine.Visible = false;
					}
					break;

				case 2:
					Padding = new BorderDouble(4);
					ColumnOne.Visible = true;
					ColumnTwo.Visible = true;
					ColumnOne.HAnchor = HAnchor.AbsolutePosition;
					ColumnOne.Width = ColumnOneFixedWidth; // it can hold the slice settings so it needs to be bigger.

					// TODO: Couldn't this just use this.Width so that we could lose the advancedControlsPanel variable?
					ColumnOne.MinimumSize = new Vector2(Math.Max(advancedControlsPanel.Width, ColumnOneFixedWidth), 0); //Ordering here matters - must go after children are added
					break;
			}
		}

		private void RemovePanelsAndCreateEmpties()
		{
			CloseAllChildren();

			ColumnOne = new FlowLayoutWidget(FlowDirection.TopToBottom);
			ColumnTwo = new FlowLayoutWidget(FlowDirection.TopToBottom);

			this.AddChild(ColumnOne);

			leftBorderLine = new GuiWidget()
			{
				VAnchor = VAnchor.ParentBottomTop,
				Width = 15,
				Padding = 5
			};
			leftBorderLine.AddChild(new VerticalLine(alpha:50)
			{
				HAnchor = HAnchor.ParentLeft
			});
			this.AddChild(leftBorderLine);

			this.AddChild(ColumnTwo);
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