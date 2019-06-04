/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ValidationErrorsPanel : FlowLayoutWidget
	{
		public ValidationErrorsPanel(IEnumerable<ValidationError> errors, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Fit | VAnchor;
			this.BackgroundColor = theme.ResolveColor(theme.BackgroundColor, theme.PrimaryAccentColor.WithAlpha(30));

			var errorImage = AggContext.StaticData.LoadIcon("SettingsGroupError_16x.png", 16, 16, theme.InvertIcons);
			var warningImage = AggContext.StaticData.LoadIcon("SettingsGroupWarning_16x.png", 16, 16, theme.InvertIcons);
			var infoImage = AggContext.StaticData.LoadIcon("StatusInfoTip_16x.png", 16, 16);
			var fixIcon = AggContext.StaticData.LoadIcon("noun_1306.png", 16, 16, theme.InvertIcons);

			foreach (var validationError in errors.OrderByDescending(e => e.ErrorLevel))
			{
				if (validationError.ErrorLevel == ValidationErrorLevel.Warning
					&& UserSettings.Instance.get($"Ignore_{validationError.ID}") == "true")
				{
					continue;
				}

				string errorText, errorDetails;

				var settingsValidationError = validationError as SettingsValidationError;
				if (settingsValidationError != null)
				{
					errorText = string.Format(
						"{0} {1}",
						settingsValidationError.PresentationName,
						validationError.ErrorLevel == ValidationErrorLevel.Error ? "Error".Localize() : "Warning".Localize());

					errorDetails = validationError.Error;
				}
				else
				{
					errorText = validationError.Error;
					errorDetails = validationError.Details ?? "";
				}

				var row = new SettingsRow(errorText, errorDetails, theme, validationError.ErrorLevel == ValidationErrorLevel.Error ? errorImage : warningImage, fullRowSelect: true)
				{
					ArrowDirection = ArrowDirection.Left
				};

				if (validationError.FixAction is NamedAction action)
				{
					// Show fix button
					var button = new LinkLabel(action.Title, theme, pointSize: theme.FontSize10)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(right: 8),
						Enabled = action.IsEnabled == null || action.IsEnabled()
					};

					if (!string.IsNullOrEmpty(action.ID))
					{
						button.Name = action.ID;
					}

					button.Click += (s, e) =>
					{
						// Invoke FixAction
						action.Action.Invoke();
						this.ClosePopup();
					};

					row.AddChild(button);

					row.ActionWidget = button;
					row.FullRowSelect = true;
				}
				else
				{
					// Show info indicator hinting that hover will reveal additional details
					row.AddChild(new IconButton(infoImage, theme)
					{
						Selectable = false
					});
				}

				if (validationError.ErrorLevel == ValidationErrorLevel.Warning)
				{
					var dismissButton = theme.CreateSmallResetButton();
					dismissButton.HAnchor = HAnchor.Absolute;
					dismissButton.Margin = new BorderDouble(right: theme.ButtonHeight / 2 - dismissButton.Width / 2);
					dismissButton.Name = "Dismiss " + validationError.ID;
					dismissButton.ToolTipText = "Dismiss".Localize();
					dismissButton.Click += (sender, e) =>
					{
						UserSettings.Instance.set($"Ignore_{validationError.ID}", "true");
						this.ClosePopup();
					};

					row.AddChild(dismissButton);

					// Enable selection without regard to FullRowSelect
					dismissButton.Selectable = true;
				}

				this.AddChild(row);
			}
		}

		private void ClosePopup()
		{
			if (this.Parents<PopupWidget>().FirstOrDefault() is PopupWidget popupWidget)
			{
				UiThread.RunOnIdle(popupWidget.CloseMenu);
			}
		}
	}
}