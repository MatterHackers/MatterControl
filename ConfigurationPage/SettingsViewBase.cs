using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class SettingsViewBase : AltGroupBox
	{
		protected FlowLayoutWidget mainContainer;

		protected TextImageButtonFactory buttonFactory;

		public SettingsViewBase(string title, TextImageButtonFactory buttonFactory)
			: base(new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor))
		{
			this.buttonFactory = buttonFactory;
			this.Margin = new BorderDouble(2, 4, 2, 0);

			mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(left: 6)
			};
		}
	}
}