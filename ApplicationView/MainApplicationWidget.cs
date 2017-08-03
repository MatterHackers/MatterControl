/*
Copyright (c) 2015, Lars Brubaker
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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class OemProfileDictionary : Dictionary<string, Dictionary<string, PublicDevice>>
	{
	}

	public class PublicDevice
	{
		public string DeviceToken { get; set; }
		public string ProfileToken { get; set; }
		public string ShortProfileID { get; set; }
		public string CacheKey => this.ShortProfileID + ProfileManager.ProfileExtension;
	}

	public abstract class ApplicationView : GuiWidget
	{
		public abstract void CreateAndAddChildren();
	}

	public class TouchscreenView : ApplicationView
	{
		private FlowLayoutWidget TopContainer;
		private TouchscreenTabView touchscreenTabView;
		private GuiWidget menuSeparator;
		private PrintProgressBar progressBar;
		private bool topIsHidden = false;

		public TouchscreenView()
		{
			CreateAndAddChildren();
			this.AnchorAll();
		}

		public void ToggleTopContainer()
		{
			topIsHidden = !topIsHidden;
			progressBar.WidgetIsExtended = !progressBar.WidgetIsExtended;

			//To do - Animate this (KP)
			this.menuSeparator.Visible = this.TopContainer.Visible;
			this.TopContainer.Visible = !this.TopContainer.Visible;
		}

		public override void CreateAndAddChildren()
		{
			topIsHidden = false;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			TopContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			TopContainer.HAnchor = HAnchor.ParentLeftRight;

			if (!UserSettings.Instance.IsTouchScreen)
			{
#if !__ANDROID__
				// The application menu bar, which is suppressed on Android
				ApplicationMenuRow menuRow = new ApplicationMenuRow();
				TopContainer.AddChild(menuRow);
#endif
			}

			menuSeparator = new GuiWidget();
			menuSeparator.Height = 12;
			menuSeparator.HAnchor = HAnchor.ParentLeftRight;
			menuSeparator.MinimumSize = new Vector2(0, 12);
			menuSeparator.Visible = false;

			TopContainer.AddChild(new ActionBarPlus());

			container.AddChild(TopContainer);

			progressBar = new PrintProgressBar();

			container.AddChild(progressBar);
			container.AddChild(menuSeparator);
			touchscreenTabView = new TouchscreenTabView();

			container.AddChild(touchscreenTabView);
			this.AddChild(container);
		}
	}

	public class DesktopView : ApplicationView
	{
		private WidescreenPanel widescreenPanel;

		public DesktopView()
		{
			CreateAndAddChildren();
			this.AnchorAll();
		}

		public override void CreateAndAddChildren()
		{
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			if (!UserSettings.Instance.IsTouchScreen)
			{
#if !__ANDROID__
				// The application menu bar, which is suppressed on Android
				var menuRow = new ApplicationMenuRow();
				container.AddChild(menuRow);
#endif
			}

			container.AddChild(new HorizontalLine(alpha:50));

			widescreenPanel = new WidescreenPanel();
			container.AddChild(widescreenPanel);

			using (new PerformanceTimer("ReloadAll", "AddChild"))
			{
				this.AddChild(container);
			}
		}
	}

	public class TerminalRedirector
	{
		public void Log(string text)
		{
			PrinterConnection.Instance.PrinterOutputCache.WriteLine(text);
		}
	}

	public class SyncReportType
	{
		public string actionLabel;
		public double percComplete;
	}
}