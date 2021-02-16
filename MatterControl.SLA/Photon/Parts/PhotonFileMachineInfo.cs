/*
 * MIT License
 *
 * Copyright (c) 2018 Bonosoft, 2021 Lars Brubaker c# port
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.IO;

/**
* by bn on 01/07/2018.
*/

namespace Photon.Parts
{
	public class PhotonFileMachineInfo
	{
		private readonly int infoByteSize;
		private readonly byte[] machineName = { };
		private readonly int machineNameAddress;
		private readonly int machineNameSize;
		private readonly int u1, u2, u3, u4, u5, u6, u7;
		private readonly int u8, u9, u10, u11, u12, u13, u14, u15, u16, u17;

		public PhotonFileMachineInfo(int address, int byteSize, byte[] file)
		{
			this.infoByteSize = byteSize;

			if (byteSize > 0)
			{
				byte[] data = ArraysEmulation.CopyOfRange(file, address, address + byteSize);

				var ds = new BinaryReader(new MemoryStream(data));
				{
					u1 = ds.ReadInt32();
					u2 = ds.ReadInt32();
					u3 = ds.ReadInt32();
					u4 = ds.ReadInt32();
					u5 = ds.ReadInt32();
					u6 = ds.ReadInt32();
					u7 = ds.ReadInt32();

					machineNameAddress = ds.ReadInt32();
					machineNameSize = ds.ReadInt32();

					u8 = ds.ReadInt32();
					u9 = ds.ReadInt32();
					u10 = ds.ReadInt32();
					u11 = ds.ReadInt32();
					u12 = ds.ReadInt32();
					u13 = ds.ReadInt32();
					u14 = ds.ReadInt32();
					u15 = ds.ReadInt32();
					u16 = ds.ReadInt32();
					u17 = ds.ReadInt32();
				}

				machineName = ArraysEmulation.CopyOfRange(file, machineNameAddress, machineNameAddress + machineNameSize);
			}
		}

		public int GetByteSize()
		{
			return infoByteSize + machineName.Length;
		}

		public void Save(BinaryWriter os, int startAddress)
		{
			if (infoByteSize > 0)
			{
				os.Write(u1);
				os.Write(u2);
				os.Write(u3);
				os.Write(u4);
				os.Write(u5);
				os.Write(u6);
				os.Write(u7);
				os.Write(startAddress + infoByteSize);
				os.Write(machineName.Length);
				os.Write(u8);
				os.Write(u9);
				os.Write(u10);
				os.Write(u11);
				os.Write(u12);
				os.Write(u13);
				os.Write(u14);
				os.Write(u15);
				os.Write(u16);
				os.Write(u17);
				os.Write(machineName);
			}
		}

		public void UnLink()
		{
		}
	}
}