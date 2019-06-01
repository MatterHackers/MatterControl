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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class StagedSetupWindow : DialogWindow
	{
		private FlowLayoutWidget leftPanel;
		private DialogPage activePage;
		private GuiWidget rightPanel;
		private bool footerHeightAcquired = false;
		private ISetupWizard _activeStage;

		private readonly Dictionary<ISetupWizard, WizardStageRow> rowsByStage = new Dictionary<ISetupWizard, WizardStageRow>();

		private IStagedSetupWizard setupWizard;
		private bool closeConfirmed;

		public StagedSetupWindow(IStagedSetupWizard setupWizard)
		{
			this.setupWizard = setupWizard;

			var theme = AppContext.Theme;

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			row.AddChild(leftPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = theme.MinimalShade,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(right: theme.DefaultContainerPadding),
				Padding = theme.DefaultContainerPadding,
				Width = 250
			});

			int i = 1;
			foreach (var stage in setupWizard.Stages.Where(s => s.Visible))
			{
				var stageWidget = new WizardStageRow(
					$"{i++}. {stage.Title}",
					"",
					stage,
					theme)
				{
					TabStop = true,
					ToolTipText = stage.HelpText
				};
				stageWidget.Name = stage.Title + " Row";
				stageWidget.Enabled = stage.Enabled;
				stageWidget.Click += (s, e) =>
				{
					// Only allow leftnav when not running SetupWizard
					if (this.ActiveStage == null)
					{
						this.ActiveStage = stage;
					}
				};

				rowsByStage.Add(stage, stageWidget);

				leftPanel.AddChild(stageWidget);
			}

			row.AddChild(rightPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			});

			this.Title = setupWizard.Title;

			// Multi-part wizard should not try to resize per page
			this.UseChildWindowSize = false;

			this.AddChild(row);
		}

		public bool AutoAdvance => setupWizard.AutoAdvance;

		public ISetupWizard ActiveStage
		{
			get => _activeStage;
			set
			{
				if (_activeStage != null
					&& rowsByStage.TryGetValue(_activeStage, out WizardStageRow activeButton))
				{
					// Mark the leftnav widget as inactive
					activeButton.Active = false;
				}

				// Ensure all or only the active stage is enabled
				foreach (var (stage, widget) in rowsByStage.Select(x => (x.Key, x.Value))) // project to tuple - deconstruct to named items
				{
					bool isActiveStage = stage == value;
					bool noActiveStage = value == null;

					// Enable GuiWidget when no stage is active or when the current stage is active and enabled
					widget.Enabled = stage.Enabled && (noActiveStage || isActiveStage);
				}

				// Shutdown the active Wizard
				_activeStage?.Dispose();

				_activeStage = value;

				if (_activeStage == null)
				{
					return;
				}

				if (rowsByStage.TryGetValue(_activeStage, out WizardStageRow stageButton))
				{
					stageButton.Active = true;
				}

				// Reset enumerator, move to first item
				_activeStage.Reset();
				_activeStage.MoveNext();

				this.ChangeToPage(_activeStage.Current);
			}
		}

		public override void OnClosing(ClosingEventArgs eventArgs)
		{
			if (this.ActiveStage != null
				&& !closeConfirmed)
			{
				// We need to show an interactive dialog to determine if the original Close request should be honored, thus cancel the current Close request
				eventArgs.Cancel = true;

				ConditionalAbort("Are you sure you want to abort calibration?".Localize(), () =>
				{
					closeConfirmed = true;
					this.CloseOnIdle();
				});
			}

			base.OnClosing(eventArgs);
		}

		private void ConditionalAbort(string message, Action exitConfirmedAction)
		{
			UiThread.RunOnIdle(() =>
			{
				StyledMessageBox.ShowMessageBox(
					(exitConfirmed) =>
					{
						// Continue with the original shutdown request if exit confirmed by user
						if (exitConfirmed)
						{
							exitConfirmedAction?.Invoke();
						}
					},
					message,
					"Abort Calibration".Localize(),
					StyledMessageBox.MessageType.YES_NO_WITHOUT_HIGHLIGHT);
			});
		}

		public override void ChangeToPage(DialogPage pageToChangeTo)
		{
			if (!footerHeightAcquired)
			{
				GuiWidget footerRow = pageToChangeTo.FindDescendant("FooterRow");
				var fullHeight = footerRow.Height + footerRow.DeviceMarginAndBorder.Height;
				leftPanel.Margin = leftPanel.Margin.Clone(bottom: fullHeight);
				footerHeightAcquired = true;
			}

			// Close the previously displayed page
			activePage?.Close();

			// Activate the new page
			activePage = pageToChangeTo;

			pageToChangeTo.DialogWindow = this;
			rightPanel.CloseAllChildren();

			rightPanel.AddChild(pageToChangeTo);

			this.Invalidate();
		}

		public override void OnCancel(out bool abortCancel)
		{
			// Cancel actions in this wizard should check to see if the ActiveStage requires confirmation,
			// then proceed to the HomePage, conditionally if confirmation required
			abortCancel = this.ActiveStage?.RequireCancelConfirmation == true;

			if (abortCancel)
			{
				ConditionalAbort("Are you sure you want to abort calibration?".Localize(), () =>
				{
					this.NavigateHome();
				});
			}
		}

		/// <summary>
		/// Navigate to the next incomplete stage or return to the home page
		/// </summary>
		public void NextIncompleteStage()
		{
			ISetupWizard nextStage = setupWizard.Stages.FirstOrDefault(s => s.SetupRequired && s.Enabled);

			if (nextStage != null)
			{
				this.ActiveStage = nextStage;
			}
			else
			{
				this.NavigateHome();
			}
		}

		public override void ClosePage(bool allowAbort = true)
		{
			if (allowAbort)
			{
				this.OnCancel(out bool abortClose);

				if (abortClose)
				{
					return;
				}
			}

			if (this.ActiveStage == null
				|| this.ActiveStage?.ClosePage() == true)
			{
				NavigateHome();
			}
			else
			{
				this.ActiveStage.MoveNext();
				this.ChangeToPage(this.ActiveStage.Current);
			}
		}

		/// <summary>
		/// Unconditionally change back to the home page and exit any active stage
		/// </summary>
		private void NavigateHome()
		{
			if (setupWizard is PrinterCalibrationWizard printerCalibrationWizard)
			{
				printerCalibrationWizard.ReturnedToHomePage = true;
			}

			// Construct and move to the summary/home page
			this.ChangeToPage(setupWizard.HomePageGenerator());

			this.ActiveStage = null;
		}
	}
}