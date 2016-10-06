﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System;

namespace MatterHackers.MatterControl
{
	public class ExportToFolderFeedbackWindow : SystemWindow
	{
		private int totalParts;
		private int count = 0;
		private FlowLayoutWidget feedback = new FlowLayoutWidget(FlowDirection.TopToBottom);
		private TextWidget nextLine;

		public ExportToFolderFeedbackWindow(int totalParts, string firstPartName, RGBA_Bytes backgroundColor)
			: base(300, 500)
		{
			BackgroundColor = backgroundColor;
			string exportingToFolderTitle = "MatterControl".Localize();
			string exportingToFolderTitleFull = "Exporting to Folder or SD Card".Localize();
			Title = string.Format("{0} - {1}", exportingToFolderTitle, exportingToFolderTitleFull);
			this.totalParts = totalParts;

			feedback.Padding = new BorderDouble(5, 5);
			feedback.AnchorAll();
			AddChild(feedback);

			nextLine = CreateNextLine("");
			feedback.AddChild(nextLine);
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
				nextLine.Text = partDescription;
				nextLine = CreateNextLine("");
				feedback.AddChild(nextLine);
			}
		}

		public void DoneSaving(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				nextLine.Text = "";
				feedback.AddChild(CreateNextLine(string.Format("Filament length = {0} mm", stringEvent.Data)));
			}
		}

		public void UpdatePartStatus(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				nextLine.Text = "   " + stringEvent.Data;
			}
		}
	}
}