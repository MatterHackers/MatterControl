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

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	public delegate void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e);

	public delegate void SerialPinChangedEventHandler(object sender, SerialPinChangedEventArgs e);

	public delegate void SerialErrorReceivedEventHandler(object sender, SerialErrorReceivedEventArgs e);

	public interface IFrostedSerialPort
	{
		bool RtsEnable { get; set; }

		bool DtrEnable { get; set; }

		int BaudRate { get; set; }

		int BytesToRead { get; }

		void Write(string str);

		void Write(byte[] buffer, int offset, int count);

		int WriteTimeout { get; set; }

		int ReadTimeout { get; set; }

		string ReadExisting();

		int Read(byte[] buffer, int offset, int count);

		bool IsOpen { get; }

		void Open();

		void Close();

		void Dispose();
	}

	internal enum SerialSignal
	{
		None = 0,
		Cd = 1, // Carrier detect
		Cts = 2, // Clear to send
		Dsr = 4, // Data set ready
		Dtr = 8, // Data terminal ready
		Rts = 16 // Request to send
	}

	public enum Handshake
	{
		None,
		XOnXOff,
		RequestToSend,
		RequestToSendXOnXOff
	}

	public enum StopBits
	{
		None,
		One,
		Two,
		OnePointFive
	}

	public enum Parity
	{
		None,
		Odd,
		Even,
		Mark,
		Space
	}

	public enum SerialError
	{
		RXOver = 1,
		Overrun = 2,
		RXParity = 4,
		Frame = 8,
		TXFull = 256
	}

	public enum SerialData
	{
		Chars = 1,
		Eof
	}

	public enum SerialPinChange
	{
		CtsChanged = 8,
		DsrChanged = 16,
		CDChanged = 32,
		Break = 64,
		Ring = 256
	}

	public class SerialDataReceivedEventArgs : EventArgs
	{
		internal SerialDataReceivedEventArgs(SerialData eventType)
		{
			this.eventType = eventType;
		}

		// properties

		public SerialData EventType
		{
			get
			{
				return eventType;
			}
		}

		private SerialData eventType;
	}

	public class SerialPinChangedEventArgs : EventArgs
	{
		internal SerialPinChangedEventArgs(SerialPinChange eventType)
		{
			this.eventType = eventType;
		}

		// properties

		public SerialPinChange EventType
		{
			get
			{
				return eventType;
			}
		}

		private SerialPinChange eventType;
	}

	public class SerialErrorReceivedEventArgs : EventArgs
	{
		internal SerialErrorReceivedEventArgs(SerialError eventType)
		{
			this.eventType = eventType;
		}

		// properties

		public SerialError EventType
		{
			get
			{
				return eventType;
			}
		}

		private SerialError eventType;
	}
}