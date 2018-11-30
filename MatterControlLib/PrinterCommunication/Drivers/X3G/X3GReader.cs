using System;
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	/********************************************************
     * S3G Response Packet Structure:
     * Index 0:StartBit
     * Index 1:packet length
     * Index 2+: Payload-
     *  PayLoad Index 0: Response Code(values 0x80 - 0x8C
     *  PayLoad Index 1+: Optional Response Arguments
     * Index (2+N): crc
     *******************************************************/
	public class X3GReader
	{
		private PrinterSettings settings;
		private X3GPrinterDetails printerDetails;
		private X3GPacketAnalyzer analyzer;

		public X3GReader(X3GPrinterDetails PtrDetails, PrinterSettings settings)
		{
			this.settings = settings;
			this.printerDetails = PtrDetails;
			analyzer = new X3GPacketAnalyzer(PtrDetails, settings);
		}

		public string translate(byte[] x3gResponse, string relatedGCommand, out bool commandOK)
		{
			//X3GPacketAnalyzer analyzer = new X3GPacketAnalyzer(,writerPtr);
			return analyzer.analyze(x3gResponse, relatedGCommand, out commandOK);
		}

		private class X3GPacketAnalyzer
		{
			private byte[] response;
			private PrinterSettings settings;
			private X3GCrc crc;
			private string gCommandForResponse; //Figure out better name. this is the gCommand that was sent to the printer that caused this response
			private X3GPrinterDetails printerDetails; //used to get location information and other needed response data
			private StringBuilder temperatureResponseStrBuilder; //Saves extruder temp when we have a heated bed to send back temps together

			public X3GPacketAnalyzer(X3GPrinterDetails PtrDetails, PrinterSettings settings)
			{
				this.settings = settings;
				crc = new X3GCrc();
				printerDetails = PtrDetails;
				temperatureResponseStrBuilder = new StringBuilder();
			}

			public string analyze(byte[] x3gResponse, string relatedGcommand, out bool commandOK)
			{
				response = x3gResponse;
				gCommandForResponse = relatedGcommand;
				StringBuilder gCodeResponse = new StringBuilder();
				int payloadLength;
				commandOK = false;
				if (response[0] == 0xD5)
				{
					payloadLength = response[1];
					gCodeResponse.Append(analyzePayload(payloadLength, out commandOK));
					checkCrc(payloadLength + 2);
				}

				gCodeResponse.Append("\n");

				return gCodeResponse.ToString();
			}

			private string analyzePayload(int payloadLength, out bool commandOK)
			{
				commandOK = false;
				StringBuilder payloadStrBuilder = new StringBuilder();
				switch (response[2])
				{
					case 0x81:
						payloadStrBuilder.Append("ok");
						commandOK = true;
						break;
					case 0x80:
						payloadStrBuilder.Append("Generic Packet Error, packet discarded");
						break;
					case 0x83:
						payloadStrBuilder.Append("CRC mismatch, packet discarded\n");
						payloadStrBuilder.Append("RS:" + X3GWriter.lineNumber + "\n");
						payloadStrBuilder.Append("ok");
						break;
					case 0x88:
						payloadStrBuilder.Append("Tool lock Timeout");
						break;
					case 0x89:
						payloadStrBuilder.Append("Cancel Build");
						break;
					case 0x8C:
						payloadStrBuilder.Append("Packet timeout error, packet discarded");
						payloadStrBuilder.Append("RS:" + X3GWriter.lineNumber + "\n");
						payloadStrBuilder.Append("ok");
						break;
					case 0x82://Action Buffer overflow, Packet Discarded (currently will request resend of line, later should be avoided by checking buffer size before send)
						payloadStrBuilder.Append("Action Buffer overflow, Packet Discarded\n");
						payloadStrBuilder.Append("RS:" + X3GWriter.lineNumber + "\n");
						payloadStrBuilder.Append("ok");
						break;
					case 0x84:
						payloadStrBuilder.Append("Query Packet too big, packet discarded");
						break;
					case 0x85:
						payloadStrBuilder.Append("Command not supported/recognized");
						break;
					case 0x87:
						payloadStrBuilder.Append("Downstream timeout");
						break;
					case 0x8A:
						payloadStrBuilder.Append("Bot is Building from SD");
						break;
					case 0x8B:
						payloadStrBuilder.Append("Bot is Shutdown due to Overheat");
						break;
					default:
						payloadStrBuilder.Append("Command Failed: " + response[2]);
						break;
				}

				switch (payloadLength)
				{
					case 23: //22 is the length of the get position response + 1 for response code
						if (printerDetails.currentPosition.Length != 0)//if we are not connecting just now to the printer we will report back the target move position
						{
							Vector3 printerPos = printerDetails.targetMovePosition;
							payloadStrBuilder.Append(String.Format(" C: X:{0} Y:{1} Z:{2} E:{3}", printerPos.X, printerPos.Y, printerPos.Z, 0));
						}
						else//if we have not told the printer to move yet we get the location the printer actually thinks it is at
						{
							Vector3 posFromPrinter = new Vector3();
							posFromPrinter.X = translateInt32(3);
							posFromPrinter.Y = translateInt32(7);
							posFromPrinter.Z = translateInt32(11);

							posFromPrinter.X = posFromPrinter.X / printerDetails.stepsPerMm.X;
							posFromPrinter.Y = posFromPrinter.Y / printerDetails.stepsPerMm.Y;
							posFromPrinter.Z = posFromPrinter.Z / printerDetails.stepsPerMm.Z;

							payloadStrBuilder.Append(String.Format(" C: X:{0} Y:{1} Z:{2} E:{3}", posFromPrinter.X, posFromPrinter.Y, posFromPrinter.Z, 0));
						}

						break;
					case 3: //Length of temperature response, temperature is requested individually for each extruder and bed separately. This collects the information and condenses it into one response to be sent to the printer
						if (!gCommandForResponse.Contains("M115"))
						{
							int temperature = translateInt16(3);
							printerDetails.teperatureResponseCount++;

							if (printerDetails.teperatureResponseCount == 1)
							{
								if (settings.GetValue<int>(SettingsKey.extruder_count) > 1)
								{
									temperatureResponseStrBuilder.Append(String.Format(" T0:{0}", temperature));
								}
								else
								{
									temperatureResponseStrBuilder.Append(String.Format(" T:{0}", temperature));
								}
							}
							else if (printerDetails.teperatureResponseCount == 2 && settings.GetValue<bool>(SettingsKey.has_heated_bed))
							{
								temperatureResponseStrBuilder.Append(String.Format(" B:{0}", temperature));
							}
							else
							{
								temperatureResponseStrBuilder.Append(String.Format(" T1:{0}", temperature));
							}

							if (printerDetails.teperatureResponseCount == printerDetails.requiredTemperatureResponseCount)
							{
								payloadStrBuilder.Append(temperatureResponseStrBuilder.ToString());
								temperatureResponseStrBuilder.Clear();
								printerDetails.teperatureResponseCount = 0;
							}
						}
						break;
				}

				for (int i = 2; i < payloadLength + 2; i++)
				{
					crc.update(response[i]);
				}

				return payloadStrBuilder.ToString();
			}

			private int translateInt16(int startingIndex)
			{
				return (response[startingIndex] + (response[startingIndex + 1] * 256));
			}

			private long translateInt32(int startingIndex)
			{
				return (long)(response[startingIndex] + (response[startingIndex + 1] * 256) + (response[startingIndex + 2] * 256 ^ 2) + (response[startingIndex + 3] * (256 ^ 3)));
			}

			private bool checkCrc(int crcIndex)
			{
				return crc.getCrc() == response[crcIndex];
			}
		}
	}
}
