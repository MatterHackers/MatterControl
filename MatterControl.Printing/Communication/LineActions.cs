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
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.SerialPortCommunication
{
	/// <summary>
	/// A dictionary of key to Action delegates that are invoked per received/sent line
	/// </summary>
	public abstract class LineActions
	{
		public Dictionary<string, List<Action<string>>> registeredActions = new Dictionary<string, List<Action<string>>>();

		public void Register(string key, Action<string> value)
		{
			if (registeredActions.ContainsKey(key))
			{
				registeredActions[key].Add(value);
			}
			else
			{
				registeredActions.Add(key, new List<Action<string>>() { value });
			}
		}

		public void Clear()
		{
			registeredActions.Clear();
		}

		public void Unregister(string key, Action<string> value)
		{
			if (registeredActions.ContainsKey(key))
			{
				if (registeredActions[key].Contains(value))
				{
					registeredActions[key].Remove(value);
				}
			}
			else
			{
				throw new Exception();
			}
		}

		public abstract void ProcessLine(string line);
	}

	public class StartsWithLineActions : LineActions
	{
		public override void ProcessLine(string line)
		{
			foreach (var kvp in this.registeredActions)
			{
				if (line.StartsWith(kvp.Key))
				{
					foreach (var value in kvp.Value)
					{
						value(line);
					}
				}
			}
		}
	}

	public class ContainsStringLineActions : LineActions
	{
		public override void ProcessLine(string line)
		{
			foreach (var kvp in this.registeredActions)
			{
				if (line.Contains(kvp.Key))
				{
					foreach (var value in kvp.Value)
					{
						value(line);
					}
				}
			}
		}
	}
}