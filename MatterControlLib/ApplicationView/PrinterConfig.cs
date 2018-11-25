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

namespace MatterHackers.MatterControl
{
	using System.IO;
	using System.Threading;
	using MatterHackers.Agg;
	using MatterHackers.DataConverters3D;
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.DataStorage;
	using MatterHackers.MatterControl.PrinterCommunication;
	using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;
	using MatterHackers.MeshVisualizer;
	using MatterHackers.VectorMath;

	public class PrinterConfig : IDisposable
	{
		private MappedSetting[] replaceWithSettingsStrings = null;

		public event EventHandler Disposed;

		public BedConfig Bed { get; }

		public static PrinterConfig EmptyPrinter { get; } = new PrinterConfig();

		public EngineMappingsMatterSlice EngineMappingsMatterSlice { get; }

		// heating status
		private bool waitingForBedHeat = false;
		private bool waitingForExtruderHeat = false;
		private double heatDistance = 0;
		private double heatStart = 0;

		private PrinterConfig()
		{
			this.Connection = new PrinterConnection(this);

			replaceWithSettingsStrings = new MappedSetting[]
			{
				// Have a mapping so that MatterSlice while always use a setting that can be set. (the user cannot set first_layer_bedTemperature in MatterSlice)
				new AsPercentOfReferenceOrDirect(this, SettingsKey.first_layer_speed, "first_layer_speed", "infill_speed", 60),
				new AsPercentOfReferenceOrDirect(this, "external_perimeter_speed","external_perimeter_speed", "perimeter_speed", 60),
				new AsPercentOfReferenceOrDirect(this, "raft_print_speed", "raft_print_speed", "infill_speed", 60),
				new MappedSetting(this, SettingsKey.bed_remove_part_temperature,SettingsKey.bed_remove_part_temperature),
				new MappedSetting(this, "bridge_fan_speed","bridge_fan_speed"),
				new MappedSetting(this, "bridge_speed","bridge_speed"),
				new MappedSetting(this, "air_gap_speed", "air_gap_speed"),
				new MappedSetting(this, "extruder_wipe_temperature","extruder_wipe_temperature"),
				new MappedSetting(this, SettingsKey.filament_diameter,SettingsKey.filament_diameter),
				new MappedSetting(this, "first_layer_bed_temperature", SettingsKey.bed_temperature),
				new MappedSetting(this, "first_layer_temperature", SettingsKey.temperature),
				new MappedSetting(this, SettingsKey.max_fan_speed,"max_fan_speed"),
				new MappedSetting(this, SettingsKey.min_fan_speed,"min_fan_speed"),
				new MappedSetting(this, "retract_length","retract_length"),
				new MappedSetting(this, SettingsKey.temperature,SettingsKey.temperature),
				new MappedSetting(this, "z_offset","z_offset"),
				new MappedSetting(this, SettingsKey.bed_temperature,SettingsKey.bed_temperature),
				new ScaledSingleNumber(this, "infill_speed", "infill_speed", 60),
				new ScaledSingleNumber(this, "min_print_speed", "min_print_speed", 60),
				new ScaledSingleNumber(this, "perimeter_speed","perimeter_speed", 60),
				new ScaledSingleNumber(this, "retract_speed","retract_speed", 60),
				new ScaledSingleNumber(this, "support_material_speed","support_material_speed", 60),
				new ScaledSingleNumber(this, "travel_speed", "travel_speed", 60),
				new ScaledSingleNumber(this, SettingsKey.load_filament_speed, SettingsKey.load_filament_speed, 60),
				new MappedSetting(this, SettingsKey.trim_filament_markdown, SettingsKey.trim_filament_markdown),
				new MappedSetting(this, SettingsKey.insert_filament_markdown2, SettingsKey.insert_filament_markdown2),
				new MappedSetting(this, SettingsKey.running_clean_markdown2, SettingsKey.running_clean_markdown2),
			};

			EngineMappingsMatterSlice = new EngineMappingsMatterSlice(this);
		}

		public PrinterConfig(PrinterSettings settings)
			: this()
		{
			this.Bed = new BedConfig(ApplicationController.Instance.Library.PlatingHistory, this);
			this.ViewState = new PrinterViewState();

			// Register listeners
			this.Connection.TemporarilyHoldingTemp += ApplicationController.Instance.Connection_TemporarilyHoldingTemp;
			this.Connection.ErrorReported += ApplicationController.Instance.Connection_ErrorReported;
			this.Connection.ConnectionSucceeded += Connection_ConnectionSucceeded;
			this.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
			this.Connection.PrintFinished += Connection_PrintFinished;

			this.Settings = settings;
			this.Settings.SettingChanged += Printer_SettingChanged;

			if (!string.IsNullOrEmpty(this.Settings.GetValue(SettingsKey.baud_rate)))
			{
				this.Connection.BaudRate = this.Settings.GetValue<int>(SettingsKey.baud_rate);
			}

			this.Connection.ConnectGCode = this.Settings.GetValue(SettingsKey.connect_gcode);
			this.Connection.CancelGCode = this.Settings.GetValue(SettingsKey.cancel_gcode);
			this.Connection.EnableNetworkPrinting = this.Settings.GetValue<bool>(SettingsKey.enable_network_printing);
			this.Connection.AutoReleaseMotors = this.Settings.GetValue<bool>(SettingsKey.auto_release_motors);
			this.Connection.RecoveryIsEnabled = this.Settings.GetValue<bool>(SettingsKey.recover_is_enabled);
			this.Connection.ExtruderCount = this.Settings.GetValue<int>(SettingsKey.extruder_count);
			this.Connection.SendWithChecksum = this.Settings.GetValue<bool>(SettingsKey.send_with_checksum);
			this.Connection.ReadLineReplacementString = this.Settings.GetValue(SettingsKey.read_regex);
		}

		public string GetGCodePathAndFileName(string fileLocation)
		{
			if (fileLocation.Trim() != "")
			{
				if (Path.GetExtension(fileLocation).ToUpper() == ".GCODE")
				{
					return fileLocation;
				}

				return GCodePath(HashGenerator.ComputeFileSHA1(fileLocation));
			}
			else
			{
				return null;
			}
		}

		public string GCodePath(string fileHashCode)
		{
			long settingsHashCode = this.Settings.GetLongHashCode();

			return Path.Combine(
				ApplicationDataStorage.Instance.GCodeOutputPath,
				$"{fileHashCode}_{ settingsHashCode}.gcode");
		}

		public string ReplaceMacroValues(string gcodeWithMacros)
		{
			foreach (MappedSetting mappedSetting in replaceWithSettingsStrings)
			{
				// first check if this setting is anywhere in the line
				if (gcodeWithMacros.Contains(mappedSetting.CanonicalSettingsName))
				{
					{
						// do the replacement with {} (curly brackets)
						string thingToReplace = "{" + "{0}".FormatWith(mappedSetting.CanonicalSettingsName) + "}";
						gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, mappedSetting.Value);
					}
					// do the replacement with [] (square brackets) Slic3r uses only square brackets
					{
						string thingToReplace = "[" + "{0}".FormatWith(mappedSetting.CanonicalSettingsName) + "]";
						gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, mappedSetting.Value);
					}
				}
			}

			return gcodeWithMacros;
		}

		public PrinterViewState ViewState { get; }

		private PrinterSettings _settings = PrinterSettings.Empty;
		public PrinterSettings Settings
		{
			get => _settings;
			private set
			{
				if (_settings != value)
				{
					_settings = value;
					this.ReloadBedSettings();
					this.Bed.InvalidateBedMesh();
				}
			}
		}

		public PrinterConnection Connection { get; }

		public string PrinterConnectionStatus
		{
			get
			{
				switch (this.Connection.CommunicationState)
				{
					case CommunicationStates.Disconnected:
						return "Not Connected".Localize();

					case CommunicationStates.Disconnecting:
						return "Disconnecting".Localize();

					case CommunicationStates.AttemptingToConnect:
						return "Connecting".Localize() + "...";

					case CommunicationStates.ConnectionLost:
						return "Connection Lost".Localize();

					case CommunicationStates.FailedToConnect:
						return "Unable to Connect".Localize();

					case CommunicationStates.Connected:
						return "Connected".Localize();

					case CommunicationStates.PreparingToPrint:
						return "Preparing To Print".Localize();

					case CommunicationStates.Printing:
						switch (this.Connection.DetailedPrintingState)
						{
							case DetailedPrintingState.HomingAxis:
								return "Homing".Localize();

							case DetailedPrintingState.HeatingBed:
								return "Waiting for Bed to Heat to".Localize() + $" {this.Connection.TargetBedTemperature}°C";

							case DetailedPrintingState.HeatingExtruder:
								return "Waiting for Extruder to Heat to".Localize() + $" {this.Connection.GetTargetHotendTemperature(0)}°C";

							case DetailedPrintingState.Printing:
							default:
								return "Printing".Localize();
						}

					case CommunicationStates.PrintingFromSd:
						return "Printing From SD Card".Localize();

					case CommunicationStates.Paused:
						return "Paused".Localize();

					case CommunicationStates.FinishedPrint:
						return "Finished Print".Localize();

					default:
						throw new NotImplementedException("Make sure every status returns the correct connected state.");
				}
			}
		}

		public void SwapToSettings(PrinterSettings printerSettings)
		{
			_settings = printerSettings;

			// TODO: Why reload all after swap? We need to rebuild the printer tab only and should have messaging to do so...
			ApplicationController.Instance.ReloadAll();
		}

		private void ReloadBedSettings()
		{
			this.Bed.BuildHeight = this.Settings.GetValue<double>(SettingsKey.build_height);
			this.Bed.ViewerVolume = new Vector3(this.Settings.GetValue<Vector2>(SettingsKey.bed_size), this.Bed.BuildHeight);
			this.Bed.BedCenter = this.Settings.GetValue<Vector2>(SettingsKey.print_center);
			this.Bed.BedShape = this.Settings.GetValue<BedShape>(SettingsKey.bed_shape);
		}

		private void Connection_PrintFinished(object s, EventArgs e)
		{
			// clear single use setting on print completion
			foreach (var keyValue in this.Settings.BaseLayer)
			{
				string currentValue = this.Settings.GetValue(keyValue.Key);

				bool valueIsClear = currentValue == "0" | currentValue == "";

				SliceSettingData data = SettingsOrganizer.Instance.GetSettingsData(keyValue.Key);
				if (data?.ResetAtEndOfPrint == true && !valueIsClear)
				{
					this.Settings.ClearValue(keyValue.Key);
				}
			}
		}

		private void Connection_CommunicationStateChanged(object s, EventArgs e)
		{
			var printerConnection = this.Connection;

			if (printerConnection.PrinterIsPrinting || printerConnection.PrinterIsPaused)
			{
				switch (printerConnection.DetailedPrintingState)
				{
					case DetailedPrintingState.HeatingBed:
						ApplicationController.Instance.Tasks.Execute(
							"Heating Bed".Localize(),
							(reporter, cancellationToken) =>
							{
								waitingForBedHeat = true;
								waitingForExtruderHeat = false;

								var progressStatus = new ProgressStatus();
								heatStart = printerConnection.ActualBedTemperature;
								heatDistance = Math.Abs(printerConnection.TargetBedTemperature - heatStart);

								while (heatDistance > 0 && waitingForBedHeat)
								{
									var remainingDistance = Math.Abs(printerConnection.TargetBedTemperature - printerConnection.ActualBedTemperature);
									progressStatus.Status = $"Heating Bed ({printerConnection.ActualBedTemperature:0}/{printerConnection.TargetBedTemperature:0})";
									progressStatus.Progress0To1 = (heatDistance - remainingDistance) / heatDistance;
									reporter.Report(progressStatus);
									Thread.Sleep(10);
								}

								return Task.CompletedTask;
							},
							new RunningTaskOptions()
							{
								ReadOnlyReporting = true
							});
						break;

					case DetailedPrintingState.HeatingExtruder:
						ApplicationController.Instance.Tasks.Execute(
							"Heating Extruder".Localize(),
							(reporter, cancellationToken) =>
							{
								waitingForBedHeat = false;
								waitingForExtruderHeat = true;

								var progressStatus = new ProgressStatus();

								heatStart = printerConnection.GetActualHotendTemperature(0);
								heatDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - heatStart);

								while (heatDistance > 0 && waitingForExtruderHeat)
								{
									var currentDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - printerConnection.GetActualHotendTemperature(0));
									progressStatus.Progress0To1 = (heatDistance - currentDistance) / heatDistance;
									progressStatus.Status = $"Heating Extruder ({printerConnection.GetActualHotendTemperature(0):0}/{printerConnection.GetTargetHotendTemperature(0):0})";
									reporter.Report(progressStatus);
									Thread.Sleep(1000);
								}

								return Task.CompletedTask;
							},
							new RunningTaskOptions()
							{
								ReadOnlyReporting = true
							});
						break;

					case DetailedPrintingState.HomingAxis:
					case DetailedPrintingState.Printing:
					default:
						// clear any existing waiting states
						waitingForBedHeat = false;
						waitingForExtruderHeat = false;
						break;
				}
			}
			else
			{
				// turn of any running temp feedback tasks
				waitingForBedHeat = false;
				waitingForExtruderHeat = false;
			}
		}

		private void Connection_ConnectionSucceeded(object sender, EventArgs e)
		{
			if (sender is PrinterConfig printer)
			{
				ApplicationController.Instance.RunAnyRequiredPrinterSetup(printer, ApplicationController.Instance.Theme);
			}
		}

		private void Printer_SettingChanged(object sender, EventArgs e)
		{
			if (e is StringEventArgs stringEvent)
			{
				// Fire ReloadAll if changed setting marked with ReloadUiWhenChanged
				if (SettingsOrganizer.SettingsData.TryGetValue(stringEvent.Data, out SliceSettingData settingsData)
					&& settingsData.ReloadUiWhenChanged)
				{
					UiThread.RunOnIdle(ApplicationController.Instance.ReloadAll);
				}

				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					this.ReloadBedSettings();
					this.Bed.InvalidateBedMesh();
				}

				// Sync settings changes to printer connection
				switch(stringEvent.Data)
				{
					case SettingsKey.feedrate_ratio:
						this.Connection.FeedRateRatio = this.Settings.GetValue<double>(SettingsKey.feedrate_ratio);
						break;

					case SettingsKey.baud_rate:
						if (!string.IsNullOrEmpty(this.Settings.GetValue(SettingsKey.baud_rate)))
						{
							this.Connection.BaudRate = this.Settings.GetValue<int>(SettingsKey.baud_rate);
						}
						break;

					case SettingsKey.connect_gcode:
						this.Connection.ConnectGCode = this.Settings.GetValue(SettingsKey.connect_gcode);
						break;

					case SettingsKey.cancel_gcode:
						this.Connection.CancelGCode = this.Settings.GetValue(SettingsKey.cancel_gcode);
						break;

					case SettingsKey.enable_network_printing:
						this.Connection.EnableNetworkPrinting = this.Settings.GetValue<bool>(SettingsKey.enable_network_printing);
						break;

					case SettingsKey.auto_release_motors:
						this.Connection.AutoReleaseMotors = this.Settings.GetValue<bool>(SettingsKey.auto_release_motors);
						break;

					case SettingsKey.recover_is_enabled:
						this.Connection.RecoveryIsEnabled = this.Settings.GetValue<bool>(SettingsKey.recover_is_enabled);
						break;

					case SettingsKey.extruder_count:
						this.Connection.ExtruderCount = this.Settings.GetValue<int>(SettingsKey.extruder_count);
						break;

					case SettingsKey.send_with_checksum:
						this.Connection.SendWithChecksum = this.Settings.GetValue<bool>(SettingsKey.send_with_checksum);
						break;

					case SettingsKey.read_regex:
						this.Connection.ReadLineReplacementString = this.Settings.GetValue(SettingsKey.read_regex);
						break;
				}
			}
		}

		public void Dispose()
		{
			// Unregister listeners
			this.Settings.SettingChanged -= Printer_SettingChanged;
			this.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;
			this.Connection.ConnectionSucceeded -= Connection_ConnectionSucceeded;
			this.Connection.PrintFinished -= Connection_PrintFinished;
			this.Connection.TemporarilyHoldingTemp -= ApplicationController.Instance.Connection_TemporarilyHoldingTemp;
			this.Connection.ErrorReported -= ApplicationController.Instance.Connection_ErrorReported;

			replaceWithSettingsStrings = null;

			// Dispose children
			this.Connection.Dispose();
			this.Disposed?.Invoke(this, null);
			this.Disposed = null;
		}
	}
}