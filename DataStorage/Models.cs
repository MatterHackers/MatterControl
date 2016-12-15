/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DataStorage
{
	public class ApplicationSession : Entity
	{
		public ApplicationSession()
			: base()
		{
			SessionStart = DateTime.Now;
			PrintCount = 0;
		}

		public int PrintCount { get; set; }

		public DateTime SessionEnd { get; set; }

		public DateTime SessionStart { get; set; }
	}

	public class CustomCommands : Entity
	{
		public DateTime DateLastModified { get; set; }

		public string Name { get; set; }

		[Indexed]
		public int PrinterId { get; set; }

		public string Value { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class Entity
	{
		protected int hashCode = 0;

		protected bool isSaved;

		private static readonly int maxRetries = 20;

		private IEnumerable<PropertyInfo> properties;

		private int retryCount = 0;

		public Entity()
		{
			isSaved = false;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public virtual void Commit()
		{
			//Assumes that autoincremented ids start with 1
			if (this.Id == 0)
			{
				TryHandleInsert();
			}
			else
			{
				TryHandleUpdate();
			}
		}

		public void Delete()
		{
			Datastore.Instance.dbSQLite.Delete(this);
		}

		public override int GetHashCode()
		{
			StringBuilder bigStringForHashCode = new StringBuilder();

			if (this.hashCode == 0)
			{
				properties = this.GetType()
						.GetProperties(BindingFlags.Instance |
									  BindingFlags.Public)
					// Ignore non-string properties
						.Where(prop => prop.PropertyType == typeof(string) || prop.PropertyType == typeof(int))
					// Ignore indexers
						.Where(prop => prop.GetIndexParameters().Length == 0)
					// Must be both readable and writable
						.Where(prop => prop.CanWrite && prop.CanRead);

				foreach (PropertyInfo prop in properties)
				{
					object objectHoldingValue = prop.GetValue(this, null);
					if (objectHoldingValue != null)
					{
						string value = objectHoldingValue.ToString();
						if (value != null)
						{
							bigStringForHashCode.Append(prop.Name);
							bigStringForHashCode.Append(value);
						}
					}
				}
				this.hashCode = bigStringForHashCode.ToString().GetHashCode();
			}

			return this.hashCode;
		}

		protected void OnPropertyChanged(string name)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			this.hashCode = 0;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(name));
			}
		}

		private void TryHandleInsert()
		{
			retryCount++;
			try
			{
				if (retryCount < maxRetries)
				{
					Datastore.Instance.dbSQLite.Insert(this);
				}
			}
			catch (Exception)
			{
				GuiWidget.BreakInDebugger();
				Thread.Sleep(100);
				this.TryHandleInsert();
			}

			retryCount = 0;
		}

		private void TryHandleUpdate()
		{
			retryCount++;
			try
			{
				if (retryCount < maxRetries)
				{
					Datastore.Instance.dbSQLite.Update(this);
				}
			}
			catch (Exception)
			{
				GuiWidget.BreakInDebugger();
				Thread.Sleep(100);
				this.TryHandleUpdate();
			}

			retryCount = 0;
		}
	}

	public class Printer : Entity
	{
		public Printer()
			: base()
		{
			this.Make = "Unknown";
			this.Model = "Unknown";
		}

		// features
		public string _features { get; set; }

		public bool AutoConnect { get; set; }

		public string BaudRate { get; set; }

		public string ComPort { get; set; }

		public string CurrentSlicingEngine { get; set; }

		public int DefaultSettingsCollectionId { get; set; }

		public string DeviceToken { get; set; }

		//Auto connect to printer (if available)
		public string DeviceType { get; set; }

		// all the data about print leveling
		public bool DoPrintLeveling { get; set; }

		public string DriverType { get; set; }

		public string Make { get; set; }

		public string ManualMovementSpeeds { get; set; }

		// stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...
		public string MaterialCollectionIds { get; set; }

		public string Model { get; set; }

		public string Name { get; set; }

		public string PrintLevelingJsonData { get; set; }

		public string PrintLevelingProbePositions { get; set; } // this is deprecated go through PrintLevelingData

		// store id1,id2... (for N extruders)

		public int QualityCollectionId { get; set; }
	}

	public class PrinterSetting : Entity
	{
		public DateTime DateLastModified { get; set; }

		public string Name { get; set; }

		[Indexed]
		public int PrinterId { get; set; }

		public string Value { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class PrintItem : Entity
	{
		public PrintItem()
			: this("", "")
		{
		}

		public PrintItem(string name, string fileLocation)
		{
			this.Name = name;
			this.FileLocation = fileLocation;

			DateAdded = DateTime.Now;
			PrintCount = 0;
		}

		public DateTime DateAdded { get; set; }

		public string FileLocation { get; set; }

		public string Name { get; set; }

		public int PrintCount { get; set; }

		[Indexed]
		public int PrintItemCollectionID { get; set; }

		public bool ReadOnly { get; set; }

		public bool Protected { get; set; }
	}

	public class PrintItemCollection : Entity
	{
		public PrintItemCollection()
		{
		}

		public PrintItemCollection(string name, string collectionKey)
		{
			this.Name = name;
			Key = collectionKey;
		}

		public string Key { get; set; }

		public string Name { get; set; }

		[Indexed]
		public int ParentCollectionID { get; set; }
	}

	public class PrintTask : Entity
	{
		public PrintTask()
			: base()
		{
			PrintStart = DateTime.Now;
		}

		public string PrintingGCodeFileName { get; set; }

		public double RecoveryCount { get; set; }

		public double PercentDone { get; set; }

		public bool PrintComplete { get; set; }

		public DateTime PrintEnd { get; set; }

		[Indexed]
		public int PrinterId { get; set; }

		[Indexed]
		public int PrintItemId { get; set; }

		public string PrintName { get; set; }

		public DateTime PrintStart { get; set; }

		public int PrintTimeMinutes
		{
			get
			{
				TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);

				return (int)(printTimeSpan.TotalMinutes + .5);
			}
		}

		public int PrintTimeSeconds { get; set; }
		public float PrintingOffsetX { get; set; }
		public float PrintingOffsetY { get; set; }
		public float PrintingOffsetZ { get; set; }

		public override void Commit()
		{
			if (this.PrintEnd != DateTime.MinValue)
			{
				TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);
				PrintTimeSeconds = (int)printTimeSpan.TotalSeconds;
			}
			base.Commit();
		}
	}

	public class SliceSetting : Entity
	{
		public SliceSetting()
			: base()
		{
		}

		public string Name { get; set; }

		[Indexed]
		public int SettingsCollectionId { get; set; }

		public string Value { get; set; }
	}

	public class SliceSettingsCollection : Entity
	{
		public string Name { get; set; }

		public int PrinterId { get; set; }

		public string Tag { get; set; } //ex. 'material' or 'quality'
	}

	public class SystemSetting : Entity
	{
		public DateTime DateLastModified { get; set; }

		[Indexed]
		public string Name { get; set; }

		public string Value { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class UserSetting : Entity
	{
		public DateTime DateLastModified { get; set; }

		[Indexed]
		public string Name { get; set; }

		public string Value { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}
}