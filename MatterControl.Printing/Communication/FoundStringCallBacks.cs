/*
Copyright (c) 2014, Lars Brubaker
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

namespace MatterHackers.SerialPortCommunication
{
	public class FoundStringEventArgs : EventArgs
	{
		public FoundStringEventArgs(string lineReceived)
		{
			this.LineToCheck = lineReceived.Trim();
		}

		public bool CallbackWasCalled { get; set; }

		public string LineToCheck { get; }

		public bool SendToDelegateFunctions { get; set; }
	}

	public class FoundStringCallbacks
	{
		public Dictionary<string, EventHandler<FoundStringEventArgs> > dictionaryOfCallbacks = new Dictionary<string, EventHandler<FoundStringEventArgs>>();

		public void AddCallbackToKey(string key, EventHandler<FoundStringEventArgs> value)
		{
			if (dictionaryOfCallbacks.ContainsKey(key))
			{
				dictionaryOfCallbacks[key] += value;
			}
			else
			{
				dictionaryOfCallbacks.Add(key, value);
			}
		}

		public void RemoveCallbackFromKey(string key, EventHandler<FoundStringEventArgs> value)
		{
			if (dictionaryOfCallbacks.ContainsKey(key))
			{
				if (dictionaryOfCallbacks[key] == null)
				{
					throw new Exception();
				}
				dictionaryOfCallbacks[key] -= value;
				if (dictionaryOfCallbacks[key] == null)
				{
					dictionaryOfCallbacks.Remove(key);
				}
			}
			else
			{
				throw new Exception();
			}
		}
	}

	public class FoundStringStartsWithCallbacks : FoundStringCallbacks
	{
		public void CheckForKeys(FoundStringEventArgs e)
		{
			foreach (var pair in this.dictionaryOfCallbacks)
			{
				if (e.LineToCheck.StartsWith(pair.Key))
				{
					e.CallbackWasCalled = true;
					pair.Value(this, e);
				}
			}
		}
	}

	public class FoundStringContainsCallbacks : FoundStringCallbacks
	{
		public void CheckForKeys(FoundStringEventArgs e)
		{
			foreach (var pair in this.dictionaryOfCallbacks)
			{
				if (e.LineToCheck.Contains(pair.Key))
				{
					e.CallbackWasCalled = true;
					pair.Value(this, e);
				}
			}
		}
	}
}