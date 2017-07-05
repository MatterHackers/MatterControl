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
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.PrinterEmulator
{
	public partial class Emulator : IDisposable
	{
		public int CDChangeCount;
		public bool CDState;
		public int CtsChangeCount;
		public bool CtsState;
		public int DsrChangeCount;
		public bool DsrState;
		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");

		private long commandIndex = 1;

		private int recievedCount = 0;

		// Instance reference allows test to access the most recently initialized emulator
		public static Emulator Instance { get; private set; }

		// Dictionary of command and response callback
		private Dictionary<string, Func<string, string>> responses;

		private bool shutDown = false;

		public Emulator()
		{
			Emulator.Instance = this;

			responses = new Dictionary<string, Func<string, string>>()
			{
				{ "A", Echo },
				{ "G0", SetPosition },
				{ "G1", SetPosition },
				{ "G28", HomePosition },
				{ "G4", Wait },
				{ "G92", ResetPosition },
				{ "M104", SetExtruderTemperature },
				{ "M105", ReturnTemp },
				{ "M106", SetFan },
				{ "M109", SetExtruderTemperature },
				{ "M110", SetLineCount },
				{ "M114", GetPosition },
				{ "M115", ReportMarlinFirmware },
				{ "M140", SetBedTemperature },
				{ "M190", SetBedTemperature },
				{ "M20", ListSdCard },
				{ "M21", InitSdCard },
				{ "N", ParseChecksumLine },
			};
		}

		public event EventHandler ExtruderTemperatureChanged;

		public event EventHandler FanSpeedChanged;

		public double BedCurrentTemperature { get; private set; } = 26;
		public double BedGoalTemperature { get; private set; } = -1;
		public double EPosition { get; private set; }

		public double ExtruderCurrentTemperature { get; private set; } = 27;
		public double ExtruderGoalTemperature { get; private set; } = 0;
		public double FanSpeed { get; private set; }

		public string PortName { get; set; }

		public bool RunSlow { get; set; } = false;

		public bool SimulateLineErrors { get; set; } = false;
		public double XPosition { get; private set; }

		public double YPosition { get; private set; }

		public double ZPosition { get; private set; }

		public static int CalculateChecksum(string commandToGetChecksumFor)
		{
			int checksum = 0;
			if (commandToGetChecksumFor.Length > 0)
			{
				checksum = commandToGetChecksumFor[0];
				for (int i = 1; i < commandToGetChecksumFor.Length; i++)
				{
					checksum ^= commandToGetChecksumFor[i];
				}
			}
			return checksum;
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

		public static double ParseDouble(String source, ref int startIndex)
		{
			Match numberMatch = numberRegex.Match(source, startIndex);
			String returnString = numberMatch.Value;
			startIndex = numberMatch.Index + numberMatch.Length;
			double returnVal;
			double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
			return returnVal;
		}

		public void Dispose()
		{
			ShutDown();

			Emulator.Instance = null;
		}

		public string Echo(string command)
		{
			return command;
		}

		public string GetCommandKey(string command)
		{
			if (command.IndexOf(' ') != -1)
			{
				return command.Substring(0, command.IndexOf(' '));
			}
			return command;
		}

		public string GetCorrectResponse(string inCommand)
		{
			try
			{
				// Remove line returns
				var commandNoNl = inCommand.Split('\n')[0]; // strip of the trailing cr (\n)
				var command = ParseChecksumLine(commandNoNl);
				if (command.Contains("Resend"))
				{
					return command + "ok\n";
				}

				var commandKey = GetCommandKey(command);
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

		public string GetPosition(string command)
		{
			// position commands look like this: X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00 then an ok on the next line
			return $"X:{XPosition:0.00} Y: {YPosition:0.00} Z: {ZPosition:0.00} E: {EPosition:0.00} Count X: 0.00 Y: 0.00 Z: 0.00\nok\n";
		}

		public string ReportMarlinFirmware(string command)
		{
			return "FIRMWARE_NAME:Marlin V1; Sprinter/grbl mashup for gen6 FIRMWARE_URL:https://github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Framelis v1 EXTRUDER_COUNT:1 UUID:155f84b5-d4d7-46f4-9432-667e6876f37a\nok\n";
		}

		// Add response callbacks here
		public string ReturnTemp(string command)
		{
			// temp commands look like this: ok T:19.4 /0.0 B:0.0 /0.0 @:0 B@:0
			if (BedGoalTemperature == -1)
			{
				if (ExtruderGoalTemperature != 0)
				{
					ExtruderCurrentTemperature = ExtruderCurrentTemperature + (ExtruderGoalTemperature - ExtruderCurrentTemperature) * .8;
				}
				return $"ok T:{ExtruderCurrentTemperature:0.0} / {ExtruderGoalTemperature:0.0}\n";
			}
			else
			{
				ExtruderCurrentTemperature = ExtruderCurrentTemperature + (ExtruderGoalTemperature - ExtruderCurrentTemperature) * .8;
				BedCurrentTemperature = BedCurrentTemperature + (BedGoalTemperature - BedCurrentTemperature) * .8;
				return $"ok T:{ExtruderCurrentTemperature:0.0} / {ExtruderGoalTemperature:0.0} B: {BedCurrentTemperature:0.0} / {BedGoalTemperature:0.0}\n";
			}
		}

		public string SetFan(string command)
		{
			try
			{
				var sIndex = command.IndexOf('S') + 1;
				FanSpeed = int.Parse(command.Substring(sIndex));
				FanSpeedChanged?.Invoke(this, null);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}

		public void ShutDown()
		{
			shutDown = true;
		}

		public void SimulateReboot()
		{
			commandIndex = 1;
			recievedCount = 0;
		}

		private string HomePosition(string command)
		{
			XPosition = 0;
			YPosition = 0;
			ZPosition = 0;
			return "ok\n";
		}

		private string InitSdCard(string arg)
		{
			return "ok\n";
		}

		private string ListSdCard(string arg)
		{
			string[] responsList =
			{
				"Begin file list",
				"Item 1.gcode",
				"Item 2.gcode",
				"End file list",
			};

			foreach (var response in responsList)
			{
				this.QueueResponse(response + '\n');
			}

			return "ok\n";
		}

		private string ParseChecksumLine(string command)
		{
			recievedCount++;
			if (SimulateLineErrors && (recievedCount % 11) == 0)
			{
				command = "N-1 nthoeuc 654*";
			}

			if (command[0] == 'N')
			{
				double lineNumber = 0;
				GetFirstNumberAfter("N", command, ref lineNumber);
				var checksumStart = command.LastIndexOf('*');
				var commandToChecksum = command.Substring(0, checksumStart);
				if(commandToChecksum[commandToChecksum.Length-1] == ' ')
				{
					commandToChecksum = commandToChecksum.Substring(0, commandToChecksum.Length - 1);
				}
				double expectedChecksum = 0;
				GetFirstNumberAfter("*", command, ref expectedChecksum, checksumStart);
				int actualChecksum = CalculateChecksum(commandToChecksum);
				if ((lineNumber == commandIndex
					&& actualChecksum == expectedChecksum)
					|| command.Contains("M110"))
				{
					commandIndex++;
					int spaceIndex = command.IndexOf(' ') + 1;
					int endIndex = command.IndexOf('*');
					return command.Substring(spaceIndex, endIndex - spaceIndex);
				}
				else
				{
					return $"Error:checksum mismatch, Last Line: {commandIndex - 1}\nResend: {commandIndex}\n";
				}
			}
			else
			{
				return command;
			}
		}

		private string ResetPosition(string command)
		{
			return "ok\n";
		}

		private string SetBedTemperature(string command)
		{
			try
			{
				// M140 S210 or M190 S[temp]
				var sIndex = command.IndexOf('S') + 1;
				BedGoalTemperature = int.Parse(command.Substring(sIndex));
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
				ExtruderGoalTemperature = int.Parse(command.Substring(sIndex));
				ExtruderTemperatureChanged?.Invoke(this, null);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return "ok\n";
		}

		private string SetLineCount(string command)
		{
			double number = commandIndex;
			if (GetFirstNumberAfter("N", command, ref number))
			{
				commandIndex = (long)number + 1;
			}

			return "ok\n";
		}

		private string SetPosition(string command)
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

		private string Wait(string command)
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
	}

	public class EmulatorPortFactory : FrostedSerialPortFactory
	{
		override protected string GetDriverType() => "Emulator";
		public override IFrostedSerialPort Create(string serialPortName) => new Emulator();
	}

	// EmulatorPort
	public partial class Emulator : IFrostedSerialPort
	{
		public bool RtsEnable { get; set; }
		public bool DtrEnable { get; set; }
		public int BaudRate { get; set; }

		private Queue<string> sendQueue = new Queue<string>();
		private Queue<string> receiveQueue = new Queue<string>();

		private AutoResetEvent receiveResetEvent;

		private object receiveLock = new object();
		private object sendLock = new object();

		public int BytesToRead
		{
			get
			{
				if (sendQueue.Count == 0)
				{
					return 0;
				}

				return sendQueue?.Peek().Length ?? 0;
			}
		}

		public int WriteTimeout { get; set; }
		public int ReadTimeout { get; set; }

		public bool IsOpen { get; private set; }

		public void Close()
		{
			this.shutDown = true;
		}

		public void Open()
		{
			this.IsOpen = true;

			receiveResetEvent = new AutoResetEvent(false);

			this.ReadTimeout = 500;
			this.WriteTimeout = 500;

			Console.WriteLine("\n Initializing emulator (Speed: {0})", (this.RunSlow) ? "slow" : "fast");

			Task.Run(() =>
			{
				while (!shutDown)
				{
					if (this.DtrEnable != DsrState)
					{
						DsrState = this.DtrEnable;
						DsrChangeCount++;
					}

					Thread.Sleep(10);
				}
			});

			Task.Run(() =>
			{
				while (!shutDown)
				{
					if (receiveQueue.Count == 0)
					{
						receiveResetEvent.WaitOne();
					}

					if (receiveQueue.Count == 0)
					{
						Thread.Sleep(10);
					}
					else
					{
						string receivedLine;

						lock (receiveLock)
						{
							receivedLine = receiveQueue.Dequeue();
						}

						if (receivedLine?.Length > 0)
						{
							//Thread.Sleep(250);
							string emulatedResponse = GetCorrectResponse(receivedLine);

							lock(sendLock)
							{
								sendQueue.Enqueue(emulatedResponse);
							}
						}
					}
				}

				this.IsOpen = false;

				this.Close();
				this.Dispose();
			});

			this.IsOpen = true;
		}

		public void QueueResponse(string line)
		{
			sendQueue.Enqueue(line);
		}

		public string ReadExisting()
		{
			lock(sendLock)
			{
				return sendQueue.Dequeue();
			}
		}

		public void Write(string receivedLine)
		{
			lock(receiveLock)
			{
				receiveQueue.Enqueue(receivedLine);
			}


			// Release the main loop to process the received command
			receiveResetEvent.Set();
		}

		public int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

		public void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
	}
}