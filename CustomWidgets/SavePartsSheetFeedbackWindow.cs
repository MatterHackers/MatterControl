using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System;

namespace MatterHackers.MatterControl
{
	public class SavePartsSheetFeedbackWindow : SystemWindow
	{
		private int totalParts;
		private int count = 0;
		private FlowLayoutWidget feedback = new FlowLayoutWidget(FlowDirection.TopToBottom);

		public SavePartsSheetFeedbackWindow(int totalParts, string firstPartName, RGBA_Bytes backgroundColor)
			: base(300, 500)
		{
			BackgroundColor = backgroundColor;
			string savePartSheetTitle = "MatterControl".Localize();
			string savePartSheetTitleFull = "Saving to Parts Sheet".Localize();
			Title = string.Format("{0} - {1}", savePartSheetTitle, savePartSheetTitleFull);
			this.totalParts = totalParts;

			feedback.Padding = new BorderDouble(5, 5);
			feedback.AnchorAll();
			AddChild(feedback);
		}

		private TextWidget CreateNextLine(string startText)
		{
			TextWidget nextLine = new TextWidget(startText, textColor: ActiveTheme.Instance.PrimaryTextColor);
			nextLine.Margin = new BorderDouble(0, 2);
			nextLine.HAnchor = Agg.UI.HAnchor.ParentLeft;
			nextLine.AutoExpandBoundsToText = true;
			return nextLine;
		}

		public void StartingNextPart(object sender, EventArgs e)
		{
			count++;
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				string partDescription = string.Format("{0}/{1} '{2}'", count, totalParts, stringEvent.Data);
				feedback.AddChild(CreateNextLine(partDescription));
			}
		}

		public void DoneSaving(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				feedback.AddChild(CreateNextLine(string.Format("{0}", stringEvent.Data)));
			}
		}
	}
}