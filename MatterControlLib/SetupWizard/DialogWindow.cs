﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class DialogWindow : SystemWindow
	{
		private DialogPage activePage;
		private EventHandler unregisterEvents;
		private static Dictionary<(Type, int), DialogWindow> allWindows = new Dictionary<(Type, int), DialogWindow>();
		private ThemeConfig theme;

		protected DialogWindow()
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			theme = ApplicationController.Instance.Theme;

			this.AlwaysOnTopOfMain = true;
			this.MinimumSize = new Vector2(200 * GuiWidget.DeviceScale, 200 * GuiWidget.DeviceScale);
			this.SetBackgroundColor();

			var defaultPadding = theme.DefaultContainerPadding;
			this.Padding = new BorderDouble(defaultPadding, defaultPadding, defaultPadding, 2);
		}

		public bool UseChildWindowSize { get; protected set; } = true;

		public static void Close(Type type, int instanceIndex = 0)
		{
			if (allWindows.TryGetValue((type, instanceIndex), out DialogWindow existingWindow))
			{
				existingWindow.Close();
			}
		}

		public static void Show<PanelType>() where PanelType : DialogPage, new()
		{
			DialogWindow wizardWindow = GetWindow(typeof(PanelType));
			var newPanel = wizardWindow.ChangeToPage<PanelType>();
			wizardWindow.Title = newPanel.WindowTitle;
			SetSizeAndShow(wizardWindow, newPanel);
		}

		public static DialogWindow Show(DialogPage wizardPage, int instanceIndex = 0)
		{
			DialogWindow wizardWindow = GetWindow(wizardPage.GetType(), instanceIndex);
			wizardWindow.Title = wizardPage.WindowTitle;

			SetSizeAndShow(wizardWindow, wizardPage);

			wizardWindow.ChangeToPage(wizardPage);

			return wizardWindow;
		}

		public static DialogWindow Show(IStagedSetupWizard setupWizard, bool advanceToIncompleteStage = false)
		{
			var type = setupWizard.GetType();
			var instanceIndex = 0;

			var wizardWindow = new StagedSetupWindow(setupWizard);
			setupWizard.StagedSetupWindow = wizardWindow;
			wizardWindow.Closed += (s, e) => allWindows.Remove((type, instanceIndex));
			allWindows[(type, instanceIndex)] = wizardWindow;

			wizardWindow.Size = setupWizard.WindowSize;

			var homePage = setupWizard.HomePageGenerator();
			SetSizeAndShow(wizardWindow, homePage);

			setupWizard.AutoAdvance = advanceToIncompleteStage;

			if (advanceToIncompleteStage)
			{
				wizardWindow.NextIncompleteStage();
			}
			else
			{
				wizardWindow.ChangeToPage(homePage);
			}

			return wizardWindow;
		}

		public static DialogWindow Show(ISetupWizard setupWizard)
		{
			DialogWindow wizardWindow = GetWindow(setupWizard.GetType());
			wizardWindow.Title = setupWizard.Title;

			if (setupWizard.WindowSize != Vector2.Zero)
			{
				wizardWindow.Size = setupWizard.WindowSize;
			}

			SetSizeAndShow(wizardWindow, setupWizard.Current);

			wizardWindow.ChangeToPage(setupWizard.Current);

			// Set focus to ensure Enter/Esc key handlers are caught
			setupWizard.Current.Focus();

			EventHandler windowClosed = null;
			EventHandler<KeyEventArgs> windowKeyDown = null;

			windowClosed = (s, e) =>
			{
				setupWizard.Dispose();
				wizardWindow.Closed -= windowClosed;
				wizardWindow.KeyDown -= windowKeyDown;
			};

			windowKeyDown = (s, e) =>
			{
				switch (e.KeyCode)
				{
					// Auto-advance to next page on enter key
					case Keys.Enter:
						if (setupWizard.Current is WizardPage currentPage && currentPage.NextButton.Enabled)
						{
							UiThread.RunOnIdle(() => currentPage.NextButton.InvokeClick());
						}

						break;
				}
			};

			wizardWindow.Closed += windowClosed;
			wizardWindow.KeyDown += windowKeyDown;

			return wizardWindow;
		}

		// Allow the WizardPage MinimumSize to override our MinimumSize
		public override Vector2 MinimumSize
		{
			get => activePage?.MinimumSize ?? base.MinimumSize;
			set => base.MinimumSize = value;
		}

		public static void SetSizeAndShow(DialogWindow dialogWindow, DialogPage wizardPage)
		{
			if (dialogWindow.UseChildWindowSize
				&& wizardPage.WindowSize != Vector2.Zero)
			{
				dialogWindow.Size = wizardPage.WindowSize;
			}

			dialogWindow.AlwaysOnTopOfMain = wizardPage.AlwaysOnTopOfMain;

			dialogWindow.ShowAsSystemWindow();
		}

		public static bool IsOpen(Type type, int instanceIndex = 0) => allWindows.ContainsKey((type, instanceIndex));

		private static DialogWindow GetWindow(Type type, int instanceIndex = 0)
		{
			if (allWindows.TryGetValue((type, instanceIndex), out DialogWindow wizardWindow))
			{
				wizardWindow.BringToFront();
				wizardWindow.Focus();
			}
			else
			{
				wizardWindow = new DialogWindow();
				wizardWindow.Closed += (s, e) => allWindows.Remove((type, instanceIndex));
				allWindows[(type, instanceIndex)] = wizardWindow;
			}

			return wizardWindow;
		}

		public virtual void ClosePage(bool allowAbort = true)
		{
			// Close this dialog window
			this.CloseOnIdle();
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public virtual void ChangeToPage(DialogPage pageToChangeTo)
		{
			activePage = pageToChangeTo;

			pageToChangeTo.DialogWindow = this;
			this.CloseChildren();
			this.AddChild(pageToChangeTo);

			this.Invalidate();
		}

		public virtual void OnCancel(out bool abortCancel)
		{
			abortCancel = false;
		}

		public virtual DialogPage ChangeToPage<PanelType>() where PanelType : DialogPage, new()
		{
			var panel = new PanelType
			{
				DialogWindow = this
			};
			this.ChangeToPage(panel);

			// in the event of a reload all make sure we rebuild the contents correctly
			ApplicationController.Instance.DoneReloadingAll.RegisterEvent((s,e) =>
			{
				// Normal theme references are safe to hold in widgets because they're rebuild on ReloadAll. DialogWindow
				// survives and must refresh its reference on reload
				theme = ApplicationController.Instance.Theme;

				// fix the main window background color if needed
				this.SetBackgroundColor();

				// find out where the contents we put in last time are
				int thisIndex = Children.IndexOf(panel);
				this.RemoveChildren();

				// make new content with the possibly changed theme
				var newPanel = new PanelType
				{
					DialogWindow = this
				};
				this.AddChild(newPanel, thisIndex);
				panel.CloseOnIdle();

				// remember the new content
				panel = newPanel;
			}, ref unregisterEvents);

			return panel;
		}

		private void SetBackgroundColor()
		{
			this.BackgroundColor = theme.BackgroundColor;
		}
	}
}