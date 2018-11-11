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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.EeProm
{
	public class EePromRepetierStorage
	{
		public Dictionary<int, EePromRepetierParameter> eePromSettingsList;

		public event EventHandler SettingAdded = null;

		public EePromRepetierStorage()
		{
			eePromSettingsList = new Dictionary<int, EePromRepetierParameter>();
		}

		public void Clear()
		{
			lock (eePromSettingsList)
			{
				eePromSettingsList.Clear();
			}
		}

		public void Save(PrinterConnection printerConnection)
		{
			lock (eePromSettingsList)
			{
				foreach (EePromRepetierParameter p in eePromSettingsList.Values)
				{
					p.Save(printerConnection);
				}
			}
		}

		public void Add(object sender, string line)
		{
			if (line == null
				|| !line.StartsWith("EPR:"))
			{
				return;
			}

			var parameter = new EePromRepetierParameter(line);
			lock (eePromSettingsList)
			{
				if (eePromSettingsList.ContainsKey(parameter.position))
				{
					eePromSettingsList.Remove(parameter.position);
				}

				eePromSettingsList.Add(parameter.position, parameter);
			}

			this.SettingAdded?.Invoke(this, parameter);
		}

		public void AskPrinterForSettings(PrinterConnection printerConnection)
		{
			printerConnection.QueueLine("M205");
		}

		internal void Export(string fileName)
		{
			using (var sw = new StreamWriter(fileName))
			{
				lock (eePromSettingsList)
				{
					foreach (EePromRepetierParameter p in eePromSettingsList.Values)
					{
						sw.WriteLine("{0}|{1}", p.description, p.value);
					}
				}
			}
		}

		internal void Import(string fileName)
		{
			// find all the descriptions we can
			foreach (string line in File.ReadAllLines(fileName))
			{
				if (line.Contains("|"))
				{
					string[] descriptionValue = line.Split('|');
					if (descriptionValue.Length == 2)
					{
						foreach (KeyValuePair<int, EePromRepetierParameter> keyValue in eePromSettingsList)
						{
							if (keyValue.Value.Description == descriptionValue[0])
							{
								if (keyValue.Value.Value != descriptionValue[1])
								{
									// push in the value
									keyValue.Value.Value = descriptionValue[1];
									keyValue.Value.MarkChanged();
									break;
								}
							}
						}
					}
				}
			}
		}
	}
}