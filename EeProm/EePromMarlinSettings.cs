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
using System.IO;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.EeProm
{
	public class EePromMarlinSettings : EventArgs
	{
		public event EventHandler eventAdded = null;

		public string sx = "0";
		public string sy = "0";
		public string sz = "0";
		public string se = "0";
		public string fx = "0";
		public string fy = "0";
		public string fz = "0";
		public string fe = "0";
		public string ax = "0";
		public string ay = "0";
		public string az = "0";
		public string ae = "0";
		public string acc = "0";
		public string racc = "0";
		public string avs = "0";
		public string avt = "0";
		public string avb = "0";
		public string avx = "0";
		public string avz = "0";
		public string ppid = "0";
		public string ipid = "0";
		public string dpid = "0";
		public string hox = "0";
		public string hoy = "0";
		public string hoz = "0";
		public bool hasPID = false;

		private bool changed = false;
		private PrinterConnection printerConnection;

		public EePromMarlinSettings(PrinterConnection printerConnection)
		{
			this.printerConnection = printerConnection;
		}

		public bool update(string line)
		{
			bool foundSetting = false;
			// string[] lines = line.Substring(5).Split(' ');
			string[] test = line.Split(' ');
			string mode = "";
			bool foundFirstM92E = false;
			foreach (string token in test)
			{
				if(string.IsNullOrWhiteSpace(token))
				{
					continue;
				}
				if (((token == "M92") || (mode == "M92")))
				{
					foundSetting = true;
					if (mode != "M92")
					{
						foundFirstM92E = false;
					}

					mode = "M92";
					if (token[0] == 'X')
					{
						sx = token.Substring(1);
					}
					if (token[0] == 'Y')
					{
						sy = token.Substring(1);
					}
					if (token[0] == 'Z')
					{
						sz = token.Substring(1);
					}
					if (token[0] == 'E' && !foundFirstM92E)
					{
						foundFirstM92E = true;
						se = token.Substring(1);
					}
				}
				if (((token == "M203") || (mode == "M203")))
				{
					foundSetting = true;
					mode = "M203";
					if (token[0] == 'X')
					{
						fx = token.Substring(1);
					}
					if (token[0] == 'Y')
					{
						fy = token.Substring(1);
					}
					if (token[0] == 'Z')
					{
						fz = token.Substring(1);
					}
					if (token[0] == 'E')
					{
						fe = token.Substring(1);
					}
				}
				if (((token == "M201") || (mode == "M201")))
				{
					foundSetting = true;
					mode = "M201";
					if (token[0] == 'X')
					{
						ax = token.Substring(1);
					}
					if (token[0] == 'Y')
					{
						ay = token.Substring(1);
					}
					if (token[0] == 'Z')
					{
						az = token.Substring(1);
					}
					if (token[0] == 'E')
					{
						ae = token.Substring(1);
					}
				}
				if (((token == "M204") || (mode == "M204")))
				{
					foundSetting = true;
					mode = "M204";
					if (token[0] == 'S')
					{
						acc = token.Substring(1);
					}
					if (token[0] == 'T')
					{
						racc = token.Substring(1);
					}
				}
				if (((token == "M205") || (mode == "M205")))
				{
					foundSetting = true;
					mode = "M205";
					if (token[0] == 'S')
					{
						avs = token.Substring(1);
					}
					if (token[0] == 'T')
					{
						avt = token.Substring(1);
					}
					if (token[0] == 'B')
					{
						avb = token.Substring(1);
					}
					if (token[0] == 'X')
					{
						avx = token.Substring(1);
					}
					if (token[0] == 'Z')
					{
						avz = token.Substring(1);
					}
				}
				if (((token == "M301") || (mode == "M301")))
				{
					foundSetting = true;
					mode = "M301";
					hasPID = true;
					if (token[0] == 'P')
					{
						ppid = token.Substring(1);
					}
					if (token[0] == 'I')
					{
						ipid = token.Substring(1);
					}
					if (token[0] == 'D')
					{
						dpid = token.Substring(1);
					}
				}
				if (((token == "M206") || (mode == "M206")))
				{
					foundSetting = true;
					mode = "M206";
					hasPID = true;
					if (token[0] == 'X')
					{
						hox = token.Substring(1);
					}
					if (token[0] == 'Y')
					{
						hoy = token.Substring(1);
					}
					if (token[0] == 'Z')
					{
						hoz = token.Substring(1);
					}
				}
			}
			changed = false;

			return foundSetting;
		}

		public void Save()
		{
			if (!changed) return; // nothing changed
			string cmdsteps = "M92 X" + sx + " Y" + sy + " Z" + sz + " E" + se;
			string cmdfeed = "M203 X" + fx + " Y" + fy + " Z" + fz + " E" + fe;
			string cmdmacc = "M201 X" + ax + " Y" + ay + " Z" + az + " E" + ae;
			string cmdacc = "M204 S" + acc + " T" + racc;
			string cmdav = "M205 S" + avs + " T" + avt + " B" + avb + " X" + avx + " Z" + avz;
			string cmdho = "M206 X" + hox + " Y" + hoy + " Z" + hoz;
			string cmdpid = "M301 P" + ppid + " I" + ipid + " D" + dpid;

			printerConnection.QueueLine(cmdsteps);
			printerConnection.QueueLine(cmdfeed);
			printerConnection.QueueLine(cmdmacc);
			printerConnection.QueueLine(cmdacc);
			printerConnection.QueueLine(cmdav);
			printerConnection.QueueLine(cmdho);
			if (hasPID)
			{
				printerConnection.QueueLine(cmdpid);
			}

			changed = false;
		}

		public string SX
		{
			get { return sx; }
			set { if (sx.Equals(value)) return; sx = value; changed = true; }
		}

		public string SY
		{
			get { return sy; }
			set { if (sy.Equals(value)) return; sy = value; changed = true; }
		}

		public string SZ
		{
			get { return sz; }
			set { if (sz.Equals(value)) return; sz = value; changed = true; }
		}
		//This is it 
		public string SE
		{

			//String l
			get { return se; }
			set
			{
				if (se.Equals(value))
					return;
				se = value;
				changed = true;
			}
		}

		public string FX
		{
			get { return fx; }
			set { if (fx.Equals(value)) return; fx = value; changed = true; }
		}

		public string FY
		{
			get { return fy; }
			set { if (fy.Equals(value)) return; fy = value; changed = true; }
		}

		public string FZ
		{
			get { return fz; }
			set { if (fz.Equals(value)) return; fz = value; changed = true; }
		}

		public string FE
		{
			get { return fe; }
			set { if (fe.Equals(value)) return; fe = value; changed = true; }
		}

		public string AX
		{
			get { return ax; }
			set { if (ax.Equals(value)) return; ax = value; changed = true; }
		}

		internal void Import(string fileName)
		{
			// read all the lines 
			string[] allLines = File.ReadAllLines(fileName);
			// find all the descriptions we can
			foreach (string line in allLines)
			{
				if (line.Contains("|"))
				{
					string[] descriptionValue = line.Split('|');
					if (descriptionValue.Length == 2)
					{
						SetSetting(descriptionValue[0], descriptionValue[1]);
					}
				}
			}
			changed = true;
		}

		void SetSetting(string keyToSet, string valueToSetTo)
		{
			valueToSetTo = valueToSetTo.Replace("\"", "").Trim();

			List<string> lines = new List<string>();
			FieldInfo[] fields;
			fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
			foreach (FieldInfo field in fields)
			{
				List<string> possibleNames = new List<string>();
				possibleNames.Add(field.Name);

				if (possibleNames.Contains(keyToSet))
				{
					string name = field.Name;
					object value = field.GetValue(this);
					switch (field.FieldType.Name)
					{
						case "Int32":
							field.SetValue(this, (int)double.Parse(valueToSetTo));
							break;

						case "Double":
							field.SetValue(this, double.Parse(valueToSetTo));
							break;

						case "Boolean":
							field.SetValue(this, bool.Parse(valueToSetTo));
							break;

						case "String":
							field.SetValue(this, valueToSetTo.Replace("\\n", "\n"));
							break;

						default:
							throw new NotImplementedException("unknown type");
					}
				}
			}
		}

		public string AY
		{
			get { return ay; }
			set { if (ay.Equals(value)) return; ay = value; changed = true; }
		}

		public string AZ
		{
			get { return az; }
			set { if (az.Equals(value)) return; az = value; changed = true; }
		}

		public string AE
		{
			get { return ae; }
			set { if (ae.Equals(value)) return; ae = value; changed = true; }
		}

		public string ACC
		{
			get { return acc; }
			set { if (acc.Equals(value)) return; acc = value; changed = true; }
		}

		public string RACC
		{
			get { return racc; }
			set { if (racc.Equals(value)) return; racc = value; changed = true; }
		}

		public string AVS
		{
			get { return avs; }
			set { if (avs.Equals(value)) return; avs = value; changed = true; }
		}

		internal void Export(string fileName)
		{
			using (var sw = new StreamWriter(fileName))
			{
				FieldInfo[] fields;
				fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
				foreach (FieldInfo field in fields)
				{
					string name = field.Name;
					object value = field.GetValue(this);
					switch (field.FieldType.Name)
					{
						case "Int32":
						case "Double":
						case "Boolean":
						case "FMatrix3x3":
						case "String":
							// all these setting just output correctly with ToString() so we don't have to do anything special.
							sw.WriteLine("{0}|{1}".FormatWith(name, value));
							break;
					}
				}
			}
		}

		public string AVT
		{
			get { return avt; }
			set { if (avt.Equals(value)) return; avt = value; changed = true; }
		}

		public string AVB
		{
			get { return avb; }
			set { if (avb.Equals(value)) return; avb = value; changed = true; }
		}

		public string AVX
		{
			get { return avx; }
			set { if (avx.Equals(value)) return; avx = value; changed = true; }
		}

		public string AVZ
		{
			get { return avz; }
			set { if (avz.Equals(value)) return; avz = value; changed = true; }
		}

		public string PPID
		{
			get { return ppid; }
			set { if (ppid.Equals(value)) return; ppid = value; changed = true; }
		}

		public string IPID
		{
			get { return ipid; }
			set { if (ipid.Equals(value)) return; ipid = value; changed = true; }
		}

		public string DPID
		{
			get { return dpid; }
			set { if (dpid.Equals(value)) return; dpid = value; changed = true; }
		}

		public string HOX
		{
			get { return hox; }
			set { if (hox.Equals(value)) return; hox = value; changed = true; }
		}

		public string HOY
		{
			get { return hoy; }
			set { if (hoy.Equals(value)) return; hoy = value; changed = true; }
		}

		public string HOZ
		{
			get { return hoz; }
			set { if (hoz.Equals(value)) return; hoz = value; changed = true; }
		}

		public void SaveToEeProm()
		{
			printerConnection.QueueLine("M500");
		}

		// this does not save them to eeprom
		public void SetPrinterToFactorySettings()
		{
			hasPID = false;
			printerConnection.QueueLine("M502");
		}

		public void Add(object sender, EventArgs e)
		{
			StringEventArgs lineString = e as StringEventArgs;

			if (e == null)
			{
				return;
			}

			if (update(lineString.Data))
			{
				if (eventAdded != null)
				{
					UiThread.RunOnIdle(() =>
					{
						if (lineString != null)
						{
							eventAdded(this, lineString);
						}
					});
				}
			}
		}

		public void Update()
		{
			hasPID = false;
			printerConnection.QueueLine("M503");
		}
	}
}