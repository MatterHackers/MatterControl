using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct tDeviceInfo
	{
		public uint c_iflag;
		public uint c_oflag;
		public uint c_cflag;
		public uint c_lflag;

		public fixed byte c_cc[20];
		public uint c_ispeed;
		public uint c_ospeed;
	}

	internal class FrostedSerialPortStream : Stream, IFrostedSerialStream, IDisposable
	{
		private int fd;
		private int read_timeout;
		private int write_timeout;
		private bool disposed;

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int open_serial(string portName);

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int set_attributes(int fd, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake);

		public FrostedSerialPortStream(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits,
				bool dtrEnable, bool rtsEnable, Handshake handshake, int readTimeout, int writeTimeout,
				int readBufferSize, int writeBufferSize)
		{
			fd = open_serial(portName);
			if (fd == -1)
			{
				ThrowIOException();
			}

			TryBaudRate(baudRate);

			int canSetAttributes = set_attributes(fd, baudRate, parity, dataBits, stopBits, handshake);

			if (canSetAttributes != 0)
			{
				throw new IOException(canSetAttributes.ToString()); // Probably Win32Exc for compatibility
			}

			read_timeout = readTimeout;
			write_timeout = writeTimeout;

			SetSignal(SerialSignal.Dtr, dtrEnable);

			if (handshake != Handshake.RequestToSend &&
					handshake != Handshake.RequestToSendXOnXOff)
			{
				SetSignal(SerialSignal.Rts, rtsEnable);
			}
		}

		private int SetupBaudRate(int baudRate)
		{
			throw new NotImplementedException();
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int tcgetattr(int fd, tDeviceInfo newtio);

		private int TCGetAttribute(int fd, tDeviceInfo newtio)
		{
			int result = tcgetattr(fd, newtio);
			return result;
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int tcsetattr(int fd, uint optional_actions, tDeviceInfo newtio);

		private int TCSetAttribute(int fd, uint optional_actions, tDeviceInfo newtio)
		{
			return tcsetattr(fd, optional_actions, newtio);
		}

		private int CFSetISpeed(tDeviceInfo newtio, int baudRate)
		{
			newtio.c_ispeed = (uint)baudRate;
			return (int)newtio.c_ispeed;
		}

		private int CFSetOSpeed(tDeviceInfo newtio, int baudRate)
		{
			newtio.c_ospeed = (uint)baudRate;
			return (int)newtio.c_ospeed;
		}

		private bool SetFrostedAttributes(int fd, int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake)
		{
			tDeviceInfo newtio = new tDeviceInfo();
			if (TCGetAttribute(fd, newtio) == -1)
			{
				return false;
			}

			newtio.c_cflag |= (uint)(e_c_oflag.CLOCAL | e_c_oflag.CREAD);
			// there is no defenition for e_c_lflag.ECHOL that I can find. It was in the list of or'ed flags below
			unchecked
			{
				newtio.c_lflag &= (uint)-(int)(e_c_lflag.ICANON | e_c_lflag.ECHO | e_c_lflag.ECHOE | e_c_lflag.ECHOK | e_c_lflag.ECHONL | e_c_lflag.ISIG | e_c_lflag.IEXTEN);
			}
			newtio.c_oflag &= (uint)(e_c_oflag.OPOST);
			newtio.c_iflag = (uint)e_c_iflag.IGNBRK;

			baudRate = SetupBaudRate(baudRate);

			unchecked
			{
				newtio.c_cflag &= (uint)-(uint)e_c_oflag.CSIZE;
			}

			switch (dataBits)
			{
				case 5:
					newtio.c_cflag |= (uint)e_c_oflag.CS5;
					break;

				case 6:
					newtio.c_cflag |= (uint)e_c_oflag.CS6;
					break;

				case 7:
					newtio.c_cflag |= (uint)e_c_oflag.CS6;
					break;

				case 8:
				default:
					newtio.c_cflag |= (uint)e_c_oflag.CS8;
					break;
			}

			switch (stopBits)
			{
				case StopBits.None:
					break;

				case StopBits.One:
					unchecked
					{
						newtio.c_cflag &= (uint)-(uint)e_c_oflag.CSTOPB;
					}
					break;

				case StopBits.Two:
					newtio.c_cflag |= (uint)e_c_oflag.CSTOPB;
					break;

				case StopBits.OnePointFive:
					break;
			}

			unchecked
			{
				newtio.c_iflag &= (uint)-(uint)(e_c_iflag.INPCK | e_c_iflag.ISTRIP);
			}

			switch (parity)
			{
				case Parity.None: /* None */
					newtio.c_cflag &= ~(uint)(e_c_oflag.PARENB | e_c_oflag.PARODD);
					break;

				case Parity.Odd: /* Odd */
					newtio.c_cflag |= (uint)(e_c_oflag.PARENB | e_c_oflag.PARODD);
					break;

				case Parity.Even: /* Even */
					newtio.c_cflag &= ~(uint)(e_c_oflag.PARODD);
					newtio.c_cflag |= (uint)(e_c_oflag.PARENB);
					break;

				case Parity.Mark: /* Mark */
					/* XXX unhandled */
					break;

				case Parity.Space: /* Space */
					/* XXX unhandled */
					break;
			}

			newtio.c_iflag &= ~(uint)(e_c_iflag.IXOFF | e_c_iflag.IXON);
#if CRTSCTS
			newtio.c_cflag &= ~CRTSCTS;
#endif //* def CRTSCTS */

			switch (handshake)
			{
				case Handshake.None: /* None */
					/* do nothing */
					break;

				case Handshake.RequestToSend: /* RequestToSend (RTS) */
#if CRTSCTS
				newtio.c_cflag |= CRTSCTS;
#endif //* def CRTSCTS */
					break;

				case Handshake.RequestToSendXOnXOff: /* RequestToSendXOnXOff (RTS + XON/XOFF) */
#if CRTSCTS
				newtio.c_cflag |= CRTSCTS;
#endif //* def CRTSCTS */
				/* fall through */
				case Handshake.XOnXOff: /* XOnXOff */
					newtio.c_iflag |= (uint)(e_c_iflag.IXOFF | e_c_iflag.IXON);
					break;
			}

			if (CFSetOSpeed(newtio, baudRate) < 0 || CFSetISpeed(newtio, baudRate) < 0 ||
				TCSetAttribute(fd, (uint)e_tcsetaatr.TCSANOW, newtio) < 0)
			{
				return false;
			}
			else
			{
				return true;
			}

			//return set_attributes(fd, baudRate, parity, dataBits, sb, hs);
		}

		public override bool CanRead
		{
			get
			{
				return true;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return true;
			}
		}

		public override bool CanTimeout
		{
			get
			{
				return true;
			}
		}

		public override int ReadTimeout
		{
			get
			{
				return read_timeout;
			}
			set
			{
				if (value < 0 && value != FrostedSerialPort.InfiniteTimeout)
					throw new ArgumentOutOfRangeException("value");

				read_timeout = value;
			}
		}

		public override int WriteTimeout
		{
			get
			{
				return write_timeout;
			}
			set
			{
				if (value < 0 && value != FrostedSerialPort.InfiniteTimeout)
					throw new ArgumentOutOfRangeException("value");

				write_timeout = value;
			}
		}

		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public override long Position
		{
			get
			{
				throw new NotSupportedException();
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public override void Flush()
		{
			// If used, this _could_ flush the serial port
			// buffer (not the SerialPort class buffer)
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int read_serial(int fd, byte[] buffer, int offset, int count);

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern bool poll_serial(int fd, out int error, int timeout);

		public override int Read([In, Out] byte[] buffer, int offset, int count)
		{
			CheckDisposed();
			if (buffer == null)
				throw new ArgumentNullException("buffer");
			if (offset < 0 || count < 0)
				throw new ArgumentOutOfRangeException("offset or count less than zero.");

			if (buffer.Length - offset < count)
				throw new ArgumentException("offset+count",
								  "The size of the buffer is less than offset + count.");

			int error;
			bool poll_result = poll_serial(fd, out error, read_timeout);
			if (error == -1)
				ThrowIOException();

			if (!poll_result)
			{
				// see bug 79735   http://bugzilla.ximian.com/show_bug.cgi?id=79735
				// should the next line read: return -1;
				throw new TimeoutException();
			}

			int result = read_serial(fd, buffer, offset, count);
			if (result == -1)
				ThrowIOException();
			return result;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int write_serial(int fd, byte[] buffer, int offset, int count, int timeout);

		public override void Write(byte[] buffer, int offset, int count)
		{
			CheckDisposed();
			if (buffer == null)
				throw new ArgumentNullException("buffer");

			if (offset < 0 || count < 0)
				throw new ArgumentOutOfRangeException();

			if (buffer.Length - offset < count)
				throw new ArgumentException("offset+count",
								 "The size of the buffer is less than offset + count.");

			// FIXME: this reports every write error as timeout
			if (write_serial(fd, buffer, offset, count, write_timeout) < 0)
				throw new TimeoutException("The operation has timed-out");
		}

		protected override void Dispose(bool disposing)
		{
			if (disposed)
				return;

			disposed = true;
			if (close_serial(fd) != 0)
			{
				//Don't do anything
			}
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int close_serial(int fd);

		public override void Close()
		{
			((IDisposable)this).Dispose();
		}

		void IDisposable.Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~FrostedSerialPortStream()
		{
			Dispose(false);
		}

		private void CheckDisposed()
		{
			if (disposed)
				throw new ObjectDisposedException(GetType().FullName);
		}

		public void SetAttributes(int baud_rate, Parity parity, int data_bits, StopBits sb, Handshake hs)
		{
			if (!SetFrostedAttributes(fd, baud_rate, parity, data_bits, sb, hs))
				ThrowIOException();
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int get_bytes_in_buffer(int fd, int input);

		public int BytesToRead
		{
			get
			{
				int result = get_bytes_in_buffer(fd, 1);
				if (result == -1)
					ThrowIOException();
				return result;
			}
		}

		public int BytesToWrite
		{
			get
			{
				int result = get_bytes_in_buffer(fd, 0);
				if (result == -1)
					ThrowIOException();
				return result;
			}
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int discard_buffer(int fd, bool inputBuffer);

		public void DiscardInBuffer()
		{
			if (discard_buffer(fd, true) != 0)
				ThrowIOException();
		}

		public void DiscardOutBuffer()
		{
			if (discard_buffer(fd, false) != 0)
				ThrowIOException();
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern SerialSignal get_signals(int fd, out int error);

		public SerialSignal GetSignals()
		{
			int error;
			SerialSignal signals = get_signals(fd, out error);
			if (error == -1)
				ThrowIOException();

			return signals;
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int set_signal(int fd, SerialSignal signal, bool value);

		public void SetSignal(SerialSignal signal, bool value)
		{
			if (signal < SerialSignal.Cd || signal > SerialSignal.Rts ||
					signal == SerialSignal.Cd ||
					signal == SerialSignal.Cts ||
					signal == SerialSignal.Dsr)
				throw new Exception("Invalid internal value");

			if (set_signal(fd, signal, value) == -1)
				ThrowIOException();
		}

		[DllImport("FrostedSerialHelper", SetLastError = true)]
		private static extern int breakprop(int fd);

		public void SetBreakState(bool value)
		{
			if (value)
				if (breakprop(fd) == -1)
					ThrowIOException();
		}

		[DllImport("libc")]
		private static extern IntPtr strerror(int errnum);

		private static void ThrowIOException()
		{
			int errnum = Marshal.GetLastWin32Error();
			string error_message = Marshal.PtrToStringAnsi(strerror(errnum));

			throw new IOException(error_message);
		}

		[DllImport("FrostedSerialHelper")]
		private static extern bool is_baud_rate_legal(int baud_rate);

		private void TryBaudRate(int baudRate)
		{
			if (!is_baud_rate_legal(baudRate))
			{
				// this kind of exception to be compatible with MSDN API
				throw new ArgumentOutOfRangeException("baudRate",
					"Given baud rate is not supported on this platform.");
			}
		}
	}
}