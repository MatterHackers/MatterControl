/*
Copyright (c) 2019, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintLibrary
{

	public class CloneSettingsPage : DialogPage
	{
		public CloneSettingsPage()
		{
			this.WindowTitle = "Import Printer".Localize();
			this.HeaderText = "Import Printer".Localize() + ":";
			this.Name = "Import Printer Window";

			var commonMargin = new BorderDouble(4, 2);

			contentRow.AddChild(new TextWidget("File Path".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			var pathRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			contentRow.AddChild(pathRow);

			TextButton importButton = null;

			var textEditWidget = new MHTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center
			};
			textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
			{
				importButton.Enabled = !string.IsNullOrEmpty(textEditWidget.Text)
					&& File.Exists(textEditWidget.Text);
			};
			pathRow.AddChild(textEditWidget);

			// Must come before pathButton.Click definition
			RadioButton copyAndCalibrateOption = null;

			var openButton = new IconButton(AggContext.StaticData.LoadIcon("fa-folder-open_16.png", 16, 16, theme.InvertIcons), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Margin = new BorderDouble(left: 8)
			};
			openButton.Click += (s, e) =>
			{
				AggContext.FileDialogs.OpenFileDialog(
					new OpenFileDialogParams("settings files|*.ini;*.printer;*.slice"),
					(result) =>
					{
						if (!string.IsNullOrEmpty(result.FileName)
							&& File.Exists(result.FileName))
						{
							textEditWidget.Text = result.FileName;
						}

						importButton.Enabled = !string.IsNullOrEmpty(textEditWidget.Text)
							&& File.Exists(textEditWidget.Text);
					});
			};
			pathRow.AddChild(openButton);

			var exactCloneColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(top: 15)
			};
			contentRow.AddChild(exactCloneColumn);

			var siblingList = new List<GuiWidget>();

			var exactCloneOption = new RadioButton(new RadioButtonViewText("Exact clone".Localize(), theme.TextColor, fontSize: theme.DefaultFontSize))
			{
				HAnchor = HAnchor.Left,
				Margin = commonMargin,
				Cursor = Cursors.Hand,
				Name = "Exact Clone Button",
				Checked = true,
				SiblingRadioButtonList = siblingList
			};
			exactCloneColumn.AddChild(exactCloneOption);
			siblingList.Add(exactCloneOption);

			var exactCloneSummary = new WrappedTextWidget("Copy all settings including hardware calibration".Localize(), pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(left: 30, bottom: 10, top: 4),
			};
			exactCloneColumn.AddChild(exactCloneSummary);

			var copySettingsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			contentRow.AddChild(copySettingsColumn);

			// Create export button for each plugin
			copyAndCalibrateOption = new RadioButton(new RadioButtonViewText("Copy and recalibrate".Localize(), theme.TextColor, fontSize: theme.DefaultFontSize))
			{
				HAnchor = HAnchor.Left,
				Margin = commonMargin,
				Cursor = Cursors.Hand,
				Name = "Copy and Calibrate Button",
				SiblingRadioButtonList = siblingList
			};
			copySettingsColumn.AddChild(copyAndCalibrateOption);
			siblingList.Add(copyAndCalibrateOption);

			string summary = string.Format(
				"{0}\r\n{1}",
				"Copy everything but hardware specific calibration settings".Localize(),
				"Ideal for cloning settings across different physical printers".Localize());

			var copySummary = new WrappedTextWidget(summary, pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(left: 30, bottom: 10, top: 4)
			};
			copySettingsColumn.AddChild(copySummary);

			importButton = theme.CreateDialogButton("Import".Localize());
			importButton.Enabled = false;
			importButton.Name = "Import Button";
			importButton.Click += (s, e) =>
			{
				var filePath = textEditWidget.Text;

				if (ProfileManager.ImportFromExisting(filePath, clearBlackList: copyAndCalibrateOption.Checked))
				{
					string importPrinterSuccessMessage = "You have successfully imported a new printer profile. You can find '{0}' in your list of available printers.".Localize();
					this.DialogWindow.ChangeToPage(
						new ImportSucceededPage(
							importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(filePath))));
				}
				else
				{
					StyledMessageBox.ShowMessageBox(
						string.Format(
							"Oops! Settings file '{0}' did not contain any settings we could import.".Localize(),
							Path.GetFileName(filePath)),
						"Unable to Import".Localize());
				}
			};

			this.AddPageAction(importButton);
		}
	}
}
