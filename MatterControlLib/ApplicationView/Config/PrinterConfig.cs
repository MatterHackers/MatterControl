﻿/*
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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System.Linq;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using System.Collections.Generic;

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

		private PrinterConfig()
		{
			this.Connection = new PrinterConnection(this);
		}

		public bool PrintButtonEnabled()
		{
			var printingOrPause = this.Connection.Printing || this.Connection.Paused;
			var errors = this.Validate();
			return !printingOrPause && !errors.Any(err => err.ErrorLevel == ValidationErrorLevel.Error);
		}

		private PrinterSettingsLayer sceneOverrides = new PrinterSettingsLayer();

		private RunningInterval checkForSceneLayer;
		private object locker = new object();

		private PrinterSettingsLayer GetSceneLayer()
		{
			var scene = Bed?.Scene;
			if (scene != null)
			{
				var foundPartSettings = false;
				var newSceneOverrides = new PrinterSettingsLayer();
				// accumulate all the scene overrides ordered by their names, which is the order they will be in the design tree
				foreach (var partSettingsObject in scene.DescendantsAndSelf().Where(c => c is PartSettingsObject3D && c.Parent?.WorldPrintable() == true).OrderBy(i => i.Name))
				{
					foundPartSettings = true;
					var settings = ((PartSettingsObject3D)partSettingsObject).Overrides;
					foreach (var setting in settings)
					{
						newSceneOverrides[setting.Key] = setting.Value;
					}
				}

				var same = newSceneOverrides.Count == sceneOverrides.Count && !newSceneOverrides.Except(sceneOverrides).Any();
				// if settings count and keys the same, check the value of the settings
				if (same && sceneOverrides.Count > 0)
				{
					// check each setting if it is the same
					foreach (var kvp in newSceneOverrides)
					{
						if (sceneOverrides[kvp.Key] != newSceneOverrides[kvp.Key])
						{
							same = false;
						}
					}
				}

				// if they are different 
				if (!same)
                {
					var settingsToUpdate = new HashSet<string>();
					foreach (var kvp in sceneOverrides)
					{
						settingsToUpdate.Add(kvp.Key);
					}
					foreach (var kvp in newSceneOverrides)
					{
						settingsToUpdate.Add(kvp.Key);
					}

					// store that current set
					sceneOverrides = newSceneOverrides;

					// we are about to update settings but they are stored in the scene not the profile so we don't have to save anything
					var updateList = settingsToUpdate.ToList();
					ProfileManager.SaveOnSingleSettingChange = false;
					for (int i = 0; i < updateList.Count; i++)
					{
						Settings.OnSettingChanged(updateList[i]);
					}
					ProfileManager.SaveOnSingleSettingChange = true;
				}

				if (foundPartSettings)
                {
					lock (locker)
					{
						if (checkForSceneLayer == null)
						{
							checkForSceneLayer = UiThread.SetInterval(() =>
							{
								GetSceneLayer();
							}, .5);
						}
					}
                }
				else if (checkForSceneLayer != null)
                {
					lock (locker)
					{
						// we don't have a scene layer so remove the interval
						UiThread.ClearInterval(checkForSceneLayer);
						checkForSceneLayer = null;
					}
				}

				// return the current set
				return sceneOverrides;
			}

			return null;
		}

		public PrinterConfig(PrinterSettings settings)
		{
			this.Settings = settings;

			settings.GetSceneLayer = GetSceneLayer;

			this.Bed = new BedConfig(ApplicationController.Instance.Library.PlatingHistory, this);
			this.ViewState = new PrinterViewState();

			this.Connection = new PrinterConnection(this);

			// Register listeners
			this.Connection.TemporarilyHoldingTemp += ApplicationController.Instance.Connection_TemporarilyHoldingTemp;
			this.Connection.PrintStarted += ApplicationController.Instance.Connection_PrintStarted;
			this.Connection.PrintFinished += ApplicationController.Instance.Connection_PrintFinished;
			this.Connection.CancelCompleted += ApplicationController.Instance.Connection_PrintCanceled;
			this.Connection.ErrorReported += ApplicationController.Instance.Connection_ErrorReported;
			this.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
			this.Connection.DetailedPrintingStateChanged += Connection_CommunicationStateChanged;
			this.Connection.PrintFinished += Connection_PrintFinished;

			// Initialize bed settings
			this.ReloadBedSettings();
			this.Bed.InvalidateBedMesh();

			this.Settings.SettingChanged += Printer_SettingChanged;
		}


		public PrinterViewState ViewState { get; }

		public PrinterSettings Settings { get; } = PrinterSettings.Empty;

		public string PrinterName => Settings?.GetValue(SettingsKey.printer_name) ?? "unknown";

		[JsonIgnore]
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

							case DetailedPrintingState.HeatingT0:
								return "Waiting for Extruder 1 to Heat to".Localize() + $" {this.Connection.GetTargetHotendTemperature(0)}°C";

							case DetailedPrintingState.HeatingT1:
								return "Waiting for Extruder 2 to Heat to".Localize() + $" {this.Connection.GetTargetHotendTemperature(1)}°C";

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

		private void Connection_CommunicationStateChanged(object s, EventArgs e)
		{
			var printerConnection = this.Connection;

			if (printerConnection.Printing || printerConnection.Paused)
			{
				switch (printerConnection.DetailedPrintingState)
				{
					case DetailedPrintingState.HeatingBed:
						ApplicationController.Instance.Tasks.Execute(
							"Heating Bed".Localize(),
							this,
							(reporter, cancellationToken) =>
							{
								waitingForHeat = HeatingStatus.Bed;

								var progressStatus = new ProgressStatus();
								heatStart = printerConnection.ActualBedTemperature;
								heatDistance = Math.Abs(printerConnection.TargetBedTemperature - heatStart);

								while (heatDistance > 0 
									&& waitingForHeat == HeatingStatus.Bed)
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

					case DetailedPrintingState.HeatingT0:
						ApplicationController.Instance.Tasks.Execute(
							"Heating Nozzle".Localize() + " 1",
							this,
							(reporter, cancellationToken) =>
							{
								waitingForHeat = HeatingStatus.T0;

								var progressStatus = new ProgressStatus();

								heatStart = printerConnection.GetActualHotendTemperature(0);
								heatDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - heatStart);

								while (heatDistance > 0 
									&& waitingForHeat == HeatingStatus.T0)
								{
									var currentDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(0) - printerConnection.GetActualHotendTemperature(0));
									progressStatus.Progress0To1 = (heatDistance - currentDistance) / heatDistance;
									progressStatus.Status = $"Heating Nozzle ({printerConnection.GetActualHotendTemperature(0):0}/{printerConnection.GetTargetHotendTemperature(0):0})";
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

					case DetailedPrintingState.HeatingT1:
						ApplicationController.Instance.Tasks.Execute(
							"Heating Nozzle".Localize() + " 2",
							this,
							(reporter, cancellationToken) =>
							{
								waitingForHeat = HeatingStatus.T1;

								var progressStatus = new ProgressStatus();

								heatStart = printerConnection.GetActualHotendTemperature(1);
								heatDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(1) - heatStart);

								while (heatDistance > 0
									&& waitingForHeat == HeatingStatus.T1)
								{
									var currentDistance = Math.Abs(printerConnection.GetTargetHotendTemperature(1) - printerConnection.GetActualHotendTemperature(1));
									progressStatus.Progress0To1 = (heatDistance - currentDistance) / heatDistance;
									progressStatus.Status = $"Heating Nozzle ({printerConnection.GetActualHotendTemperature(1):0}/{printerConnection.GetTargetHotendTemperature(1):0})";
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
						waitingForHeat = HeatingStatus.None;
						break;
				}
			}
			else
			{
				// turn off any running temp feedback tasks
				waitingForHeat = HeatingStatus.None;
			}
		}

		private void Printer_SettingChanged(object sender, StringEventArgs stringEvent)
		{
			if (stringEvent != null)
			{
				// Fire ReloadAll if changed setting marked with ReloadUiWhenChanged
				if (PrinterSettings.SettingsData.TryGetValue(stringEvent.Data, out SliceSettingData settingsData)
					&& settingsData.UiUpdate != SliceSettingData.UiUpdateRequired.None)
				{
					UiThread.RunOnIdle(() =>
					{
						switch (settingsData.UiUpdate)
						{
							case SliceSettingData.UiUpdateRequired.Application:
								ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
								break;

							case SliceSettingData.UiUpdateRequired.SliceSettings:
								ApplicationController.Instance.ReloadSettings(this);
								break;

							default:
								throw new NotImplementedException();
						}
					});

					// No further processing if changed setting has ReloadUiWhenChanged set
					return;
				}

				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape)
				{
					this.ReloadBedSettings();
					this.Bed.InvalidateBedMesh();
				}
			}
		}

		public void Dispose()
		{
			// Unregister listeners
			this.Settings.SettingChanged -= Printer_SettingChanged;
			this.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;
			this.Connection.DetailedPrintingStateChanged -= Connection_CommunicationStateChanged;
			this.Connection.PrintFinished -= Connection_PrintFinished;
			this.Connection.TemporarilyHoldingTemp -= ApplicationController.Instance.Connection_TemporarilyHoldingTemp;
			this.Connection.PrintFinished -= ApplicationController.Instance.Connection_PrintFinished;
			this.Connection.CancelCompleted -= ApplicationController.Instance.Connection_PrintCanceled;
			this.Connection.ErrorReported -= ApplicationController.Instance.Connection_ErrorReported;

			// Dispose children
			this.Connection.Dispose();

			this.Disposed?.Invoke(this, null);
			this.Disposed = null;
		}
	}
}