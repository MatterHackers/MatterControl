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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MatterHackers.MatterControl.DataStorage
{
	public class Entity
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		protected bool isSaved;
		protected int hashCode = 0;

		public event PropertyChangedEventHandler PropertyChanged;

		private IEnumerable<PropertyInfo> properties;

		public Entity()
		{
			isSaved = false;
		}

		private static readonly int maxRetries = 20;
		private int retryCount = 0;

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
				Thread.Sleep(100);
				this.TryHandleUpdate();
			}

			retryCount = 0;
		}

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

		protected void OnPropertyChanged(string name)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			this.hashCode = 0;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(name));
			}
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
	}

	public class ApplicationSession : Entity
	{
		public DateTime SessionStart { get; set; }

		public DateTime SessionEnd { get; set; }

		public int PrintCount { get; set; }

		public ApplicationSession()
			: base()
		{
			SessionStart = DateTime.Now;
			PrintCount = 0;
		}
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

		public string Name { get; set; }

		public string Key { get; set; }
	}

	public class PrintItem : Entity
	{
		[Indexed]
		public int PrintItemCollectionID { get; set; }

		public string Name { get; set; }

		public string LibraryProviderBreadCrumbs { get; set; }

		public string FileLocation { get; set; }

		public DateTime DateAdded { get; set; }

		public int PrintCount { get; set; }

		public PrintItem()
			: this("", "")
		{
		}

		public PrintItem(string name, string fileLocation, string libraryProviderBreadCrumbs = "")
		{
			this.Name = name;
			this.FileLocation = fileLocation;
			this.LibraryProviderBreadCrumbs = libraryProviderBreadCrumbs;

			DateAdded = DateTime.Now;
			PrintCount = 0;
		}
	}

	public class SliceSettingsCollection : Entity
	{
		public string Name { get; set; }

		public string Tag { get; set; } //ex. 'material' or 'quality'

		public int PrinterId { get; set; }
	}

	public class SliceSetting : Entity
	{
		[Indexed]
		public int SettingsCollectionId { get; set; }

		public string Name { get; set; }

		public string Value { get; set; }

		public SliceSetting()
			: base()
		{
		}
	}

	public class PrintTask : Entity
	{
		[Indexed]
		public int PrinterId { get; set; }

		[Indexed]
		public int PrintItemId { get; set; }

		public string PrintName { get; set; }

		public DateTime PrintStart { get; set; }

		public DateTime PrintEnd { get; set; }

		public int PrintTimeSeconds { get; set; }

		public bool PrintComplete { get; set; }

		public PrintTask()
			: base()
		{
			PrintStart = DateTime.Now;
		}

		public int PrintTimeMinutes
		{
			get
			{
				TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);

				return (int)(printTimeSpan.TotalMinutes + .5);
			}
		}

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

	public class SystemSetting : Entity
	{
		[Indexed]
		public string Name { get; set; }

		public string Value { get; set; }

		public DateTime DateLastModified { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class UserSetting : Entity
	{
		[Indexed]
		public string Name { get; set; }

		public string Value { get; set; }

		public DateTime DateLastModified { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class CustomCommands : Entity
	{
		[Indexed]
		public int PrinterId { get; set; }

		public string Name { get; set; }

		public string Value { get; set; }

		public DateTime DateLastModified { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}

	public class Printer : Entity
	{
		public int DefaultSettingsCollectionId { get; set; }

		public string Name { get; set; }

		public string Make { get; set; }

		public string Model { get; set; }

		public string ComPort { get; set; }

		public string DriverType { get; set; }

		public string BaudRate { get; set; }

		public bool AutoConnectFlag { get; set; } //Auto connect to printer (if available)

		public string DeviceToken { get; set; }

		public string DeviceType { get; set; }

		// all the data about print leveling
		public bool DoPrintLeveling { get; set; }

		public string PrintLevelingJsonData { get; set; }

		public string PrintLevelingProbePositions { get; set; } // this is depricated go through PrintLevelingData

		// features
		public string _features { get; set; }

		public string ManualMovementSpeeds { get; set; } // stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...

		public string CurrentSlicingEngine { get; set; }

		public string MaterialCollectionIds { get; set; } // store id1,id2... (for N extruders)

		public int QualityCollectionId { get; set; }

		public Printer()
			: base()
		{
			this.Make = "Unknown";
			this.Model = "Unknown";
		}
	}

	public class PrinterSetting : Entity
	{
		[Indexed]
		public int PrinterId { get; set; }

		public string Name { get; set; }

		public string Value { get; set; }

		public DateTime DateLastModified { get; set; }

		public override void Commit()
		{
			DateLastModified = DateTime.Now;
			base.Commit();
		}
	}
}