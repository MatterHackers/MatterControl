﻿/*
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
    class CompactTabView : TabControl
    {
        public static int firstPanelCurrentTab = 0;
        static int lastAdvanceControlsIndex = 0;

        TabPage QueueTabPage;
        TabPage LibraryTabPage;
        TabPage HistoryTabPage;
        TabPage AboutTabPage;
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        GuiWidget addedUpdateMark = null;
        QueueDataView queueDataView;
        event EventHandler unregisterEvents;
        GuiWidget part3DViewContainer;
        View3DWidget part3DView;
        GuiWidget partGcodeViewContainer;
        ViewGcodeBasic partGcodeView;
		SimpleTextTabWidget aboutTabWidget;
		SliceSettingsWidget sliceSettingsWidget;

		TabPage sliceTabPage;
		TabPage manualControlsPage;
		TabPage configurationPage;


        int TabTextSize;

        public CompactTabView(QueueDataView queueDataView)
            :base(Orientation.Vertical)
        {
            this.queueDataView = queueDataView;
            this.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
            this.TabBar.Margin = new BorderDouble(0, 0);
            this.TabBar.Padding = new BorderDouble(0, 4);

            this.Margin = new BorderDouble(top: 0);
            this.TabTextSize = 15;

			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(LoadSettingsOnPrinterChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);
			ApplicationController.Instance.ReloadAdvancedControlsPanelTrigger.RegisterEvent(ReloadAdvancedControlsPanelTrigger, ref unregisterEvents);

            PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(onActivePrintItemChanged, ref unregisterEvents);

            QueueTabPage = new TabPage(new QueueDataWidget(queueDataView), LocalizedString.Get("Queue").ToUpper());
            this.AddTab(new SimpleTextTabWidget(QueueTabPage, "Queue Tab", TabTextSize,
                    ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            LibraryTabPage = new TabPage(new PrintLibraryWidget(), LocalizedString.Get("Library").ToUpper());
            this.AddTab(new SimpleTextTabWidget(LibraryTabPage, "Library Tab", TabTextSize,
                    ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            HistoryTabPage = new TabPage(new PrintHistoryWidget(), LocalizedString.Get("History").ToUpper());
            this.AddTab(new SimpleTextTabWidget(HistoryTabPage, "History Tab", TabTextSize,
                    ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            
			GuiWidget manualPrinterControls = new ManualPrinterControls();

            part3DViewContainer = new GuiWidget();
            part3DViewContainer.AnchorAll();

            partGcodeViewContainer = new GuiWidget();
            partGcodeViewContainer.AnchorAll();

            GeneratePartViews();

            string partPreviewLabel = LocalizedString.Get("Part Preview").ToUpper();

            this.AddTab(new SimpleTextTabWidget(new TabPage(part3DViewContainer, partPreviewLabel), "Part Preview Tab", TabTextSize,
                        ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string layerPreviewLabel = LocalizedString.Get("Layer Preview").ToUpper();
            this.AddTab(new SimpleTextTabWidget(new TabPage(partGcodeViewContainer, layerPreviewLabel), "Layer Preview Tab", TabTextSize,
                        ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            //Add the tab contents for 'Advanced Controls'
            string sliceSettingsLabel = LocalizedString.Get("Settings").ToUpper();
            sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
            sliceTabPage = new TabPage(sliceSettingsWidget, sliceSettingsLabel);

            string printerControlsLabel = LocalizedString.Get("Controls").ToUpper();
			manualControlsPage = new TabPage(manualPrinterControls, printerControlsLabel);
            this.AddTab(new SimpleTextTabWidget(manualControlsPage, "Controls Tab", TabTextSize,
            ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

			this.AddTab(new SimpleTextTabWidget(sliceTabPage, "Slice Settings Tab", TabTextSize,
				ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            string configurationLabel = LocalizedString.Get("Configuration").ToUpper();
			PrinterConfigurationScrollWidget printerConfigurationWidget = new PrinterConfigurationScrollWidget();
			configurationPage = new TabPage(printerConfigurationWidget, configurationLabel);
			this.AddTab(new SimpleTextTabWidget(configurationPage, "Configuration Tab", TabTextSize,
                        ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            AboutTabPage = new TabPage(new AboutPage(), LocalizedString.Get("About").ToUpper());
            aboutTabWidget = new SimpleTextTabWidget(AboutTabPage, "About Tab", TabTextSize,
                        ActiveTheme.Instance.SecondaryAccentColor, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());
            this.AddTab(aboutTabWidget);

            NumQueueItemsChanged(this, null);
            SetUpdateNotification(this, null);

            QueueData.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            QueueData.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(SetUpdateNotification, ref unregisterEvents);

            //WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentTab, ref unregisterEvents);

            SelectedTabIndex = firstPanelCurrentTab;
        }

        void onActivePrintItemChanged(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(GeneratePartViews);
        }

		public void ReloadAdvancedControlsPanelTrigger(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(ReloadAdvancedControlsPanel);
		}



		void reloadSliceSettingsWidget()
		{
			//Store the UI state from the current display
			sliceSettingsUiState = new SliceSettingsWidgetUiState(sliceSettingsWidget);

			sliceTabPage.RemoveAllChildren();
			sliceSettingsWidget = new SliceSettingsWidget(sliceSettingsUiState);
			sliceSettingsWidget.AnchorAll();
			sliceTabPage.AddChild(sliceSettingsWidget);
		}

		void reloadControlsWidget()
		{

			GuiWidget manualPrinterControls = new ManualPrinterControls();

			//ScrollableWidget manualPrinterControlsWidget = new ScrollableWidget(true);
			//manualPrinterControlsWidget.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			//manualPrinterControlsWidget.AnchorAll();
			//manualPrinterControlsWidget.AddChild(manualPrinterControls);

			manualControlsPage.RemoveAllChildren();
			manualControlsPage.AddChild(manualPrinterControls);
		}

		void reloadConfigurationWidget()
		{
			configurationPage.RemoveAllChildren();
			configurationPage.AddChild(new PrinterConfigurationScrollWidget());
		}

        void GeneratePartViews(object state = null)
        {
            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;
            part3DView = new View3DWidget(PrinterConnectionAndCommunication.Instance.ActivePrintItem,
                new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                ActiveSliceSettings.Instance.BedCenter,
                ActiveSliceSettings.Instance.BedShape,
                View3DWidget.WindowMode.Embeded,
                View3DWidget.AutoRotate.Enabled);
            part3DView.Margin = new BorderDouble(bottom: 4);
            part3DView.AnchorAll();

            part3DViewContainer.RemoveAllChildren();
            part3DViewContainer.AddChild(part3DView);

            partGcodeView = new ViewGcodeBasic(PrinterConnectionAndCommunication.Instance.ActivePrintItem,
                new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                ActiveSliceSettings.Instance.BedCenter,
                ActiveSliceSettings.Instance.BedShape,
                false);
            partGcodeView.AnchorAll();

            partGcodeViewContainer.RemoveAllChildren();
            partGcodeViewContainer.AddChild(partGcodeView);

        }
        

        static SliceSettingsWidgetUiState sliceSettingsUiState = new SliceSettingsWidgetUiState();
        void SaveCurrentPanelIndex(object sender, EventArgs e)
        {
            sliceSettingsUiState = new SliceSettingsWidgetUiState(sliceSettingsWidget);

            if (this.Children.Count > 0)
            {
                lastAdvanceControlsIndex = this.SelectedTabIndex;
            }
        }

        void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
        {
            string queueStringBeg = LocalizedString.Get("Queue").ToUpper();
            string queueString = string.Format("{1} ({0})", QueueData.Instance.Count, queueStringBeg);
            QueueTabPage.Text = string.Format(queueString, QueueData.Instance.Count);
        }

        void SaveCurrentTab(object sender, EventArgs e)
        {
            firstPanelCurrentTab = SelectedTabIndex;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
        }

		public void ReloadAdvancedControlsPanel(object state)
		{
			UiThread.RunOnIdle(LoadAdvancedControls);
		}

		void LoadAdvancedControls(object state = null)
		{
			reloadControlsWidget();
			reloadConfigurationWidget();
			reloadSliceSettingsWidget();
			this.Invalidate();
		}

		public void LoadSettingsOnPrinterChanged(object sender, EventArgs e)
		{
			ActiveSliceSettings.Instance.LoadAllSettings();
			ApplicationController.Instance.ReloadAdvancedControlsPanel();
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
                        addedUpdateMark = new NotificationWidget();
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
    }
}
