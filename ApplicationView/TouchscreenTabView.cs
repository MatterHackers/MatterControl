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
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	internal class TouchscreenTabView : TabControl
	{
		public static int firstPanelCurrentTab = 0;
		private static readonly string CompactTabView_CurrentTab = "CompactTabView_CurrentTab";
		private static readonly string CompactTabView_Options_ScrollPosition = "CompactTabView_Options_ScrollPosition";
		private static int lastAdvanceControlsIndex = 0;

		private GuiWidget addedUpdateMark = null;

		private PartPreviewContent partPreviewContainer;
		private QueueDataView queueDataView;
		private TabPage QueueTabPage;

		private bool simpleMode;
		private GuiWidget sliceSettingsWidget;

		private int TabTextSize;
		
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

			this.AddTab(
				"Part Preview Tab", 
				"Preview".Localize().ToUpper(),
				generator: () =>
				{
					partPreviewContainer = new PartPreviewContent(PrinterConnectionAndCommunication.Instance.ActivePrintItem, View3DWidget.WindowMode.Embeded, View3DWidget.AutoRotate.Enabled, View3DWidget.OpenMode.Viewing);
					return partPreviewContainer;
				});

			this.AddTab(
				"Slice Settings Tab",
				"Settings".Localize().ToUpper(),
				generator: () =>
				{
						// sliceSettingsWidget = (ActiveSliceSettings.Instance.PrinterSelected) ? new SliceSettingsWidget() : new NoSettingsWidget();
						if (ActiveSliceSettings.Instance.PrinterSelected)
						{
							sliceSettingsWidget = new SliceSettingsWidget();
						}
						else
						{
							sliceSettingsWidget = new NoSettingsWidget();
						}

						return sliceSettingsWidget;
				});

			BorderDouble horizontalSpacerMargin = new BorderDouble(4, 10);

			this.TabBar.AddChild(new HorizontalLine() { Margin = horizontalSpacerMargin });

			this.AddTab(
				"Controls Tab",
				"Controls".Localize().ToUpper(),
				() => new ManualPrinterControls());

			// TODO: How to handle reload? Create .Reload on LazyTab? Create accessor for tabs["Controls Tab"].Reload()?
			//manualControlsPage = new TabPage(, printerControlsLabel);

			this.TabBar.AddChild(new HorizontalLine() { Margin = horizontalSpacerMargin });

			this.AddTab(
				"Queue Tab",
				"Queue".Localize().ToUpper(),
				() => new QueueDataWidget(queueDataView));

			QueueTabPage = this.GetTabPage("Queue Tab");

			this.AddTab(
				"Library Tab",
				"Library".Localize().ToUpper(),
				() => new PrintLibraryWidget());

			if (!simpleMode)
			{
				this.AddTab(
				"History Tab",
				"History".Localize().ToUpper(),
				() => new PrintHistoryWidget());
			}

			this.TabBar.AddChild(new HorizontalLine() { Margin = horizontalSpacerMargin });

			this.Load += (s, e) =>
			{
				if (!simpleMode && !TouchScreenIsTall)
				{
					foreach (GuiWidget horizontalLine in this.TabBar.Children<HorizontalLine>())
					{
						horizontalLine.Margin = new BorderDouble(4, 5);
					}
				}
			};

			// Make sure we have the right scroll position when we create this view
			// This is not working well enough. So, I disabled it until it can be fixed.
			// Specifically, it has the wronge position on the app restarting.
			/*
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
			} */

			this.AddTab(
				"Options Tab",
				"Options".Localize().ToUpper(),
				() => new PrinterConfigurationScrollWidget());

			this.AddTab(
				"About Tab",
				"About".Localize().ToUpper(),
				() => new AboutWidget());

			NumQueueItemsChanged(this, null);
			SetUpdateNotification(this, null);

			QueueData.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent((s, e) =>
			{
				// ReloadPartPreview
				UiThread.RunOnIdle(() =>
				{
					partPreviewContainer?.Reload(PrinterConnectionAndCommunication.Instance.ActivePrintItem);
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

		public bool TouchScreenIsTall
		{
			get
			{
				foreach(GuiWidget topParent in this.Parents<SystemWindow>())
				{
					if(topParent.Height < 610)
					{
						return false;
					}
				}

				return true;
			}
		}

		private EventHandler unregisterEvents;

		public override void OnClosed(ClosedEventArgs e)
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

						var aboutTabWidget = TabBar.FindNamedChildRecursive("About Tab") as SimpleTextTabWidget;
						addedUpdateMark.OriginRelativeParent = new Vector2(aboutTabWidget.tabTitle.Width + 3, 7 * GuiWidget.DeviceScale);
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
			var controlsTabPage = this.GetTabPage("Controls Tab") as LazyTabPage;
			controlsTabPage.Reload();

			// ReloadConfigurationWidget
			var optionsTabPage = this.GetTabPage("Options Tab") as LazyTabPage;
			optionsTabPage.Reload();

			// ReloadSliceSettingsWidget
			var sliceSettingsTabPage = this.GetTabPage("Slice Settings Tab") as LazyTabPage;
			sliceSettingsTabPage.Reload();

			this.Invalidate();
		}

		private void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
		{
			QueueTabPage.Text = string.Format("{0} ({1})", "Queue".Localize().ToUpper(), QueueData.Instance.ItemCount);
		}

		private void AddTab(string name, string tabTitle, Func<GuiWidget> generator)
		{
			TabPage tabpage = new LazyTabPage(tabTitle) { Generator = generator };

			this.AddTab(
				new SimpleTextTabWidget(
					tabpage,
					name,
					this.TabTextSize,
					ActiveTheme.Instance.SecondaryAccentColor,
					RGBA_Bytes.Transparent,
					ActiveTheme.Instance.TabLabelUnselected,
					RGBA_Bytes.Transparent));
		}
	}
}