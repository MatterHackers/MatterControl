using MatterHackers.MatterControl.Plugins.X3GDriver;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;

/*****************************************************
 * Initialization Requirements:
 * Steps per mm
 * Initialize Firmware to boot state
 * Get current position
 *****************************************************/

namespace MatterHackers.Plugins.X3GDriver
{
	class X3GSerialPortWrapper : IFrostedSerialPort
	{
		private IFrostedSerialPort port;
		private X3GPrinterDetails printerDetails;
		private X3GWriter writer;
		private X3GReader reader;
		private Queue<string> sentCommandQueue; //Keeps track of commands sent to printer to be used in translation (only keeps strings until printer responses to command)
		private string lastStr;
		private Stopwatch timeSinceLastCommandSent;
		private Stopwatch timeSinceLastOK;
		private StringBuilder readBuffer;
		private List<Byte> readPacket;
		private Queue<byte[]> outboundOverflowPackets;
		private bool waitForResponse;
		private bool dtrEnable;

		public X3GSerialPortWrapper(string serialPortName, PrinterSettings settings)
		{
			port = FrostedSerialPortFactory.GetAppropriateFactory("raw").Create(serialPortName, settings);
			printerDetails = new X3GPrinterDetails();
			writer = new X3GWriter(printerDetails, settings);
			reader = new X3GReader(printerDetails, settings);
			timeSinceLastCommandSent = new Stopwatch();
			timeSinceLastOK = new Stopwatch();
			sentCommandQueue = new Queue<string>();
			lastStr = "";
			readBuffer = new StringBuilder();
			readPacket = new List<byte>();
			outboundOverflowPackets = new Queue<byte[]>();
			waitForResponse = false;
		}

		public bool RtsEnable
		{
			get; set;
		}

		public bool DtrEnable
		{
			get { return dtrEnable; }
			set
			{
				dtrEnable = value;
				port.DtrEnable = value;
			}
		}

		public int BaudRate
		{
			get
			{
				return port.BaudRate;
			}
			set
			{
				port.BaudRate = value;
			}
		}

		public int BytesToRead
		{
			get
			{
				//To avoid spamming resends so fast that we lag mattercontrol we wait 20ms before checking response Also if there is an active dwell we wait the dwell duration before accepting the OK
				if (timeSinceLastCommandSent.ElapsedMilliseconds > 20 && timeSinceLastCommandSent.ElapsedMilliseconds > printerDetails.dwellTime)
				{
					printerDetails.dwellTime = 0;
					//Checks to see if the buffer has an entire packet before we try to translate it
					if (hasFullPacket())
					{
						bool returnedOK;
						string translatedReply = reader.translate(readPacket.ToArray(), lastStr, out returnedOK);
						waitForResponse = false;
						//After response is translated we check to see if the lockout of a M109 is active
						if (printerDetails.heatingLockout)
						{   //Check if the previous M105 has sent all of its associated packets (1-3 packets per M105)
							if (QueueIsEmpty())
							{


								//check to see if we have reached target temperature(s) and either disable the lock out or send another M105
								if (ExtruderIsReady(translatedReply) && BedIsReady(translatedReply) && secondExtruderIsReady(translatedReply))//Maker bot seems to stop the lockout when within 2 Degrees so we will match it
								{
									printerDetails.heatingLockout = false;
								}
								else
								{
									translatedReply = supressOk(translatedReply);//don't send an ok back until we are done heating
									bool temp;//Normally we check if we need to send this command to the printer but if a RS is requested it has to be sent
									byte[] output = writer.translate("M105\n", out temp);
									Write(output, 0, output.Length);
									foreach (byte[] packet in writer.GetAndClearOverflowPackets())
									{
										outboundOverflowPackets.Enqueue(packet);
									}

								}
								if (translatedReply != string.Empty)
								{
									readBuffer.Append(translatedReply);
								}
							}
							else
							{
								byte[] output = outboundOverflowPackets.Dequeue();
								Write(output, 0, output.Length);
							}


						}
						else
						{   //This handles resending when the printer's action buffer is full
							if (translatedReply.Contains("RS:"))
							{
								bool temp;
								byte[] output = writer.translate(lastStr, out temp);
								Write(output, 0, output.Length);
							}
							else
							{
								if (!QueueIsEmpty())//If there are overFlowPackets we need to send them to the printer before we continue by sending back an OK
								{
									byte[] output = outboundOverflowPackets.Dequeue();
									Write(output, 0, output.Length);
								}
								else
								{
									readBuffer.Append(translatedReply);
								}
								if (returnedOK)
								{
									writer.updateCurrentPosition();
								}
							}
						}

						readPacket.Clear();
					}
					else if (timeSinceLastCommandSent.ElapsedMilliseconds > 3000)
					{
						if (outboundOverflowPackets.Count > 0)
						{
							//If there 3seconds has passed since a response and there are outbound packets waiting in the queue we will send one assuming a packet was dropped
							byte[] output = outboundOverflowPackets.Dequeue();
							Write(output, 0, output.Length);
						}
						else if (waitForResponse) //We haven't gotten a response in 3 seconds and MC is waiting on an ok - Send a resend
						{
							readBuffer.Append("RS:" + X3GWriter.lineNumber + "\n");
							readBuffer.Append("ok");
						}
					}
				}
				return readBuffer.Length;
			}//end get
		}

		private bool secondExtruderIsReady(string translatedReply)
		{
			int extruderTemp = 0;
			int index = translatedReply.IndexOf("T1:");

			if (index != -1)
			{
				char[] whitespace = { ' ', '\n', '\t' };
				int i = translatedReply.IndexOfAny(whitespace, index);
				string str = translatedReply.Substring(index + 3, i - (index + 3));
				extruderTemp = int.Parse(str);
			}

			return (extruderTemp >= printerDetails.targetExtruderTemps[1] - 2);//Maker bot seems to stop the lockout when within 2 Degrees so we will match it
		}

		private bool ExtruderIsReady(string translatedReply)
		{
			int extruderTemp = 0;

			int index = translatedReply.IndexOf("T:");

			if (index != -1)
			{
				char[] whitespace = { ' ', '\n', '\t' };
				int i = translatedReply.IndexOfAny(whitespace, index);
				string str = translatedReply.Substring(index + 2, i - (index + 2));
				extruderTemp = int.Parse(str);
			}
			else
			{
				index = translatedReply.IndexOf("T0:");
				if (index != -1)
				{
					char[] whitespace = { ' ', '\n', '\t' };
					int i = translatedReply.IndexOfAny(whitespace, index);
					string str = translatedReply.Substring(index + 3, i - (index + 3));
					extruderTemp = int.Parse(str);
				}
			}

			return extruderTemp >= (printerDetails.targetExtruderTemps[0] - 2);//Maker bot seems to stop the lockout when within 2 Degrees so we will match it
		}

		private bool BedIsReady(string translatedReply)
		{
			int bedTemp = 0;
			bool isReady = true;

			int index = translatedReply.IndexOf("B:");

			if (index != -1)
			{
				char[] whitespace = { ' ', '\n', '\t' };
				int i = translatedReply.IndexOfAny(whitespace, index);
				string str = translatedReply.Substring(index + 2, i - (index + 2));
				bedTemp = int.Parse(str);
			}

			isReady = bedTemp >= (printerDetails.targetBedTemp - 2);
			if (isReady)
			{
				printerDetails.targetBedTemp = 0; //Flashforges seem to lose the ability to maintain this temperature, rather than locking them out forever we remove the requirement after reached once
			}

			return isReady;
		}

		private string supressOk(string translatedReply)
		{
			if (translatedReply.Contains("ok"))
			{
				translatedReply = translatedReply.Replace("ok", "");
			}
			return translatedReply;
		}

		private bool hasFullPacket()
		{
			bool result = false;
			int byteCount = port.BytesToRead;
			byte[] bytesRead;
			if (readPacket.Count > 0 && byteCount > 0)//if the readPacket already has values and the input buffer has bytes to read
			{

				if (byteCount > readPacket.ElementAt(1))
				{
					bytesRead = new byte[readPacket.ElementAt(1) + 1];

					port.Read(bytesRead, 0, readPacket.ElementAt(1) + 1);
					readPacket.AddRange(bytesRead);
					result = true;
				}

			}
			else if (byteCount > 2)//if the input buffer has bytes to read and you get here then we start filling the readPacket
			{

				bytesRead = new byte[2];
				port.Read(bytesRead, 0, 2);
				if (bytesRead[0] == 0xD5)//checks for start bit from printer
				{
					readPacket.AddRange(bytesRead);
					if (readPacket.ElementAt(1) < byteCount)//checks packet size against how full the buffer is
					{
						bytesRead = new byte[readPacket.ElementAt(1) + 1];
						port.Read(bytesRead, 0, readPacket.ElementAt(1) + 1);
						readPacket.AddRange(bytesRead);
						result = true;
					}
				}
				else
				{
					if (bytesRead[1] == 0xD5)//checks for start bit in second spot in case we somehow got a stray bit in here somehow
					{
						readPacket.Add(bytesRead[1]);//Add the start bit and retrieve packet length from the buffer (may need to check buffer size before reading)

						bytesRead = new byte[1];
						port.Read(bytesRead, 0, 1);
						readPacket.Add(bytesRead[0]);
						if (readPacket.ElementAt(1) < byteCount)//checks packet size against how full the buffer is
						{
							bytesRead = new byte[readPacket.ElementAt(1) + 1];
							port.Read(bytesRead, 0, readPacket.ElementAt(1) + 1);
							readPacket.AddRange(bytesRead);
							result = true;
						}
					}
				}

			}


			return result;
		}

		public void Write(string str)
		{
			bool sendToPrinter;
			sentCommandQueue.Enqueue(str.ToString());
			lastStr = str.ToString();
			byte[] output = writer.translate(str, out sendToPrinter);

			if (QueueIsEmpty() && !waitForResponse)
			{
				if (sendToPrinter)
				{
					Write(output, 0, output.Length);
				}
				else
				{
					fakeOk();
				}
			}
			else
			{
				if (sendToPrinter)
				{
					outboundOverflowPackets.Enqueue(output);
				}
				else
				{
					fakeOk();
				}
			}

			//certain gcode commands are translated to multiple x3g commands and the excess are queued up here
			foreach (byte[] packet in writer.GetAndClearOverflowPackets())
			{
				outboundOverflowPackets.Enqueue(packet);
			}


		}

		private bool QueueIsEmpty()
		{
			return outboundOverflowPackets.Count < 1;
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			port.Write(buffer, offset, count);
			timeSinceLastCommandSent.Restart();
			waitForResponse = true;
		}

		public int WriteTimeout
		{
			get
			{ return port.WriteTimeout; }
			set
			{ port.WriteTimeout = value; }
		}

		public int ReadTimeout
		{
			get { return port.ReadTimeout; }
			set { port.ReadTimeout = value; }
		}

		public string ReadExisting()//Translate via Reader
		{
			string tempString = readBuffer.ToString();
			readBuffer.Clear();
			return tempString;
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			return port.Read(buffer, offset, count);
		}

		public bool IsOpen
		{
			get { return port.IsOpen; }
		}

		public void Open()
		{
			port.Open();
		}

		public void Close()
		{
			port.Close();
		}

		public void Dispose()
		{
			port.Dispose();
		}

		private void fakeOk()
		{
			readBuffer.Append("ok\n");
		}

	}
}
