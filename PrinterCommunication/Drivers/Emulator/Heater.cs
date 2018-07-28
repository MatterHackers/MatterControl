// Copyright (c) 2015, Lars Brubaker
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
// list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation
// and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.PrinterEmulator
{
	public partial class Emulator
	{
		public class Heater
		{
			public static int BounceAmount = (int)IncrementAmount / 2;
			public static double IncrementAmount = 5.3;
			private static int loopTimeInMs = 100;
			private double _currentTemperature;
			private bool _enabled;
			private double _heatupTimeInSeconds = DefaultHeatUpTime;
			private bool isDirty = true;
			private bool shutdown = false;
			private double targetTemp;

			public Heater(string identifier)
			{
				this.ID = identifier;

				// Maintain temperatures
				Task.Run(() =>
				{
					Thread.CurrentThread.Name = $"EmulatorHeator{identifier}";

					var random = new Random();

					double requiredLoops = 0;
					double incrementPerLoop = 0;

					while (!shutdown)
					{
						if (this.Enabled
							&& targetTemp > 0)
						{
							if (this.isDirty)
							{
								requiredLoops = this.HeatUpTimeInSeconds * 1000 / loopTimeInMs;
								incrementPerLoop = TargetTemperature / requiredLoops;
							}

							if (CurrentTemperature < targetTemp)
							{
								CurrentTemperature += incrementPerLoop;
							}
							else if (CurrentTemperature != targetTemp)
							{
								CurrentTemperature = targetTemp;
							}
						}

						Thread.Sleep(loopTimeInMs);
					}
				});
			}

			public double CurrentTemperature
			{
				get => _currentTemperature;
				set
				{
					_currentTemperature = value;
					isDirty = true;
				}
			}

			public bool Enabled
			{
				get => _enabled;
				set
				{
					if (_enabled != value)
					{
						_enabled = value;
						CurrentTemperature = 0;
					}
				}
			}

			public double EPosition { get; set; }

			public double HeatUpTimeInSeconds
			{
				get => _heatupTimeInSeconds;
				set
				{
					_heatupTimeInSeconds = value;
					isDirty = true;
				}
			}

			public string ID { get; }

			public double TargetTemperature
			{
				get => targetTemp;
				set
				{
					if (targetTemp != value)
					{
						targetTemp = value;
						this.Enabled = this.targetTemp > 0;
					}
				}
			}

			public void Stop()
			{
				shutdown = true;
			}
		}
	}
}