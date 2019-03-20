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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class StagedSetupWizard : DialogWindow
	{
		private IEnumerable<ISetupWizard> stages;
		private FlowLayoutWidget leftPanel;
		private DialogPage activePage;
		private GuiWidget rightPanel;
		private bool footerHeightAcquired = false;
		private WizardStageRow activeStageButton;

		public StagedSetupWizard(IEnumerable<ISetupWizard> stages)
		{
			this.stages = stages;

			var activeStage = stages.First();
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
			foreach(var stage in stages)
			{
				var stageWidget = new WizardStageRow(
					$"{i++}. {stage.Title}",
					"",
					stage,
					theme);

				stageWidget.Click += (s, e) =>
				{
					if (activeStageButton != null)
					{
						activeStageButton.Active = false;
					}

					stage.Reset();
					stage.MoveNext();

					activeStage = stage;

					activeStageButton = stageWidget;
					activeStageButton.Active = true;

					this.ChangeToPage(stage.Current);
				};

				leftPanel.AddChild(stageWidget);
			}

			row.AddChild(rightPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			});

			this.Title = activeStage.Title;
			this.Size = new Vector2(1200, 700);
			this.AddChild(row);
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

			activePage = pageToChangeTo;

			pageToChangeTo.DialogWindow = this;
			rightPanel.CloseAllChildren();

			rightPanel.AddChild(pageToChangeTo);

			this.Invalidate();
		}

		public override DialogPage ChangeToPage<PanelType>()
		{
			return base.ChangeToPage<PanelType>();
		}
	}
}