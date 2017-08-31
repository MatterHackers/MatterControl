/*
Copyright (c) 2016, Lars Brubaker
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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Agg;
using System.Collections.Generic;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class PublishPartToMatterHackers : WizardPage
	{
		string publishMessage = "Publish a copy of this part to MatterHackers.".Localize();
		string publicPublish = "\n\nThis copy will be made availble under the terms of the 'Creative Commons Attribution 4.0 International Public License', click the link below for details.".Localize();

		List<CheckBox> iAgreeCheckbox = new List<CheckBox>();

		public PublishPartToMatterHackers()
		{
			this.WindowTitle = "Publish to the MatterHakers Part Community".Localize();
			this.HeaderText = "Publish your part for everyone!".Localize();

			var scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(scrollWindow);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			scrollWindow.AddChild(container);

			var textColor = ActiveTheme.Instance.PrimaryTextColor;

			container.AddChild(new WrappedTextWidget(publishMessage + publicPublish, textColor: textColor));

			container.AddChild(new HorizontalLine()
			{
				Margin = new BorderDouble(0, 5)
			});

			container.AddChild(new TextWidget("Author".Localize() + ":")
			{
				TextColor = textColor
			});

			var userName = AuthenticationData.Instance.ActiveSessionUsername;
			container.AddChild(new MHTextEditWidget(userName == null ? "" : userName, messageWhenEmptyAndNotSelected: "Author Name")
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5, 5, 0, 0)
			});

			container.AddChild(new TextWidget("Part Name".Localize() + ":")
			{
				TextColor = textColor,
				Margin = new BorderDouble(0, 0, 0, 5)
			});

			string partName = null;
			container.AddChild(new MHTextEditWidget(partName == null ? "" : partName, messageWhenEmptyAndNotSelected: "Part Name")
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5, 5, 0, 0)
			});

			container.AddChild(new TextWidget("Details".Localize() + ":")
			{
				TextColor = textColor,
				Margin = new BorderDouble(0, 0, 0, 5)
			});

			container.AddChild(new MHTextEditWidget("", pixelHeight: 100, multiLine: true, messageWhenEmptyAndNotSelected: "A brief description of this part")
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5, 0, 0, 0)
			});

			var publishButton = textImageButtonFactory.Generate("Publish".Localize());
			publishButton.Name = "PublishPartButton";
			publishButton.Click += (s, e) =>
			{
				// check that the author is set and 'I agree' box is checked
				// do the publish

				// Close the window and update the PrintersImported flag
				UiThread.RunOnIdle(() =>
				{
					WizardWindow.Close();

					ProfileManager.Instance.PrintersImported = true;
					ProfileManager.Instance.Save();
				});
			};

			var haveRightsText = "I have the right to license these files.".Localize();
			contentRow.AddChild(CreateRequiredCheckBox(haveRightsText));

			var agreeText = "I agree to license these files under ".Localize();
			var html = $"<div>{agreeText}<a href='https://creativecommons.org/licenses/by/4.0/'>'CC BY 4.0'.</a></div>";

			contentRow.AddChild(CreateRequiredCheckBox("", new HtmlWidget(html, textColor)));

			publishButton.Visible = true;
			cancelButton.Visible = true;

			this.AddPageAction(publishButton);
		}

		private static FlowLayoutWidget CreateRequiredCheckBox(string agreeText, GuiWidget extra = null)
		{
			var agreeRegion = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};
			var agreeCheckbox = new CheckBox(agreeText)
			{
				VAnchor = VAnchor.Center,
				TextColor = ActiveTheme.Instance.PrimaryTextColor
			};
			agreeRegion.AddChild(agreeCheckbox);
			if(extra != null)
			{
				agreeRegion.AddChild(extra);
			}
			return agreeRegion;
		}
	}
}