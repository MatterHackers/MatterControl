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


namespace MatterHackers.MatterControl
{
	using System.ComponentModel;

	public class View3DConfig : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public bool IsDirty { get; internal set; }

		public bool RenderBed
		{
			get
			{
				string value = UserSettings.Instance.get(UserSettingsKey.GcodeViewerRenderGrid);
				if (value == null)
				{
					return true;
				}
				return (value == "True");
			}
			set
			{
				if (this.RenderBed != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeViewerRenderGrid, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(RenderBed));
				}
			}
		}

		public bool RenderMoves
		{
			get => UserSettings.Instance.get(UserSettingsKey.GcodeViewerRenderMoves) == "True";
			set
			{
				if (this.RenderMoves != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeViewerRenderMoves, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(RenderMoves));
				}
			}
		}

		public bool RenderRetractions
		{
			get => UserSettings.Instance.get(UserSettingsKey.GcodeViewerRenderRetractions) == "True";
			set
			{
				if (this.RenderRetractions != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeViewerRenderRetractions, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(RenderRetractions));
				}
			}
		}

		public bool GCodeModelView
		{
			get => UserSettings.Instance.get(UserSettingsKey.GcodeModelView) != "False";
			set
			{
				if (this.GCodeModelView != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeModelView, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(GCodeModelView));
				}
			}
		}

		public string GCodeLineColorStyle
		{
			get => UserSettings.Instance.get(UserSettingsKey.GCodeLineColorStyle);
			set
			{
				if (this.GCodeLineColorStyle != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GCodeLineColorStyle, value);
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(GCodeLineColorStyle));
				}
			}
		}

		public bool SimulateExtrusion
		{
			get => UserSettings.Instance.get(UserSettingsKey.GcodeViewerSimulateExtrusion) != "False";
			set
			{
				if (this.SimulateExtrusion != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeViewerSimulateExtrusion, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(SimulateExtrusion));
				}
			}
		}

		public bool TransparentExtrusion
		{
			get => UserSettings.Instance.get(UserSettingsKey.GcodeViewerTransparentExtrusion) == "True";
			set
			{
				if (this.TransparentExtrusion != value)
				{
					UserSettings.Instance.set(UserSettingsKey.GcodeViewerTransparentExtrusion, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(TransparentExtrusion));
				}
			}
		}

		public bool SyncToPrint
		{
			get => UserSettings.Instance.get(UserSettingsKey.LayerViewSyncToPrint) != "False";
			set
			{
				if (this.SyncToPrint != value)
				{
					UserSettings.Instance.set(UserSettingsKey.LayerViewSyncToPrint, value.ToString());
					this.IsDirty = true;
					this.OnPropertyChanged(nameof(SyncToPrint));
				}
			}
		}

		private bool _renderBuildVolume;

		public bool RenderBuildVolume
		{
			get => _renderBuildVolume;
			set
			{
				if (_renderBuildVolume != value)
				{
					_renderBuildVolume = value;
					this.OnPropertyChanged(nameof(RenderBuildVolume));
				}
			}
		}

		protected void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}