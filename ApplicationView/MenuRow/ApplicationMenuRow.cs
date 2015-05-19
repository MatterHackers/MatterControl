using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class ApplicationMenuRow : FlowLayoutWidget
	{
		private static FlowLayoutWidget rightElement;

		public ApplicationMenuRow()
			: base(FlowDirection.LeftToRight)
		{
			LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
			linkButtonFactory.fontSize = 8;

			Button signInLink = linkButtonFactory.Generate("(Sign Out)");
			signInLink.VAnchor = Agg.UI.VAnchor.ParentCenter;
			signInLink.Margin = new BorderDouble(top: 0);

			this.HAnchor = HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			// put in the file menu
			MenuOptionFile menuOptionFile = new MenuOptionFile();
			this.AddChild(menuOptionFile);

#if false
			// put in the view menu
			MenuOptionView menuOptionView = new MenuOptionView();
			this.AddChild(menuOptionView);
#endif

			// put in the help menu
			MenuOptionHelp menuOptionHelp = new MenuOptionHelp();
			this.AddChild(menuOptionHelp);

			// put in a spacer
			this.AddChild(new HorizontalSpacer());

			// make an object that can hold custom content on the right (like the sign in)
			rightElement = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rightElement.Height = 24;
			rightElement.Margin = new BorderDouble(bottom: 4);
			this.AddChild(rightElement);

			this.Padding = new BorderDouble(0, 0, 6, 0);

			if (privateAddRightElement != null)
			{
				privateAddRightElement(rightElement);
			}
		}

		public delegate void AddRightElementDelegate(GuiWidget iconContainer);

		private static event AddRightElementDelegate privateAddRightElement;

		public static event AddRightElementDelegate AddRightElement
		{
			add
			{
				privateAddRightElement += value;
				// and call it right away
				value(rightElement);
			}

			remove
			{
				privateAddRightElement -= value;
			}
		}
	}
}