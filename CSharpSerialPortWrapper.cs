/*
Copyright (c) 2019, Kevin Pope, John Lewin
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
using System.IO.Ports;

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	public class CSharpSerialPortWrapper : IFrostedSerialPort
	{
		private SerialPort port;

		public CSharpSerialPortWrapper(string serialPortName)
		{
			if (FrostedSerialPortFactory.GetAppropriateFactory("RepRap").IsWindows)
			{
				try
				{
					SerialPortFixer.Execute(serialPortName);
				}
				catch (Exception)
				{
				}
			}

			port = new SerialPort(serialPortName);
		}

		public int ReadTimeout
		{
			get => port.ReadTimeout;
			set => port.ReadTimeout = value;
		}

		public int WriteTimeout
		{
			get => port.WriteTimeout;
			set => port.WriteTimeout = value;
		}

		public int BaudRate
		{
			get => port.BaudRate;
			set => port.BaudRate = value;
		}

		public bool RtsEnable
		{
			get => port.RtsEnable;
			set => port.RtsEnable = value;
		}

		public bool DtrEnable
		{
			get => port.DtrEnable;
			set => port.DtrEnable = value;
		}

		public string ReadExisting() => port.ReadExisting();

		public int BytesToRead => port.BytesToRead;

		public void Dispose() => port.Dispose();

		public bool IsOpen => port.IsOpen;

		public void Open() => port.Open();

		public void Close()
		{
			try
			{
				port.Close();
			}
			catch (Exception)
			{
			}
		}

		public void Write(string str)
		{
			port.Write(str);
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			port.Write(buffer, offset, count);
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			return port.Read(buffer, offset, count);
		}
	}
}