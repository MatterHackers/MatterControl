/*
Copyright (c) 2017, John Lewin
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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SettingsContext
	{
		private IEnumerable<PrinterSettingsLayer> layerCascade;
		private PrinterSettingsLayer persistenceLayer;

		public SettingsContext(IEnumerable<PrinterSettingsLayer> layerCascade, NamedSettingsLayers viewFilter)
		{
			this.layerCascade = layerCascade;
			this.ViewFilter = viewFilter;

			// The last layer of the layerFilters is the target persistence 
			this.persistenceLayer = layerCascade?.First() ?? ActiveSliceSettings.Instance.UserLayer;
		}

		public NamedSettingsLayers ViewFilter { get; set; }

		public string GetValue(string slicerConfigName)
		{
			return ActiveSliceSettings.Instance.GetValue(slicerConfigName, layerCascade);
		}

		public void SetValue(string slicerConfigName, string settingsValue)
		{
			ActiveSliceSettings.Instance.SetValue(slicerConfigName, settingsValue, persistenceLayer);
		}

		public void SetComPort(string settingsValue)
		{
			ActiveSliceSettings.Instance.Helpers.SetComPort(settingsValue, persistenceLayer);
		}

		public void ClearValue(string slicerConfigName)
		{
			ActiveSliceSettings.Instance.ClearValue(slicerConfigName, persistenceLayer);
		}

		public bool ContainsKey(string slicerConfigName)
		{
			return persistenceLayer.ContainsKey(slicerConfigName);
		}

		internal bool ParseShowString(string enableIfSet)
		{
			return ActiveSliceSettings.Instance.ParseShowString(enableIfSet, layerCascade);
		}
	}
}
