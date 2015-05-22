using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class MenuOptionHelp : MenuBase
	{
		public MenuOptionHelp()
			: base("Help".Localize())
		{
		}

		override protected TupleList<string, Func<bool>> GetMenuItems()
		{
			return new TupleList<string, Func<bool>>
            {
                {"Getting Started".Localize(), gettingStarted_Click},
                {"View Help".Localize(), help_Click},
				{"Release Notes".Localize(), notes_Click},
				{"------------------------", nothing_Click},
                {"Report a Bug".Localize(), bug_Click},
                {"Check For Update".Localize(), checkForUpdate_Click},
				{"------------------------", nothing_Click},
                {"About MatterControl".Localize(), about_Click},
            };
		}

		private bool bug_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				ContactFormWindow.Open();
			});
			return true;
		}

		private bool help_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles");
			});
			return true;
		}

		private bool checkForUpdate_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				ApplicationMenuRow.AlwaysShowUpdateStatus = true;
				UpdateControlData.Instance.CheckForUpdateUserRequested();
			});
			return true;
		}

		private bool about_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				AboutWindow.Show();
			});
			return true;
		}

		private bool nothing_Click()
		{
			return true;
		}

		private bool notes_Click()
		{
			UiThread.RunOnIdle((state) =>
				{
					MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes");
				});
			return true;
		}

		private bool gettingStarted_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles/mattercontrol-getting-started");
			});

			return true;
		}
	}
}