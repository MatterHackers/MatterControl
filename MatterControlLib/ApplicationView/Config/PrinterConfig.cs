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
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System.Linq;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using System.Collections.Generic;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
	public class PrinterConfig : IDisposable
	{
		public event EventHandler Disposed;

		public BedConfig Bed { get; }

		// heating status
		enum HeatingStatus { None, Bed, T0, T1 }
		private HeatingStatus waitingForHeat = HeatingStatus.None;
		private double heatDistance = 0;
		private double heatStart = 0;

        // Hide the default constructor
        private PrinterConfig()
		{
        }

		private PrinterSettingsLayer sceneOverrides = new PrinterSettingsLayer();

		private object locker = new object();
        private ulong undoBufferHashCode = ulong.MaxValue;
        private int sceneChildrenCount = 0;
		private RunningInterval sceneLayerUpdateInterval;

		private PrinterSettingsLayer GetSceneLayer()
        {
            return sceneOverrides;
        }

        public PrinterConfig(PrinterSettings settings)
		{
			this.Settings = settings;

			settings.GetSceneLayer = GetSceneLayer;

			this.Bed = new BedConfig(ApplicationController.Instance.Library.PlatingHistory, this);
			this.ViewState = new PrinterViewState();

			// Initialize bed settings
			this.ReloadBedSettings();
			this.Bed.InvalidateBedMesh();
		}


		public PrinterViewState ViewState { get; }

		public PrinterSettings Settings { get; } = PrinterSettings.Empty;

		public string PrinterName => Settings?.GetValue(SettingsKey.printer_name) ?? "unknown";

		public void SwapToSettings(PrinterSettings printerSettings)
		{
			this.Settings.CopyFrom(printerSettings);

			// TODO: Why reload all after swap? We need to rebuild the printer tab only and should have messaging to do so...
			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
			});
		}

		private void ReloadBedSettings()
		{
			this.Bed.BuildHeight = this.Settings.GetValue<double>(SettingsKey.build_height);
			this.Bed.ViewerVolume = new Vector3(this.Settings.GetValue<Vector2>(SettingsKey.bed_size), this.Bed.BuildHeight);
			this.Bed.BedCenter = this.Settings.GetValue<Vector2>(SettingsKey.print_center);
			this.Bed.BedShape = this.Settings.GetValue<BedShape>(SettingsKey.bed_shape);
		}

		private void Connection_PrintFinished(object s, (string printerName, string itemName) e)
		{
			// clear single use setting on print completion
			foreach (var keyValue in this.Settings.BaseLayer)
			{
				string currentValue = this.Settings.GetValue(keyValue.Key);

				bool valueIsClear = currentValue == "0" | currentValue == "";

				SliceSettingData data = PrinterSettings.SettingsData[keyValue.Key];
				if (data?.ResetAtEndOfPrint == true && !valueIsClear)
				{
					this.Settings.ClearValue(keyValue.Key);
				}
			}
		}

		public void Dispose()
		{
			UiThread.ClearInterval(sceneLayerUpdateInterval);
			sceneLayerUpdateInterval = null;
            
			this.Disposed?.Invoke(this, null);
			this.Disposed = null;
		}
	}
}