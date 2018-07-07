using System;

namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	public enum e_cc_c
	{
		/* Indices into c_cc array.  Default values in parentheses. POSIX Table 7-5. */
		VEOF = 0,	/* cc_c[VEOF] = EOF char (^D) */
		VEOL = 1,	/* cc_c[VEOL] = EOL char (undef) */
		VERASE = 2,	/* cc_c[VERASE] = ERASE char (^H) */
		VINTR = 3,	/* cc_c[VINTR] = INTR char (DEL) */
		VKILL = 4,	/* cc_c[VKILL] = KILL char (^U) */
		VMIN = 5,	/* cc_c[VMIN] = MIN value for timer */
		VQUIT = 6,	/* cc_c[VQUIT] = QUIT char (^\) */
		VTIME = 7,	/* cc_c[VTIME] = TIME value for timer */
		VSUSP = 8,	/* cc_c[VSUSP] = SUSP (^Z, ignored) */
		VSTART = 9,	/* cc_c[VSTART] = START char (^S) */
		VSTOP = 10,	/* cc_c[VSTOP] = STOP char (^Q) */
		//_POSIX_VDISABLE	  =(cc_t)0xFF,	/* You can't even generate this /*
		/* character with 'normal' keyboards.
		* But some language specific keyboards
		* can generate 0xFF. It seems that all
		* 256 are used, so cc_t should be a
		* short...
		*/

		SIZE = 20, /* size of cc_c array, some extra space * for extensions. */
	};

	/* Values for termios c_iflag bit map.  POSIX Table 7-2. */

	[Flags]
	public enum e_c_iflag
	{
		BRKINT = 0x0001,	/* signal interrupt on break */
		ICRNL = 0x0002,	/* map CR to NL on input */
		IGNBRK = 0x0004,	/* ignore break */
		IGNCR = 0x0008,	/* ignore CR */
		IGNPAR = 0x0010,	/* ignore characters with parity errors */
		INLCR = 0x0020,	/* map NL to CR on input */
		INPCK = 0x0040,	/* enable input parity check */
		ISTRIP = 0x0080,	/* mask off 8th bit */
		IXOFF = 0x0100,	/* enable start/stop input control */
		IXON = 0x0200,	/* enable start/stop output control */
		PARMRK = 0x0400,	/* mark parity errors in the input queue */
	};

	/* Values for termios c_oflag bit map.  POSIX Sec. 7.1.2.3. */

	[Flags]
	public enum e_c_oflag
	{
		OPOST = 0x0001,	/* perform output processing */

		/* Values for termios c_cflag bit map.  POSIX Table 7-3. */
		CLOCAL = 0x0001,	/* ignore modem status lines */
		CREAD = 0x0002,	/* enable receiver */
		CSIZE = 0x000C,	/* number of bits per character */
		CS5 = 0x0000,	/* if CSIZE is CS5, characters are 5 bits */
		CS6 = 0x0004,	/* if CSIZE is CS6, characters are 6 bits */
		CS7 = 0x0008,	/* if CSIZE is CS7, characters are 7 bits */
		CS8 = 0x000C,	/* if CSIZE is CS8, characters are 8 bits */
		CSTOPB = 0x0010,	/* send 2 stop bits if set, else 1 */
		HUPCL = 0x0020,	/* hang up on last close */
		PARENB = 0x0040,	/* enable parity on output */
		PARODD = 0x0080,	/* use odd parity if set, else even */
	};

	/* Values for termios c_lflag bit map.  POSIX Table 7-4. */

	[Flags]
	public enum e_c_lflag
	{
		ECHO = 0x0001,	/* enable echoing of input characters */
		ECHOE = 0x0002,	/* echo ERASE as backspace */
		ECHOK = 0x0004,	/* echo KILL */
		ECHONL = 0x0008,	/* echo NL */
		ICANON = 0x0010,	/* canonical input (erase and kill enabled) */
		IEXTEN = 0x0020,	/* enable extended functions */
		ISIG = 0x0040,	/* enable signals */
		NOFLSH = 0x0080,	/* disable flush after interrupt or quit */
		TOSTOP = 0x0100,	/* send SIGTTOU (job control, not implemented*/
	};

	/* Values for the baud rate settings.  POSIX Table 7-6. */

	[Flags]
	public enum e_baud_rate
	{
		B0 = 0x0000,	/* hang up the line */
		B50 = 0x1000,	/* 50 baud */
		B75 = 0x2000,	/* 75 baud */
		B110 = 0x3000,	/* 110 baud */
		B134 = 0x4000,	/* 134.5 baud */
		B150 = 0x5000,	/* 150 baud */
		B200 = 0x6000,	/* 200 baud */
		B300 = 0x7000,	/* 300 baud */
		B600 = 0x8000,	/* 600 baud */
		B1200 = 0x9000,	/* 1200 baud */
		B1800 = 0xA000,	/* 1800 baud */
		B2400 = 0xB000,	/* 2400 baud */
		B4800 = 0xC000,	/* 4800 baud */
		B9600 = 0xD000,	/* 9600 baud */
		B19200 = 0xE000,	/* 19200 baud */
		B38400 = 0xF000,	/* 38400 baud */
	};

	/* Optional actions for tcsetattr().  POSIX Sec. 7.2.1.2. */

	[Flags]
	public enum e_tcsetaatr
	{
		TCSANOW = 1,	/* changes take effect immediately */
		TCSADRAIN = 2,	/* changes take effect after output is done */
		TCSAFLUSH = 3,	/* wait for output to finish and flush input */
	};

	/* Queue_selector values for tcflush().  POSIX Sec. 7.2.2.2. */

	[Flags]
	public enum e_tcflush
	{
		TCIFLUSH = 1,	/* flush accumulated input data */
		TCOFLUSH = 2,	/* flush accumulated output data */
		TCIOFLUSH = 3,	/* flush accumulated input and output data */
	};

	/* Action values for tcflow().  POSIX Sec. 7.2.2.2. */

	[Flags]
	public enum e_tcflow
	{
		TCOOFF = 1,	/* suspend output */
		TCOON = 2,	/* restart suspended output */
		TCIOFF = 3,	/* transmit a STOP character on the line */
		TCION = 4,	/* transmit a START character on the line */
	};

	internal static class testCLass
	{
		private static void TestFunc()
		{
			uint c_cflag = 0;
			uint c_lflag = 0;
			uint c_oflag = 0;
			//uint c_iflag = 0;
			c_cflag |= (uint)(e_c_oflag.CLOCAL | e_c_oflag.CREAD);
			//c_lflag &= (uint)-(e_c_lflag.ICANON | e_c_lflag.ECHO | e_c_lflag.ECHOE | e_c_lflag.ECHOK | e_c_lflag.ECHOL | e_c_lflag.ECHONL | e_c_lflag.ISIG | e_c_lflag.IEXTEN);
			// not supported in docs I can find ECHOL
			unchecked
			{
				c_lflag &= (uint)-(uint)(e_c_lflag.ICANON | e_c_lflag.ECHO | e_c_lflag.ECHOE | e_c_lflag.ECHOK | e_c_lflag.ECHONL | e_c_lflag.ISIG | e_c_lflag.IEXTEN);
			}
			c_oflag &= (uint)(e_c_oflag.OPOST);
			//c_iflag = (uint)e_c_iflag.IGNBRK;
		}
	}
}