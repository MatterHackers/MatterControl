using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.ConfigurationPage;

namespace MatterHackers.MatterControl
{

	public class MenuOptionSettings : PopupButton
	{
		public MenuOptionSettings()
			: base(new TextWidget("Options".Localize().ToUpper(), 0, 0, 10, textColor: ActiveTheme.Instance.PrimaryTextColor))
		{
			Margin = new BorderDouble(0);
			Padding = new BorderDouble(4);
			VAnchor = VAnchor.ParentCenter;
			OpenOffset = new Vector2(-3, -5);

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