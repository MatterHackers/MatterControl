/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class QuickMenuNameValue
	{
		public string MenuName;
		public string Value;
	}

	public class SliceSettingData
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public enum DataEditTypes
		{
			BOUNDS,
			CHECK_BOX,
			COLOR,
			COM_PORT,
			DOUBLE,
			DOUBLE_OR_PERCENT,
			EXTRUDER_LIST,
			HARDWARE_PRESENT,
			INT,
			INT_OR_MM,
			IP_LIST,
			LIST,
			MARKDOWN_TEXT,
			MULTI_LINE_TEXT,
			OFFSET,
			OFFSET3,
			POSITIVE_DOUBLE,
			READONLY_STRING,
			SLICE_ENGINE,
			STRING,
			VECTOR2,
			VECTOR3,
			VECTOR4,
			WIDE_STRING,
		};

		public string SlicerConfigName { get; set; }

		public string PresentationName { get; set; }

		public string ShowIfSet { get; set; }

		public string EnableIfSet { get; set; }

		public string DefaultValue { get; set; }

		public DataEditTypes DataEditType { get; set; }

		public string HelpText { get; set; } = "";

		public string Units { get; set; } = "";

		public string ListValues { get; set; } = "";

		public bool ShowAsOverride { get; set; } = true;

		public List<QuickMenuNameValue> QuickMenuSettings = new List<QuickMenuNameValue>();

		public List<Dictionary<string, string>> SetSettingsOnChange = new List<Dictionary<string, string>>();

		public bool ResetAtEndOfPrint { get; set; } = false;

		public bool RebuildGCodeOnChange { get; set; } = true;

		public Func<int, string> PerToolName { get; set; } = null;

		public enum UiUpdateRequired { None, SliceSettings, Application };

		public UiUpdateRequired UiUpdate { get; set; } = UiUpdateRequired.None;

		public SettingsLayout.Group OrganizerGroup { get; set; }

		public ValueConverter Converter { get; set; }

		/// <summary>
		/// The display minimum display detail that must be set for this setting to be visible
		/// </summary>
		public enum DisplayDetailRequired { Simple, Intermediate, Advanced }

		public DisplayDetailRequired RequiredDisplayDetail { get; internal set; } = DisplayDetailRequired.Intermediate;
	}
}