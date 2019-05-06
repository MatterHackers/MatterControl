/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl
{
	public class PrinterViewState
	{
		private const double DefaultSliceSettingsWidth = 450;

		// visibility defaults
		private bool _configurePrinterVisible = UserSettings.Instance.get(UserSettingsKey.ConfigurePrinterTabVisible) == "true";

		private bool _controlsVisible = UserSettings.Instance.get(UserSettingsKey.ControlsTabVisible) != "false";

		private bool _terminalVisible = UserSettings.Instance.get(UserSettingsKey.TerminalTabVisible) == "true";

		private PartViewMode _viewMode = PartViewMode.Model;

		public event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

		public event EventHandler VisibilityChanged;

		public bool SliceSettingsTabPinned
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsTabPinned) == "true";
			set => UserSettings.Instance.set(UserSettingsKey.SliceSettingsTabPinned, value ? "true" : "false");
		}

		public string SliceSettingsTabKey
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsTabIndex);
			set => UserSettings.Instance.set(UserSettingsKey.SliceSettingsTabIndex, value);
		}

		public bool DockWindowFloating { get; internal set; }

		public double SliceSettingsWidth
		{
			get
			{
				double.TryParse(UserSettings.Instance.get(UserSettingsKey.SliceSettingsWidth), out double controlWidth);

				if (controlWidth == 0)
				{
					controlWidth = DefaultSliceSettingsWidth;
				}

				return controlWidth;
			}

			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsWidth, value.ToString());
			}
		}

		public PartViewMode ViewMode
		{
			get => _viewMode;
			set
			{
				if (_viewMode != value)
				{
					// Capture before/after state
					var eventArgs = new ViewModeChangedEventArgs()
					{
						ViewMode = value,
						PreviousMode = _viewMode
					};

					_viewMode = value;

					this.ViewModeChanged?.Invoke(this, eventArgs);
				}
			}
		}

		public bool ConfigurePrinterVisible
		{
			get => _configurePrinterVisible;
			set
			{
				if (_configurePrinterVisible != value)
				{
					if (value)
					{
						this.SliceSettingsTabKey = "Printer";
					}

					_configurePrinterVisible = value;

					UserSettings.Instance.set(UserSettingsKey.ConfigurePrinterTabVisible, _configurePrinterVisible ? "true" : "false");

					VisibilityChanged?.Invoke(this, null);
				}
			}
		}

		public bool ControlsVisible
		{
			get => _controlsVisible;
			set
			{
				if (_controlsVisible != value)
				{
					if (value)
					{
						this.SliceSettingsTabKey = "Controls";
					}

					_controlsVisible = value;

					UserSettings.Instance.set(UserSettingsKey.ControlsTabVisible, _controlsVisible ? "true" : "false");

					VisibilityChanged?.Invoke(this, null);
				}
			}
		}

		public bool TerminalVisible
		{
			get => _terminalVisible;
			set
			{
				if (_terminalVisible != value)
				{
					if (value)
					{
						this.SliceSettingsTabKey = "Terminal";
					}

					_terminalVisible = value;

					UserSettings.Instance.set(UserSettingsKey.TerminalTabVisible, _terminalVisible ? "true" : "false");

					VisibilityChanged?.Invoke(this, null);
				}
			}
		}

		public double SelectedObjectPanelWidth
		{
			get
			{
				if (double.TryParse(UserSettings.Instance.get(UserSettingsKey.SelectedObjectPanelWidth), out double controlWidth))
				{
					return Math.Max(controlWidth, 150);
				}

				return 200;
			}

			set
			{
				var minimumValue = Math.Max(value, 150);
				UserSettings.Instance.set(UserSettingsKey.SelectedObjectPanelWidth, minimumValue.ToString());
			}
		}

		public bool SlicingItem { get; set; }
	}
}