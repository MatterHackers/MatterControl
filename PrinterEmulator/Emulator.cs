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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrinterEmulator
{
	public class Emulator
	{
		double extruderGoalTemperature = 210;
		double bedGoalTemperature = -1; // no bed present
		Random random = new Random();

		// Dictionary of command and response callback
		Dictionary<string, Func<string, string>> responses = new Dictionary<string, Func<string, string>>();

		public Emulator()
		{
			responses.Add("M105", randomTemp);
			responses.Add("A", echo);
			responses.Add("M114", getPosition);
			responses.Add("N", parseChecksumLine);
			responses.Add("M115", reportMarlinFirmware);
			responses.Add("M104", setExtruderTemperature);
			responses.Add("M109", setExtruderTemperature);
			responses.Add("M140", setBedTemperature);
			responses.Add("M190", setBedTemperature);
		}

		// Add response callbacks here
		public string randomTemp(string command)
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

		public string getCorrectResponse(string command)
		{
			try
			{
				// Remove line returns
				command = command.Split('\n')[0]; // strip of the trailing cr (\n)
				command = parseChecksumLine(command);
				var commandKey = getCommandKey(command);
				if (responses.ContainsKey(commandKey))
				{
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

		public string getPosition(string command)
		{
			// position commands look like this: X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00 then an ok on the next line
			return "X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00\nok\n";
		}

		public string reportMarlinFirmware(string command)
		{
			return "FIRMWARE_NAME:Marlin V1; Sprinter/grbl mashup for gen6 FIRMWARE_URL:https://github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Framelis v1 EXTRUDER_COUNT:1 UUID:155f84b5-d4d7-46f4-9432-667e6876f37a\nok\n";
		}

		public string echo(string command)
		{
			return command;
		}

		string parseChecksumLine(string command)
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

		public string getCommandKey(string command)
		{
			if (command.IndexOf(' ') != -1)
			{
				return command.Substring(0, command.IndexOf(' '));
			}
			return command;
		}

		string setExtruderTemperature(string command)
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

		string setBedTemperature(string command)
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
	}
}
