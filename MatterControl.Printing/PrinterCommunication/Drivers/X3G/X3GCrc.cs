using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	class X3GCrc
	{
		private uint crc;

		public X3GCrc()
		{
			crc = 0;
		}

		public void update(byte command)
		{
			crc = (crc ^ command) & 0xff;
			for (int i = 0; i < 8; i++)
			{
				if ((crc & 0x01) != 0) // C# is strictly typed so you must make a comparison
				{
					crc = ((crc >> 1) ^ 0x8c) & 0xff;
				}
				else
				{
					crc = (crc >> 1) & 0xff;
				}
			}
		}//end generateNewCRC

		public byte getCrc()
		{
			return (byte)crc;
		}

		public void clear()
		{
			crc = 0;
		}
	}
}
