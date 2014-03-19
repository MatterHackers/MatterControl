using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl 
{
	public class SaveAsWindow : SystemWindow
	{
		Button saveAsButton;
		Button cancelSaveButton;
		CheckBox addToLibraryOption;
		protected TextImageButtonFactory testButtonFactory = new TextImageButtonFactory ();
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory ();

		public SaveAsWindow()
			: base (360, 420)
		{
			Title = "Save As Window";


			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);


			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;


			
			{
				string saveAsLabel = "Save As";
				TextWidget elementHeader = new TextWidget (saveAsLabel, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild (elementHeader);
				topToBottom.AddChild (headerRow);

			}


			saveAsButton = textImageButtonFactory.Generate("Save As".Localize(), centerText: true);
			saveAsButton.Visible = true;
			saveAsButton.Cursor = Cursors.Hand;



			this.AddChild (topToBottom);


			ShowAsSystemWindow ();
		}

		public void GenericButton()
		{
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;            
			this.textImageButtonFactory.FixedHeight = 24;
			this.textImageButtonFactory.fontSize = 12;

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.HAnchor = HAnchor.ParentLeftRight;

		}
	}
}

