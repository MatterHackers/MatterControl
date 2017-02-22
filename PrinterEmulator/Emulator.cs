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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.PrinterEmulator
{
	public class Emulator : IDisposable
	{
		private double bedGoalTemperature = -1;
		private double extruderGoalTemperature = 210;

		// no bed present
		private Random random = new Random();

		// Dictionary of command and response callback
		private Dictionary<string, Func<string, string>> responses = new Dictionary<string, Func<string, string>>();

		private SerialPort serialPort = null;
		private bool shutDown = false;

		public Emulator()
		{
			responses.Add("M105", RandomTemp);
			responses.Add("A", Echo);
			responses.Add("M114", GetPosition);
			responses.Add("N", ParseChecksumLine);
			responses.Add("M115", reportMarlinFirmware);
			responses.Add("M104", SetExtruderTemperature);
			responses.Add("M109", SetExtruderTemperature);
			responses.Add("M140", SetBedTemperature);
			responses.Add("M190", SetBedTemperature);
			responses.Add("G1", SetPosition);
			responses.Add("G4", Wait);
			responses.Add("G0", SetPosition);
			responses.Add("G28", HomePosition);
			responses.Add("G92", ResetPosition);
		}

		public string PortName { get; set; }
		public bool RunSlow { get; set; }

		public void Dispose()
		{
			ShutDown();
		}

		public string Echo(string command)
		{
			return command;
		}

		public string getCommandKey(string command)
		{
			if (command.IndexOf(' ') != -1)
			{
				return command.Substring(0, command.IndexOf(' '));
			}
			return command;
		}

		public string GetCorrectResponse(string command)
		{
			try
			{
				// Remove line returns
				command = command.Split('\n')[0]; // strip of the trailing cr (\n)
				command = ParseChecksumLine(command);
				var commandKey = getCommandKey(command);
				if (responses.ContainsKey(commandKey))
				{
					if (RunSlow)
					{
						// do the right amount of time for the given command
						Thread.Sleep(20);
					}
					return responses[commandKey](command);
				}
				else
				{
					Console.WriteLine($"Command {command} not found");
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}

		public double XPosition { get; private set; }
		public double YPosition { get; private set; }
		public double ZPosition { get; private set; }
		public double EPosition { get; private set; }

		public string GetPosition(string command)
		{
			// position commands look like this: X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00 then an ok on the next line
			return $"X:{XPosition:0.00} Y:{YPosition:0.00} Z:{ZPosition:0.00} E:{EPosition:0.00} Count X: 0.00 Y:0.00 Z:0.00\nok\n";
		}

		// Add response callbacks here
		public string RandomTemp(string command)
		{
			// temp commands look like this: ok T:19.4 /0.0 B:0.0 /0.0 @:0 B@:0
			if (bedGoalTemperature == -1)
			{
				return $"ok T:{(extruderGoalTemperature + random.Next(-2, 2))}\n";
			}
			else
			{
				return $"ok T:{extruderGoalTemperature + random.Next(-2, 2)} B:{bedGoalTemperature + random.Next(-2, 2) }\n";
			}
		}

		public string reportMarlinFirmware(string command)
		{
			return "FIRMWARE_NAME:Marlin V1; Sprinter/grbl mashup for gen6 FIRMWARE_URL:https://github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Framelis v1 EXTRUDER_COUNT:1 UUID:155f84b5-d4d7-46f4-9432-667e6876f37a\nok\n";
		}

		public void ShutDown()
		{
			shutDown = true;
		}

		public bool CDState;
		public int CDChangeCount;
		public bool DsrState;
		public int DsrChangeCount;
		public bool CtsState;
		public int CtsChangeCount;

		public void Startup()
		{
			serialPort = new SerialPort(PortName);

			serialPort.ReadTimeout = 500;
			serialPort.WriteTimeout = 500;
			serialPort.Open();
			string speed = RunSlow ? "slow" : "fast";
			Console.WriteLine($"\n Initializing emulator on port {serialPort.PortName} (Speed: {speed})");

			Task.Run(() =>
			{
				while (!shutDown)
				{
					if (serialPort.CDHolding != CDState)
					{
						CDState = serialPort.CDHolding;
						CDChangeCount++;
					}
					if (serialPort.CtsHolding != CtsState)
					{
						CtsState = serialPort.CtsHolding;
						CtsChangeCount++;
					}
					if (serialPort.DsrHolding != DsrState)
					{
						DsrState = serialPort.DsrHolding;
						DsrChangeCount++;
					}

					Thread.Sleep(10);
				}
			});

			Task.Run(() =>
			{
				while (!shutDown)
				{
					string line = "";
					try
					{
						line = serialPort.ReadLine(); // read a '\n' terminated line
					}
					catch (TimeoutException te)
					{
					}
					catch (Exception)
					{
					}
					if (line.Length > 0)
					{
						Console.WriteLine(line);
						var response = GetCorrectResponse(line);

						Console.WriteLine(response);
						serialPort.Write(response);
					}
				}

				serialPort.Close();
				serialPort.Dispose();
			});
		}

		private string ParseChecksumLine(string command)
		{
			if (command[0] == 'N')
			{
				int spaceIndex = command.IndexOf(' ') + 1;
				int endIndex = command.IndexOf('*');
				return command.Substring(spaceIndex, endIndex - spaceIndex);
			}
			else
			{
				return command;
			}
		}

		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");

		public static double ParseDouble(String source, ref int startIndex)
		{
			Match numberMatch = numberRegex.Match(source, startIndex);
			String returnString = numberMatch.Value;
			startIndex = numberMatch.Index + numberMatch.Length;
			double returnVal;
			double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
			return returnVal;
		}

		public static bool GetFirstNumberAfter(string stringToCheckAfter, string stringWithNumber, ref double readValue, int startIndex = 0)
		{
			int stringPos = stringWithNumber.IndexOf(stringToCheckAfter, startIndex);
			if (stringPos != -1)
			{
				stringPos += stringToCheckAfter.Length;
				readValue = ParseDouble(stringWithNumber, ref stringPos);

				return true;
			}

			return false;
		}

		string ResetPosition(string command)
		{
			return "ok\n";
		}

		string Wait(string command)
		{
			try
			{
				// M140 S210 or M190 S[temp]
				double timeToWait = 0;
				if (!GetFirstNumberAfter("S", command, ref timeToWait))
				{
					if (GetFirstNumberAfter("P", command, ref timeToWait))
					{
						timeToWait /= 1000;
					}
				}

				Thread.Sleep((int)(timeToWait * 1000));
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}

		string HomePosition(string command)
		{
			XPosition = 0;
			YPosition = 0;
			ZPosition = 0;
			return "ok\n";
		}

		string SetPosition(string command)
		{
			double value = 0;
			if (GetFirstNumberAfter("X", command, ref value))
			{
				XPosition = value;
			}
			if (GetFirstNumberAfter("Y", command, ref value))
			{
				YPosition = value;
			}
			if (GetFirstNumberAfter("Z", command, ref value))
			{
				ZPosition = value;
			}
			if (GetFirstNumberAfter("E", command, ref value))
			{
				EPosition = value;
			}

			return "ok\n";
		}

		private string SetBedTemperature(string command)
		{
			try
			{
				// M140 S210 or M190 S[temp]
				var sIndex = command.IndexOf('S') + 1;
				bedGoalTemperature = int.Parse(command.Substring(sIndex));
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}

		private string SetExtruderTemperature(string command)
		{
			try
			{
				// M104 S210 or M109 S[temp]
				var sIndex = command.IndexOf('S') + 1;
				extruderGoalTemperature = int.Parse(command.Substring(sIndex));
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}
	}
}