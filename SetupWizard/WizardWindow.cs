using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class WizardWindow : SystemWindow
	{
		private EventHandler unregisterEvents;

		private static Dictionary<Type, WizardWindow> allWindows = new Dictionary<Type, WizardWindow>();

		private WizardWindow()
			: base(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale)
		{
			this.AlwaysOnTopOfMain = true;
			this.MinimumSize = new Vector2(200, 200);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Padding = new BorderDouble(8);
		}

		public static void Close(Type type)
		{
			WizardWindow existingWindow;

			if (allWindows.TryGetValue(type, out existingWindow))
			{
				existingWindow.Close();
			}
		}

		public static void Show<PanelType>() where PanelType : WizardPage, new()
		{
			WizardWindow wizardWindow = GetWindow(typeof(PanelType));
			var newPanel = wizardWindow.ChangeToPage<PanelType>();
			wizardWindow.Title = newPanel.WindowTitle;

			SetSizeAndShow(wizardWindow, newPanel);
		}

		public static void Show(WizardPage wizardPage)
		{
			WizardWindow wizardWindow = GetWindow(wizardPage.GetType());
			wizardWindow.Title = wizardPage.WindowTitle;

			SetSizeAndShow(wizardWindow, wizardPage);

			wizardWindow.ChangeToPage(wizardPage);
		}

		WizardPage activePage;

		// Allow the WizardPage MinimumSize to override our MinimumSize
		public override Vector2 MinimumSize
		{
			get => activePage?.MinimumSize ?? base.MinimumSize;
			set => base.MinimumSize = value;
		}

		public static void SetSizeAndShow(WizardWindow wizardWindow, WizardPage wizardPage)
		{
			if (wizardPage.WindowSize != Vector2.Zero)
			{
				wizardWindow.Size = wizardPage.WindowSize;
			}

			wizardWindow.ShowAsSystemWindow();
		}

		public static bool IsOpen(Type type)
		{
			WizardWindow wizardWindow;

			if (allWindows.TryGetValue(type, out wizardWindow))
			{
				return true;
			}

			return false;
		}

		private static WizardWindow GetWindow(Type type)
		{
			WizardWindow wizardWindow;

			if (allWindows.TryGetValue(type, out wizardWindow))
			{
				wizardWindow.BringToFront();
			}
			else
			{
				wizardWindow = new WizardWindow();
				wizardWindow.Closed += (s, e) => allWindows.Remove(type);
				allWindows[type] = wizardWindow;
			}

			return wizardWindow;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void ChangeToPage(WizardPage pageToChangeTo)
		{
			activePage = pageToChangeTo;

			pageToChangeTo.WizardWindow = this;
			this.CloseAllChildren();
			this.AddChild(pageToChangeTo);
			this.Invalidate();
		}

		public WizardPage ChangeToPage<PanelType>() where PanelType : WizardPage, new()
		{
			PanelType panel = new PanelType();
			panel.WizardWindow = this;
			ChangeToPage(panel);

			// in the event of a reload all make sure we rebuild the contents correctly
			ApplicationController.Instance.DoneReloadingAll.RegisterEvent((s,e) =>
			{
				// fix the main window background color if needed
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

				// find out where the contents we put in last time are
				int thisIndex = GetChildIndex(panel);
				RemoveAllChildren();
				// make new content with the possibly changed theme
				PanelType newPanel = new PanelType();
				newPanel.WizardWindow = this;
				AddChild(newPanel, thisIndex);
				panel.CloseOnIdle();
				// remember the new content
				panel = newPanel;
			}, ref unregisterEvents);

			return panel;
		}
	}
}