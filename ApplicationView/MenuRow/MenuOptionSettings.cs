using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class MenuOptionSettings : PopupButton
	{
		public MenuOptionSettings()
			: base(new TextWidget("Options".Localize().ToUpper(), 0, 0, 10, textColor: ActiveTheme.Instance.PrimaryTextColor))
		{
			this.Name = "Options Tab";
			Margin = new BorderDouble(0);
			Padding = new BorderDouble(4);
			VAnchor = VAnchor.ParentCenter;

			this.PopupContent = new ApplicationSettingsWidget(ApplicationController.Instance.Theme.MenuButtonFactory)
			{
				HAnchor = HAnchor.AbsolutePosition,
				VAnchor = VAnchor.FitToChildren,
				Width = 500,
				BackgroundColor = RGBA_Bytes.White
			};
		}
	}
}