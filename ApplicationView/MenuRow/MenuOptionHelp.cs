using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.AboutPage;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class MenuOptionHelp : MenuBase
	{
		public MenuOptionHelp()
			: base("Help".Localize())
		{
			Name = "Help Menu";
		}

		override protected IEnumerable<MenuItemAction> GetMenuItems()
		{
			return new List<MenuItemAction>
            {
				new MenuItemAction("Getting Started".Localize(), gettingStarted_Click),
                new MenuItemAction("View Help".Localize(), help_Click),
				new MenuItemAction("Release Notes".Localize(), notes_Click),
                new MenuItemAction("User Manual".Localize(), manual_Click),
                new MenuItemAction("------------------------", null),
                new MenuItemAction("Report a Bug".Localize(), bug_Click),
                new MenuItemAction("Check For Update".Localize(), checkForUpdate_Click),
				new MenuItemAction("------------------------", null),
                new MenuItemAction("About MatterControl".Localize(), about_Click),
            };
		}

		private void bug_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				ContactFormWindow.Open();
			});
		}

		private void help_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles");
			});
		}

		private void checkForUpdate_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				ApplicationMenuRow.AlwaysShowUpdateStatus = true;
				UpdateControlData.Instance.CheckForUpdateUserRequested();
				CheckForUpdateWindow.Show();
			});
		}

		private void about_Click()
		{
			UiThread.RunOnIdle(AboutWindow.Show);
		}

		private void notes_Click()
		{
			UiThread.RunOnIdle(() =>
				{
					MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes");
				});
		}

		private void gettingStarted_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles/mattercontrol-getting-started");
			});
		}

        private void manual_Click()
        {
            UiThread.RunOnIdle(() =>
            {
                MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com");
            });
        }
    }
}