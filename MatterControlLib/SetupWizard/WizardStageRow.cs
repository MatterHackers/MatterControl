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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class WizardStageRow : SettingsRow
	{
		private ISetupWizard stage;
		private ImageBuffer completedIcon;
		private ImageBuffer recommendedIcon;
		private ImageBuffer disabledCompletedIcon = null;
		private ImageBuffer setupIcon;
		private ImageBuffer disabledSetupIcon = null;
		private ImageBuffer hoverIcon;
		private double iconXOffset;
		private double iconYOffset;
		private bool hasKeyboardFocus;

		public WizardStageRow(string text, string helpText, ISetupWizard stage, ThemeConfig theme)
			: base(text, helpText, theme, fullRowSelect: true)
		{
			this.stage = stage;
			this.Cursor = Cursors.Hand;

			completedIcon = AggContext.StaticData.LoadIcon("fa-check_16.png", 16, 16, theme.InvertIcons).AjustAlpha(0.3);
			recommendedIcon = AggContext.StaticData.LoadIcon("SettingsGroupWarning_16x.png", 16, 16, theme.InvertIcons);
			setupIcon = AggContext.StaticData.LoadIcon("SettingsGroupError_16x.png", 16, 16, theme.InvertIcons);
			hoverIcon = AggContext.StaticData.LoadIcon("expand.png", 16, 16, theme.InvertIcons);
		}

		public bool Active { get; set; }

		public override Cursors Cursor
		{
			get => this.Active ? Cursors.Default : base.Cursor;
			set => base.Cursor = value;
		}

		public override Color BackgroundColor
		{
			get => (Active) ? theme.AccentMimimalOverlay : base.BackgroundColor;
			set => base.BackgroundColor = value;
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			if (completedIcon != null)
			{
				iconXOffset = LocalBounds.Right - completedIcon.Width - theme.DefaultContainerPadding;
				iconYOffset = LocalBounds.YCenter - (completedIcon.Height / 2);
				hoverIcon = hoverIcon.AlphaToPrimaryAccent();
			}

			base.OnBoundsChanged(e);
		}

		public override void OnFocusChanged(EventArgs e)
		{
			hasKeyboardFocus = this.Focused && !mouseInBounds;
			this.Invalidate();

			base.OnFocusChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (!this.Active)
			{
				graphics2D.Render(
					this.StageIcon,
					iconXOffset,
					iconYOffset);

				if (this.TabStop
					&& hasKeyboardFocus)
				{
					graphics2D.Rectangle(this.LocalBounds, theme.EditFieldColors.Focused.BorderColor);
				}
			}
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Enter
				|| keyEvent.KeyCode == Keys.Space)
			{
				UiThread.RunOnIdle(this.InvokeClick);
			}

			base.OnKeyUp(keyEvent);
		}

		private ImageBuffer StageIcon
		{
			get
			{
				ImageBuffer icon;

				if (mouseInBounds
					&& this.Enabled)
				{
					icon = hoverIcon;
				}
				else if (stage.SetupRequired)
				{
					icon = setupIcon;

					if (!this.Enabled)
					{
						if (disabledSetupIcon == null)
						{
							disabledSetupIcon = icon.AjustAlpha(0.2);
						}

						icon = disabledSetupIcon;
					}

					ToolTipText = "Required".Localize();
				}
				else if (!stage.Completed)
				{
					icon = recommendedIcon;

					if (!this.Enabled)
					{
						if (disabledSetupIcon == null)
						{
							disabledSetupIcon = icon.AjustAlpha(0.2);
						}

						icon = disabledSetupIcon;
					}

					ToolTipText = "Optional".Localize();
				}
				else
				{
					icon = completedIcon;

					if (!this.Enabled)
					{
						if (disabledCompletedIcon == null)
						{
							disabledCompletedIcon = icon.AjustAlpha(0.2);
						}

						icon = disabledCompletedIcon;
					}

					ToolTipText = "Completed".Localize();
				}

				return icon;
			}
		}
	}
}