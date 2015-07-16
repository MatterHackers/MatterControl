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

﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;

namespace MatterHackers.MatterControl
{
	public abstract class ApplicationView : GuiWidget
	{
		public TopContainerWidget TopContainer;

		public abstract void AddElements();

		public abstract void HideTopContainer();

		public abstract void ToggleTopContainer();
	}

	public class CompactApplicationView : ApplicationView
	{
		private CompactTabView compactTabView;
		private QueueDataView queueDataView;
		private GuiWidget menuSeparator;
		private PrintProgressBar progressBar;

		public CompactApplicationView()
		{
			AddElements();
			Initialize();
		}

		private bool topIsHidden = false;

		public override void HideTopContainer()
		{
			if (!topIsHidden)
			{
				progressBar.WidgetIsExtended = false;

				//To do - Animate this (KP)
				this.menuSeparator.Visible = true;
				this.TopContainer.Visible = false;

				topIsHidden = true;
			}
		}

		public override void ToggleTopContainer()
		{
			topIsHidden = !topIsHidden;
			progressBar.WidgetIsExtended = !progressBar.WidgetIsExtended;

			//To do - Animate this (KP)
			this.menuSeparator.Visible = this.TopContainer.Visible;
			this.TopContainer.Visible = !this.TopContainer.Visible;
		}

		public override void AddElements()
		{
			topIsHidden = false;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			TopContainer = new TopContainerWidget();
			TopContainer.HAnchor = HAnchor.ParentLeftRight;

			ApplicationMenuRow menuRow = new ApplicationMenuRow();
#if !__ANDROID__
			TopContainer.AddChild(menuRow);
#endif

			menuSeparator = new GuiWidget();
			menuSeparator.Height = 12;
			menuSeparator.HAnchor = HAnchor.ParentLeftRight;
			menuSeparator.MinimumSize = new Vector2(0, 12);
			menuSeparator.Visible = false;

			queueDataView = new QueueDataView();
			TopContainer.AddChild(new ActionBarPlus(queueDataView));
			TopContainer.SetOriginalHeight();

			container.AddChild(TopContainer);

			progressBar = new PrintProgressBar();

			container.AddChild(progressBar);
			container.AddChild(menuSeparator);
			compactTabView = new CompactTabView(queueDataView);

			BottomOverlay bottomOverlay = new BottomOverlay();
			bottomOverlay.AddChild(compactTabView);

			container.AddChild(bottomOverlay);

			this.AddChild(container);
		}

		private void Initialize()
		{
			this.AnchorAll();
		}
	}

	public class TopContainerWidget : FlowLayoutWidget
	{
		private double originalHeight;

		public TopContainerWidget()
			: base(FlowDirection.TopToBottom)
		{
		}

		public void SetOriginalHeight()
		{
			originalHeight = this.Height;
		}
	}

	internal class BottomOverlay : GuiWidget
	{
		public BottomOverlay()
			: base()
		{
			this.AnchorAll();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			//ApplicationController.Instance.MainView.HideTopContainer();
		}
	}

	public class ResponsiveApplicationView : ApplicationView
	{
		private WidescreenPanel widescreenPanel;

		public ResponsiveApplicationView()
		{
			AddElements();
			Initialize();
		}

		public override void ToggleTopContainer()
		{
		}

		public override void HideTopContainer()
		{
		}

		public override void AddElements()
		{
			Stopwatch timer = Stopwatch.StartNew();
			timer.Start();
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			ApplicationMenuRow menuRow = new ApplicationMenuRow();
			container.AddChild(menuRow);

			GuiWidget menuSeparator = new GuiWidget();
			menuSeparator.BackgroundColor = new RGBA_Bytes(200, 200, 200);
			menuSeparator.Height = 2;
			menuSeparator.HAnchor = HAnchor.ParentLeftRight;
			menuSeparator.Margin = new BorderDouble(3, 6, 3, 3);

			container.AddChild(menuSeparator);
			Console.WriteLine("{0} ms 1".FormatWith(timer.ElapsedMilliseconds)); timer.Restart();

			widescreenPanel = new WidescreenPanel();
			container.AddChild(widescreenPanel);
			Console.WriteLine("{0} ms 2".FormatWith(timer.ElapsedMilliseconds)); timer.Restart();

			Console.WriteLine("{0} ms 3".FormatWith(timer.ElapsedMilliseconds)); timer.Restart();
			this.AddChild(container);
			Console.WriteLine("{0} ms 4".FormatWith(timer.ElapsedMilliseconds)); timer.Restart();
		}

		private void Initialize()
		{
			this.AnchorAll();
		}
	}

	public class ApplicationController
	{
		private static ApplicationController globalInstance;
		public RootedObjectEventHandler ReloadAdvancedControlsPanelTrigger = new RootedObjectEventHandler();
		public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();
		public RootedObjectEventHandler PluginsLoaded = new RootedObjectEventHandler();

		public delegate string GetSessionInfoDelegate();

		public static event GetSessionInfoDelegate privateGetSessionInfo;

		public SlicePresetsWindow EditMaterialPresetsWindow { get; set; }

		public SlicePresetsWindow EditQualityPresetsWindow { get; set; }

		public ApplicationView MainView;

		public event EventHandler ApplicationClosed;

		private event EventHandler unregisterEvents;

		public bool WidescreenMode { get; set; }

		public ApplicationController()
		{
			//Name = "MainSlidePanel";
			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			ReloadAll(null, null);
		}

		public void ReloadAll(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				// give the widget a chance to hear about the close before they are actually colsed.
				WidescreenPanel.PreChangePanels.CallEvents(this, null);
				MainView.CloseAndRemoveAllChildren();
				MainView.AddElements();
				if (DoneReloadingAll != null)
				{
					DoneReloadingAll.CallEvents(null, null);
				}
			});
		}

		public void OnApplicationClosed()
		{
			if (ApplicationClosed != null)
			{
				ApplicationClosed(null, null);
			}
		}

		public static ApplicationController Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationController();
					if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
					{
						globalInstance.MainView = new CompactApplicationView();
					}
					else
					{
						globalInstance.MainView = new ResponsiveApplicationView();
					}
				}
				return globalInstance;
			}
		}

		public void ReloadAdvancedControlsPanel()
		{
			ReloadAdvancedControlsPanelTrigger.CallEvents(this, null);
		}

		public void SwitchToPurchasedLibrary()
		{
			// Switch to the purchased library
			LibraryDataView.CurrentLibraryProvider = LibraryProviderSelector.Instance.PurchasedLibrary;
		}

		public void ChangeCloudSyncStatus(bool userAuthenticated)
		{
			CloudSyncStatusChanged.CallEvents(this, new CloudSyncEventArgs() { IsAuthenticated = userAuthenticated });
		}

		public class CloudSyncEventArgs : EventArgs
		{
			public bool IsAuthenticated { get; set; }
		}
	}
}