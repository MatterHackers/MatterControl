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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	internal class TouchscreenTabView : TabControl
	{
		public static int firstPanelCurrentTab = 0;
		private static readonly string CompactTabView_CurrentTab = "CompactTabView_CurrentTab";
		private static readonly string CompactTabView_Options_ScrollPosition = "CompactTabView_Options_ScrollPosition";
		private static int lastAdvanceControlsIndex = 0;

		private TabPage AboutTabPage;
		private SimpleTextTabWidget aboutTabWidget;
		private GuiWidget addedUpdateMark = null;
		private TabPage optionsPage;
		private TabPage HistoryTabPage;
		private TabPage LibraryTabPage;
		private TabPage manualControlsPage;
		private View3DWidget part3DView;
		private GuiWidget part3DViewContainer;
		private ViewGcodeBasic partGcodeView;
		private GuiWidget partGcodeViewContainer;
		private PartPreviewContent partPreviewContainer;
		private QueueDataView queueDataView;
		private TabPage QueueTabPage;
		private bool simpleMode;
		private GuiWidget sliceSettingsWidget;
		private TabPage sliceTabPage;
		private int TabTextSize;
		private TabPage TerminalTabPage;
		private RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;

		public TouchscreenTabView(QueueDataView queueDataView)
			: base(Orientation.Vertical)
		{
			this.queueDataView = queueDataView;
			this.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
			this.TabBar.Margin = new BorderDouble(4, 0, 0, 0);
			this.TabBar.Padding = new BorderDouble(0, 8);

			this.Margin = new BorderDouble(top: 0);
			this.TabTextSize = 18;

			string simpleModeString = UserSettings.Instance.get("IsSimpleMode");

			if (simpleModeString == null)
			{
				simpleMode = true;
				UserSettings.Instance.set("IsSimpleMode", "true");
			}
			else
			{
				simpleMode = Convert.ToBoolean(simpleModeString);
			}

			QueueTabPage = new TabPage(new QueueDataWidget(queueDataView), LocalizedString.Get("Queue").ToUpper());
			SimpleTextTabWidget queueTabWidget = new SimpleTextTabWidget(QueueTabPage, "Queue Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());

			partPreviewContainer = new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, View3DWidget.WindowMode.Embeded, View3DWidget.AutoRotate.Enabled, View3DWidget.OpenMode.Viewing);

			string partPreviewLabel = LocalizedString.Get("Preview").ToUpper();

			this.AddTab(new SimpleTextTabWidget(new TabPage(partPreviewContainer, partPreviewLabel), "Part Preview Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			string sliceSettingsLabel = LocalizedString.Get("Settings").ToUpper();
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				sliceSettingsWidget = new SliceSettingsWidget();
			}
			else
			{
				sliceSettingsWidget = new NoSettingsWidget();
			}
			sliceTabPage = new TabPage(sliceSettingsWidget, sliceSettingsLabel);

			this.AddTab(new SimpleTextTabWidget(sliceTabPage, "Slice Settings Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			HorizontalLine lineSpacerZero = new HorizontalLine();
			lineSpacerZero.Margin = new BorderDouble(4, 10);
			this.TabBar.AddChild(lineSpacerZero);
#if __ANDROID__
            //Add the tab contents for 'Advanced Controls'
			GuiWidget manualPrinterControls = new ManualControlsWidget();
            string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
            manualControlsPage = new TabPage(manualPrinterControls, printerControlsLabel);
            this.AddTab(new SimpleTextTabWidget(manualControlsPage, "Controls Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
#else
			//Add the tab contents for 'Advanced Controls'
			ScrollableWidget manualPrinterControlsScrollArea = CreateManualControlsTab();
			string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
			manualControlsPage = new TabPage(manualPrinterControlsScrollArea, printerControlsLabel);

			this.AddTab(new SimpleTextTabWidget(manualControlsPage, "Controls Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));
#endif

			HorizontalLine lineSpacerOne = new HorizontalLine();
			lineSpacerOne.Margin = new BorderDouble(4, 10);
			this.TabBar.AddChild(lineSpacerOne);

			this.AddTab(queueTabWidget);

			LibraryTabPage = new TabPage(new PrintLibraryWidget(), LocalizedString.Get("Library").ToUpper());
			this.AddTab(new SimpleTextTabWidget(LibraryTabPage, "Library Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			HistoryTabPage = new TabPage(new PrintHistoryWidget(), LocalizedString.Get("History").ToUpper());
			SimpleTextTabWidget historyTabWidget = new SimpleTextTabWidget(HistoryTabPage, "History Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());

			if (!simpleMode)
			{
				this.AddTab(historyTabWidget);
			}

			HorizontalLine lineSpacerTwo = new HorizontalLine();
			lineSpacerTwo.Margin = new BorderDouble(4, 10);
			this.TabBar.AddChild(lineSpacerTwo);

			string configurationLabel = LocalizedString.Get("Options").ToUpper();
			PrinterConfigurationScrollWidget printerConfigurationWidget = new PrinterConfigurationScrollWidget();

			// Make sure we have the right scroll position when we create this view
			// This is not working well enough. So, I disabled it until it can be fixed.
			// Specifically, it has the wronge position on the app restarting.
			if(false) 
			{
				UiThread.RunOnIdle(() => 
				{
					int scrollPosition = UserSettings.Instance.Fields.GetInt(CompactTabView_Options_ScrollPosition, -100000);
					if (scrollPosition != -100000)
					{
						printerConfigurationWidget.ScrollPosition = new Vector2(0, scrollPosition);
					}
				});

				printerConfigurationWidget.ScrollPositionChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.Fields.SetInt(CompactTabView_Options_ScrollPosition, (int)printerConfigurationWidget.ScrollPosition.y);
				};
			}

			optionsPage = new TabPage(printerConfigurationWidget, configurationLabel);
			this.AddTab(new SimpleTextTabWidget(optionsPage, "Options Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			TerminalTabPage = new TabPage(new TerminalWidget(false), LocalizedString.Get("Console").ToUpper());
			SimpleTextTabWidget terminalTabWidget = new SimpleTextTabWidget(TerminalTabPage, "Terminal Tab", TabTextSize,
													   ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());
			if (!simpleMode)
			{
				this.AddTab(terminalTabWidget);
			}

			AboutTabPage = new TabPage(new AboutWidget(), LocalizedString.Get("About").ToUpper());
			aboutTabWidget = new SimpleTextTabWidget(AboutTabPage, "About Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());
			this.AddTab(aboutTabWidget);

			NumQueueItemsChanged(this, null);
			SetUpdateNotification(this, null);

			QueueData.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);

			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) => ApplicationController.Instance.ReloadAdvancedControlsPanel(), ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent((s, e) =>
			{
				// ReloadPartPreview
				UiThread.RunOnIdle(() =>
				{
					partPreviewContainer.Reload(PrinterConnectionAndCommunication.Instance.ActivePrintItem);
				}, 1);

			}, ref unregisterEvents);

			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent((s, e) => UiThread.RunOnIdle(ReloadAdvancedControls), ref unregisterEvents);
			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(SetUpdateNotification, ref unregisterEvents);

			// Make sure we are on the right tab when we create this view
			{
				string selectedTab = UserSettings.Instance.get(CompactTabView_CurrentTab);
				this.SelectTab(selectedTab);

				TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					string selectedTabName = TabBar.SelectedTabName;
					if (!string.IsNullOrEmpty(selectedTabName))
					{
						UserSettings.Instance.set(CompactTabView_CurrentTab, selectedTabName);
					}
				};
			}
		}
		private event EventHandler unregisterEvents;

		private ScrollableWidget CreateManualControlsTab()
		{
			GuiWidget manualPrinterControls = new ManualControlsWidget();

			ScrollableWidget manualPrinterControlsScrollArea = new ScrollableWidget(true);
			manualPrinterControlsScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			manualPrinterControlsScrollArea.AnchorAll();
			manualPrinterControlsScrollArea.AddChild(manualPrinterControls);

			return manualPrinterControlsScrollArea;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
		}

		public void SetUpdateNotification(object sender, EventArgs widgetEvent)
		{
			switch (UpdateControlData.Instance.UpdateStatus)
			{
				case UpdateControlData.UpdateStatusStates.MayBeAvailable:
				case UpdateControlData.UpdateStatusStates.ReadyToInstall:
				case UpdateControlData.UpdateStatusStates.UpdateAvailable:
				case UpdateControlData.UpdateStatusStates.UpdateDownloading:
					if (addedUpdateMark == null)
					{
						addedUpdateMark = new UpdateNotificationMark();
						addedUpdateMark.OriginRelativeParent = new Vector2(aboutTabWidget.tabTitle.Width + 3, 7);
						aboutTabWidget.AddChild(addedUpdateMark);
					}
					addedUpdateMark.Visible = true;
					break;

				case UpdateControlData.UpdateStatusStates.UpToDate:
				case UpdateControlData.UpdateStatusStates.CheckingForUpdate:
					if (addedUpdateMark != null)
					{
						addedUpdateMark.Visible = false;
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		private void ReloadAdvancedControls()
		{
			// ReloadControlsWidget
			manualControlsPage.RemoveAllChildren();
			ScrollableWidget manualScroll = CreateManualControlsTab();
			manualControlsPage.AddChild(manualScroll);

			// ReloadConfigurationWidget
			optionsPage.RemoveAllChildren();
			optionsPage.AddChild(new PrinterConfigurationScrollWidget());

			// ReloadSliceSettingsWidget
			sliceTabPage.RemoveAllChildren();
			sliceTabPage.AddChild(new SliceSettingsWidget()
			{
				HAnchor = HAnchor.ParentLeftRight, VAnchor = VAnchor.ParentBottomTop
			});

			this.Invalidate();
		}

		private void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
		{
			QueueTabPage.Text = string.Format("{0} ({1})", "Queue".Localize().ToUpper(), QueueData.Instance.Count);
		}
	}
}