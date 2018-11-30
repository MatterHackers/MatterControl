using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

//Protocol Documentation found at: https://github.com/makerbot/s3g/blob/master/doc/s3gProtocol.md

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	public class X3GWriter
	{
		private PrinterSettings settings;
		private X3GPrinterDetails printerDetails;

		private Queue<byte[]> overFlowPackets;

		private bool relativePos = false;
		private int feedrate;
		//private int activeExtruderIndex;

		public static int lineNumber;

		public X3GWriter()
		{
			overFlowPackets = new Queue<byte[]>();
			printerDetails = new X3GPrinterDetails();
			feedrate = 3200;
			printerDetails.activeExtruderIndex = 0;
			lineNumber = 0;
		}

		public X3GWriter(X3GPrinterDetails printerInfo, PrinterSettings settings)
		{
			this.settings = settings;
			printerDetails = printerInfo;
			overFlowPackets = new Queue<byte[]>();
			feedrate = 3200;
			printerDetails.activeExtruderIndex = 0;
			lineNumber = 0;
		}

		public byte[] translate(string writemessage, out bool sendToPrinter)
		{
			byte[] convertedMessage = new byte[] { 0 };
			List<string> commands;
			X3GPacketFactory binaryPacket;
			char commandType = writemessage[0];
			sendToPrinter = true;

			if (commandType == 'N') //Strips leading line number and post command checksum
			{

				lineNumber = (int)getParameterValue(writemessage, 'N');
				int start = writemessage.IndexOf(' ', 0) + 1;
				int checksumIndex = writemessage.IndexOf('*');
				if (checksumIndex > 0)
				{
					writemessage = writemessage.Substring(start, checksumIndex - start);
				}
				else
				{
					writemessage = writemessage.Substring(start);
				}
				writemessage += "\r\n";
				commandType = writemessage[0];
			}

			commands = parseGcode(writemessage); //gcode is parsed into a list of strings each corresponding to a parameter (example: G1X10Y38.5 => G1,X10,Y38.5)

			//Convert Connect message to X3G
			switch (commandType)
			{
				case 'M':

					int commandVal = (int)getParameterValue(commands, 'M');
					switch (commandVal)
					{
						case 73://Set Build Perc M73
							binaryPacket = new X3GPacketFactory(150);
							binaryPacket.addByte((byte)getParameterValue(commands, 'P'));
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 82://set extruder to absolute move M82
							sendToPrinter = false;
							printerDetails.extruderRelativePos = false;
							break;

						case 83://set extruder to relative move M83
							sendToPrinter = false;
							printerDetails.extruderRelativePos = true;
							break;

						case 84://Stop idle hold (release motors) M84
							binaryPacket = new X3GPacketFactory(137);
							binaryPacket.addByte(31);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 92://set axis steps per unit M92
							sendToPrinter = false;
							updateStepsPerMm(commands);

							break;

						case 114://Get Current Position M114
							binaryPacket = new X3GPacketFactory(21);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 115://connecting M115
							binaryPacket = new X3GPacketFactory(0x00);
							binaryPacket.add16bits(0x28);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 105://get temperature M105
							binaryPacket = new X3GPacketFactory(0x0A);
							binaryPacket.addByte(0x00);
							binaryPacket.addByte(0x02);
							convertedMessage = binaryPacket.getX3GPacket();
							printerDetails.requiredTemperatureResponseCount = 1;

							if (settings.GetValue<bool>(SettingsKey.has_heated_bed))//if it has a bed get the bed temp
							{
								binaryPacket = new X3GPacketFactory(10);
								binaryPacket.addByte(0);
								binaryPacket.addByte(30);
								printerDetails.requiredTemperatureResponseCount++;
								overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
							}

							if (settings.GetValue<int>(SettingsKey.extruder_count) > 1)
							{
								binaryPacket = new X3GPacketFactory(10);
								binaryPacket.addByte(1);
								binaryPacket.addByte(2);
								printerDetails.requiredTemperatureResponseCount++;
								overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
							}
							printerDetails.teperatureResponseCount = 0;
							break;

						case 104://set extruder temperature M104
							int temp = (int)getParameterValue(commands, 'S');
							byte extruder = (byte)getParameterValue(commands, 'T');
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(0x03);
							binaryPacket.addByte(0x02);
							binaryPacket.add16bits(temp);

							convertedMessage = binaryPacket.getX3GPacket();

							binaryPacket = new X3GPacketFactory(136);//turns on cooling fan
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(12);
							binaryPacket.addByte(1);
							binaryPacket.addByte(1);

							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							printerDetails.targetTempForMakerbotStyleCommands = temp;
							break;

						case 109://set extruder temperature and wait M109
							temp = (int)getParameterValue(commands, 'S');
							extruder = (byte)getParameterValue(commands, 'T');
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(0x03);
							binaryPacket.addByte(0x02);
							binaryPacket.add16bits(temp);
							convertedMessage = binaryPacket.getX3GPacket();

							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(12);
							binaryPacket.addByte(1);
							binaryPacket.addByte(1);

							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							binaryPacket = new X3GPacketFactory(135);
							binaryPacket.addByte(0x00);
							binaryPacket.add16bits(100);//delay between query packets in ms
							binaryPacket.add16bits(1200);//timeout before continuing w/o  tool ready in seconds

							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							printerDetails.targetExtruderTemps[extruder] = temp;
							printerDetails.heatingLockout = true;

							break;

						case 106://Fan On M106
							int zeroCheck = (int)getParameterValue(commands, 'S');
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(0x00);
							binaryPacket.addByte(13);
							binaryPacket.addByte(1);

							if (zeroCheck > 0)
							{
								binaryPacket.addByte(1);//If the value is not zero enable motor
							}
							else
							{
								binaryPacket.addByte(0);//If value is zero disable motor
							}

							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 107://Fan off M107
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(0x00);
							binaryPacket.addByte(13);
							binaryPacket.addByte(1);
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 110://set current line number M110
							lineNumber = (int)getParameterValue(commands, 'N');
							sendToPrinter = false;
							break;

						case 117://Set Display message M117
							binaryPacket = new X3GPacketFactory(149);
							binaryPacket.addByte(4);
							binaryPacket.addByte(0);
							binaryPacket.addByte(0);
							binaryPacket.addByte(20); //20 second timeout on message
							for (int i = 1; i < commands.Count; i++)
							{
								byte b = Convert.ToByte(commands.ElementAt(i)[0]);
								binaryPacket.addByte(b);
							}
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();

							break;

						case 127://Disable extra output(fan) Makerbot M127
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(0x00);
							binaryPacket.addByte(13);
							binaryPacket.addByte(1);
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 132://load axis offset of current home pos Makerbot M132

							binaryPacket = new X3GPacketFactory(144);
							binaryPacket.addByte(31);

							convertedMessage = binaryPacket.getX3GPacket();

							break;

						case 133://wait for toolhead to heat to target temp Makerbot M133
							temp = printerDetails.targetTempForMakerbotStyleCommands;
							extruder = (byte)getParameterValue(commands, 'T');
							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(0x03);
							binaryPacket.addByte(0x02);
							binaryPacket.add16bits(temp);
							convertedMessage = binaryPacket.getX3GPacket();

							binaryPacket = new X3GPacketFactory(136);
							binaryPacket.addByte(extruder);
							binaryPacket.addByte(12);
							binaryPacket.addByte(1);
							binaryPacket.addByte(1);

							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							binaryPacket = new X3GPacketFactory(135);
							binaryPacket.addByte(0x00);
							binaryPacket.add16bits(100);//delay between query packets in ms
							binaryPacket.add16bits(1200);//timeout before continuing w/o  tool ready in seconds

							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							printerDetails.targetExtruderTemps[extruder] = temp;
							printerDetails.heatingLockout = true;
							break;

						case 134://wait for build platform temp Makerbot M134
							if (settings.GetValue<bool>(SettingsKey.has_heated_bed))
							{
								binaryPacket = new X3GPacketFactory(136);
								binaryPacket.addByte(0);
								binaryPacket.addByte(31);
								binaryPacket.addByte(2);
								binaryPacket.add16bits(printerDetails.targetBedTemp);
							}
							else
							{
								sendToPrinter = false;
							}

							break;
						case 135://change toolhead Makerbot M135
							printerDetails.activeExtruderIndex = (byte)getParameterValue(commands, 'T');
							//Swaps active&inactive toolheads
							float switchPositionHolder = printerDetails.activeExtruderPosition;
							printerDetails.activeExtruderPosition = printerDetails.inactiveExtruderPosition;
							printerDetails.inactiveExtruderPosition = switchPositionHolder;
							//sends toolchange command to printer
							binaryPacket = new X3GPacketFactory(134);
							binaryPacket.addByte(printerDetails.activeExtruderIndex);

							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 140://Set Bed temp M140
							if (settings.GetValue<bool>(SettingsKey.has_heated_bed))
							{
								int temperature = (int)getParameterValue(commands, 'S');
								binaryPacket = new X3GPacketFactory(136);
								binaryPacket.addByte(0);
								binaryPacket.addByte(31);
								binaryPacket.addByte(2);
								binaryPacket.add16bits(temperature);

								convertedMessage = binaryPacket.getX3GPacket();
								printerDetails.targetBedTemp = temperature;
							}
							else
							{
								sendToPrinter = false;
							}

							break;

						case 190://Wait for bed to reach target temp M190
							if (settings.GetValue<bool>(SettingsKey.has_heated_bed))
							{
								int temperature = (int)getParameterValue(commands, 'S');
								binaryPacket = new X3GPacketFactory(136);
								binaryPacket.addByte(0);
								binaryPacket.addByte(31);
								binaryPacket.addByte(2);
								binaryPacket.add16bits(temperature);

								convertedMessage = binaryPacket.getX3GPacket();
								printerDetails.targetBedTemp = temperature;

								binaryPacket = new X3GPacketFactory(141);
								binaryPacket.addByte(0);
								binaryPacket.add16bits(100);
								binaryPacket.add16bits(1200);

								overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
								printerDetails.heatingLockout = true;
							}
							else
							{
								sendToPrinter = false;
							}

							break;

						case 206://Positional offset for bed M206
							sendToPrinter = false;
							updateBedOffset(commands);

							break;

						//The following are fake gcode commands to do features that are not included in gCode or are needed for printer initialization
						case 1200: //Build Start Notification M1200
							binaryPacket = new X3GPacketFactory(153);
							binaryPacket.add32bits(0);
							for (int i = 1; i < commands.Count; i++)
							{
								byte b = Convert.ToByte(commands.ElementAt(i)[0]);
								binaryPacket.addByte(b);
							}
							//binaryPacket.addByte(77);
							//binaryPacket.addByte(67);
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 1201: //Build End Notification M1201
							binaryPacket = new X3GPacketFactory(154);
							binaryPacket.addByte(0);
							convertedMessage = binaryPacket.getX3GPacket();
							printerDetails.heatingLockout = false;
							printerDetails.targetBedTemp = 0;
							printerDetails.targetExtruderTemps[0] = 0;
							printerDetails.targetExtruderTemps[1] = 0;
							break;

						case 1202: //dtr hi-low (reset) M1202
							binaryPacket = new X3GPacketFactory(3);
							convertedMessage = binaryPacket.getX3GPacket();
							break;

						case 1203: //toolhead offset M1203
							sendToPrinter = false;
							printerDetails.extruderOffset = new Vector2(getParameterValue(commands, 'X'), getParameterValue(commands, 'Y'));
							break;

						default:
							sendToPrinter = false;
							convertedMessage = new byte[] { 0 };
							break;
					}

					break;

				case 'G':
					int commandValue = getCommandValue(commands[0]);

					switch (commandValue)
					{
						case 0:
						case 1://Move G0 Xnnn Ynnn Znnn Ennn Fnnn Snnn G0/G1

							if (FeedrateOnly(writemessage))
							{
								sendToPrinter = false;
								updateFeedRate((int)getParameterValue(commands, 'F'));
							}
							else
							{
								if (!relativePos)
								{
									binaryPacket = new X3GPacketFactory(155);//Host command code
									updateTargetPostition(commands);
									updateFeedRate((int)getParameterValue(commands, 'F'));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.X) * printerDetails.stepsPerMm.X));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.Y) * printerDetails.stepsPerMm.Y));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.Z) * printerDetails.stepsPerMm.Z));
									if (printerDetails.activeExtruderIndex == 0)//checks which extruder is active and
									{
										binaryPacket.add32bits((long)((printerDetails.targetExtruderPosition) * printerDetails.extruderStepsPerMm));//First extruder
										binaryPacket.add32bits((long)(printerDetails.inactiveExtruderPosition * printerDetails.extruderStepsPerMm)); //second extruder
									}
									else
									{
										binaryPacket.add32bits((long)(printerDetails.inactiveExtruderPosition * printerDetails.extruderStepsPerMm));
										binaryPacket.add32bits((long)((printerDetails.targetExtruderPosition) * printerDetails.extruderStepsPerMm));//second extruder
									}

									binaryPacket.add32bits((long)(feedrate * (printerDetails.stepsPerMm.X / 60)));//feedrate in steps/second                                
									binaryPacket.addByte(getRelativeMovementAxes(commands));//specifies which axes should make a relative move:none (0)
									float move = CalculateMoveInMM(commands);
									binaryPacket.addFloat(move);//this calculates time(needs length of the move target - current and get magnitude) (expected in mm)
									binaryPacket.add16bits((feedrate / 60) * 64);//feedrate(mm/s) mult by 64 used with above float to calc time

									//Update of the position is now down when an OK is returned from the printer

									convertedMessage = binaryPacket.getX3GPacket();
								}
								else
								{
									binaryPacket = new X3GPacketFactory(155);
									updateTargetPostition(commands);
									updateFeedRate((int)getParameterValue(commands, 'F'));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.X) * printerDetails.stepsPerMm.X));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.Y) * printerDetails.stepsPerMm.Y));
									binaryPacket.add32bits((long)((printerDetails.targetMovePosition.Z) * printerDetails.stepsPerMm.Z));
									if (printerDetails.activeExtruderIndex == 0)
									{
										binaryPacket.add32bits((long)(printerDetails.targetExtruderPosition * printerDetails.extruderStepsPerMm));
										binaryPacket.add32bits(0);
									}
									else
									{
										binaryPacket.add32bits(0);
										binaryPacket.add32bits((long)(printerDetails.targetExtruderPosition * printerDetails.extruderStepsPerMm));
									}


									binaryPacket.add32bits((long)(feedrate * (printerDetails.stepsPerMm.X) / 60));
									binaryPacket.addByte(getRelativeMovementAxes(commands));//specifies which axes should make a relative move: all(31)
									float move = CalculateMoveInMM(commands);
									binaryPacket.addFloat(move);
									binaryPacket.add16bits((feedrate / 60) * 64);

									printerDetails.targetMovePosition.X = printerDetails.currentPosition.X + printerDetails.targetMovePosition.X;
									printerDetails.targetMovePosition.Y = printerDetails.currentPosition.Y + printerDetails.targetMovePosition.Y;
									printerDetails.targetMovePosition.Z = printerDetails.currentPosition.Z + printerDetails.targetMovePosition.Z;

									convertedMessage = binaryPacket.getX3GPacket();
								}
							}


							break;
						case 2:
						case 3://Controlled Arc Move G2 Xnnn Ynnn Innn Jnnn Ennn Fnnn (clockwise arc) G3 (counter-Clockwise)
							break;
						case 28://Move to Origin (home) G28 (Flags)X Y Z
							byte axesBitfeild = 0;
							if (commandHasNoParameters(commands))//If there are no parameters home all axes
							{
								binaryPacket = new X3GPacketFactory(132);
								binaryPacket.addByte(3);
								binaryPacket.add32bits((long)printerDetails.homingFeedRate.X);
								binaryPacket.add16bits(45);//Time out in seconds
								convertedMessage = binaryPacket.getX3GPacket();
								printerDetails.targetMovePosition.X = printerDetails.positionalOffset.X;
								printerDetails.targetMovePosition.Y = printerDetails.positionalOffset.Y;
								printerDetails.targetMovePosition.Z = printerDetails.positionalOffset.Z;
								axesBitfeild = 7;
							}
							else//Otherwise check for which axes should be homed
							{
								if (checkCommandForFlag(writemessage, 'X'))
								{
									axesBitfeild = 1;
									printerDetails.targetMovePosition.X = printerDetails.positionalOffset.X;
								}
								if (checkCommandForFlag(writemessage, 'Y'))
								{
									axesBitfeild += 2;
									printerDetails.targetMovePosition.Y = printerDetails.positionalOffset.Y;
								}
								binaryPacket = new X3GPacketFactory(132);
								binaryPacket.addByte(axesBitfeild);
								binaryPacket.add32bits((long)printerDetails.homingFeedRate.X);
								binaryPacket.add16bits(45);//Time out
								convertedMessage = binaryPacket.getX3GPacket();
								if (checkCommandForFlag(writemessage, 'Z'))
								{
									axesBitfeild += 4;
									printerDetails.targetMovePosition.Z = printerDetails.positionalOffset.Z;
								}
							}

							if (axesBitfeild > 3 || axesBitfeild == 0)//handles Z homing
							{
								binaryPacket = new X3GPacketFactory(131); //This Will Home Z if it is specified
								binaryPacket.addByte(0x04);
								binaryPacket.add32bits((long)printerDetails.homingFeedRate.Z);
								binaryPacket.add16bits(45);
								overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
							}

							printerDetails.currentPosition.X = printerDetails.targetMovePosition.X;
							printerDetails.currentPosition.Y = printerDetails.targetMovePosition.Y;
							printerDetails.currentPosition.Z = printerDetails.targetMovePosition.Z;
							printerDetails.activeExtruderPosition = 0;
							printerDetails.inactiveExtruderPosition = 0;
							printerDetails.targetExtruderPosition = 0;

							//Set position of homed position the inverse of the printer offset (this will turn 0,0 for the printer to be the same as what MC expects)
							binaryPacket = new X3GPacketFactory(140);
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.X * printerDetails.stepsPerMm.X));
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.Y * printerDetails.stepsPerMm.Y));
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.Z * printerDetails.stepsPerMm.Z));
							binaryPacket.add32bits(0);
							binaryPacket.add32bits(0);
							overFlowPackets.Enqueue(binaryPacket.getX3GPacket());

							break;
						case 29://Detailed Z-Probe G29
							break;
						case 30://Single Z-Probe G30
							break;
						case 4://Dwell G4 Pnnn or Snnn (P = milliseconds S = seconds)
							binaryPacket = new X3GPacketFactory(0x85);
							long i = (long)getParameterValue(commands, 'P');
							if (i == 0)
							{
								i = (long)(getParameterValue(commands, 'S') * 1000);
							}
							printerDetails.dwellTime = i;
							binaryPacket.add32bits(i);
							convertedMessage = binaryPacket.getX3GPacket();
							break;
						case 10://Retract G10
							break;
						case 11://UnRetract G11
							break;
						case 20://set units to inches G20
							break;
						case 21://set units to Millimeters G21
							sendToPrinter = false;
							break;
						case 90://Set to Absolute Positioning G90
							relativePos = false;
							printerDetails.extruderRelativePos = false;
							sendToPrinter = false;
							break;
						case 91://set to Relative Positioning G91
							relativePos = true;
							printerDetails.extruderRelativePos = true;
							sendToPrinter = false;
							break;
						case 92://Set Position G92 Xnnn Ynnn Znnn Ennn
							binaryPacket = new X3GPacketFactory(0x8C);
							updateTargetPostition(commands);
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.X * printerDetails.stepsPerMm.X));
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.Y * printerDetails.stepsPerMm.Y));
							binaryPacket.add32bits((long)(printerDetails.targetMovePosition.Z * printerDetails.stepsPerMm.Z));
							if (printerDetails.activeExtruderIndex == 0)
							{
								binaryPacket.add32bits((long)(printerDetails.targetExtruderPosition * printerDetails.extruderStepsPerMm));
								binaryPacket.add32bits((long)(printerDetails.inactiveExtruderPosition * printerDetails.extruderStepsPerMm));
							}
							else
							{
								binaryPacket.add32bits((long)(printerDetails.inactiveExtruderPosition * printerDetails.extruderStepsPerMm));
								binaryPacket.add32bits((long)(printerDetails.targetExtruderPosition * printerDetails.extruderStepsPerMm));
							}

							printerDetails.currentPosition = new Vector3(printerDetails.targetMovePosition); //sets the current position to the targeted move position
							printerDetails.activeExtruderPosition = printerDetails.targetExtruderPosition;

							convertedMessage = binaryPacket.getX3GPacket();
							break;
						case 130://Set digital Potentiometer G130 Xnn Ynn Znn Ann Bnn
							if (checkCommandForFlag(commands, 'X'))
							{
								binaryPacket = new X3GPacketFactory(145);
								binaryPacket.addByte(0);
								binaryPacket.addByte((byte)getParameterValue(commands, 'X'));
								convertedMessage = binaryPacket.getX3GPacket();
							}
							if (checkCommandForFlag(commands, 'Y'))
							{
								binaryPacket = new X3GPacketFactory(145);
								binaryPacket.addByte(1);
								binaryPacket.addByte((byte)getParameterValue(commands, 'Y'));
								if (convertedMessage != null)
								{
									overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
								}
								else
								{
									convertedMessage = binaryPacket.getX3GPacket();
								}
							}
							if (checkCommandForFlag(commands, 'Z'))
							{
								binaryPacket = new X3GPacketFactory(145);
								binaryPacket.addByte(2);
								binaryPacket.addByte((byte)getParameterValue(commands, 'Z'));
								if (convertedMessage != null)
								{
									overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
								}
								else
								{
									convertedMessage = binaryPacket.getX3GPacket();
								}
							}
							if (checkCommandForFlag(commands, 'A'))
							{
								binaryPacket = new X3GPacketFactory(145);
								binaryPacket.addByte(3);
								binaryPacket.addByte((byte)getParameterValue(commands, 'A'));
								if (convertedMessage != null)
								{
									overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
								}
								else
								{
									convertedMessage = binaryPacket.getX3GPacket();
								}
							}
							if (checkCommandForFlag(commands, 'B'))
							{
								binaryPacket = new X3GPacketFactory(145);
								binaryPacket.addByte(4);
								binaryPacket.addByte((byte)getParameterValue(commands, 'B'));
								if (convertedMessage != null)
								{
									overFlowPackets.Enqueue(binaryPacket.getX3GPacket());
								}
								else
								{
									convertedMessage = binaryPacket.getX3GPacket();
								}
							}
							break;
						case 161://Home axis to minimum G161 Z Fnnn
							double targetFeedrate = getParameterValue(commands, 'F');
							if (targetFeedrate < printerDetails.homingFeedRate.Z)
							{
								targetFeedrate = printerDetails.homingFeedRate.Z;
							}
							binaryPacket = new X3GPacketFactory(131);
							binaryPacket.addByte(0x04);
							binaryPacket.add32bits((long)targetFeedrate);
							binaryPacket.add16bits(45);
							convertedMessage = binaryPacket.getX3GPacket();
							//Set positional details
							printerDetails.targetMovePosition.Z = printerDetails.positionalOffset.Z;
							printerDetails.currentPosition.Z = printerDetails.targetMovePosition.Z;

							break;
						case 162://Home axis to maximum G162 X Y Fnnn
							targetFeedrate = getParameterValue(commands, 'F');
							if (targetFeedrate < printerDetails.homingFeedRate.X)
							{
								targetFeedrate = printerDetails.homingFeedRate.X;
							}
							binaryPacket = new X3GPacketFactory(132);
							binaryPacket.addByte(0x03);
							binaryPacket.add32bits((long)targetFeedrate);
							binaryPacket.add16bits(45);
							convertedMessage = binaryPacket.getX3GPacket();
							//Set positional details
							printerDetails.targetMovePosition.X = printerDetails.positionalOffset.X;
							printerDetails.targetMovePosition.Y = printerDetails.positionalOffset.Y;
							printerDetails.currentPosition.X = printerDetails.targetMovePosition.X;
							printerDetails.currentPosition.Y = printerDetails.targetMovePosition.Y;

							break;
						default:
							sendToPrinter = false;
							convertedMessage = new byte[] { 0 };
							break;
					}

					break;

				case 'T'://Change toolhead
					printerDetails.activeExtruderIndex = (byte)getParameterValue(commands, 'T');
					//Swaps active&inactive toolheads
					float positionHolder = printerDetails.activeExtruderPosition;
					printerDetails.activeExtruderPosition = printerDetails.inactiveExtruderPosition;
					printerDetails.inactiveExtruderPosition = positionHolder;
					//sends toolchange command to printer
					binaryPacket = new X3GPacketFactory(134);
					binaryPacket.addByte(printerDetails.activeExtruderIndex);

					convertedMessage = binaryPacket.getX3GPacket();
					break;
				case 'X'://Used to test binary commands typed in via terminal(ex. Home x&y: X132 B03 L500 I30)
					writemessage = writemessage.Substring(1);
					int arraySize = commands.Count;

					binaryPacket = new X3GPacketFactory((byte)getParameterValue(commands, 'X'));

					for (int i = 1; i < arraySize; i++)
					{
						if (commands.ElementAt(i) != null && commands.ElementAt(i) != "")
						{
							char c = commands[i][0];
							commands[i] = commands[i].Substring(1);
							switch (c)
							{
								case 'b':
								case 'B':
									binaryPacket.addByte(Byte.Parse(commands[i]));
									break;
								case 'i':
								case 'I':
									binaryPacket.add16bits(int.Parse(commands[i]));
									break;
								case 'l':
								case 'L':
									binaryPacket.add32bits(long.Parse(commands[i]));
									break;
							}
						}

					}

					convertedMessage = binaryPacket.getX3GPacket();
					break;
				default:
					convertedMessage = new byte[] { 0 };
					sendToPrinter = false;
					break;
			}//End Switch


			return convertedMessage;
		}

		private byte getRelativeMovementAxes(List<string> commands)
		{
			byte axes = 0;//defaults all to absolute
			if (relativePos)
			{
				axes = 31;//sets all movements axes to be relative
			}
			else if (printerDetails.extruderRelativePos)
			{
				axes = 24;//sets both extruders to be relative moves
			}

			return axes;
		}

		private void updateBedOffset(List<string> commands)
		{
			float offSet;
			if (checkCommandForFlag(commands, 'X'))
			{
				offSet = getParameterValue(commands, 'X');
				printerDetails.positionalOffset.X = offSet;
			}

			if (checkCommandForFlag(commands, 'Y'))
			{
				offSet = getParameterValue(commands, 'Y');
				printerDetails.positionalOffset.Y = offSet;

			}

			if (checkCommandForFlag(commands, 'Z'))
			{
				offSet = getParameterValue(commands, 'Z');
				printerDetails.positionalOffset.Z = offSet;

			}
		}

		private void updateStepsPerMm(List<string> commands)
		{
			float steps = getParameterValue(commands, 'X');
			if (steps != 0)
			{
				printerDetails.stepsPerMm.X = steps;
			}
			steps = getParameterValue(commands, 'Y');
			if (steps != 0)
			{
				printerDetails.stepsPerMm.Y = steps;
			}
			steps = getParameterValue(commands, 'Z');
			if (steps != 0)
			{
				printerDetails.stepsPerMm.Z = steps;
			}
			steps = getParameterValue(commands, 'E');
			if (steps != 0)
			{
				printerDetails.extruderStepsPerMm = (long)steps;
			}
		}

		public void updateCurrentPosition()
		{
			printerDetails.currentPosition = new Vector3(printerDetails.targetMovePosition); //sets the current position to the targeted move position
			printerDetails.activeExtruderPosition = printerDetails.targetExtruderPosition;
		}

		public Queue<byte[]> GetAndClearOverflowPackets()
		{
			Queue<byte[]> temp = new Queue<byte[]>(overFlowPackets);
			overFlowPackets.Clear();

			return temp;
		}

		private bool FeedrateOnly(string writemessage)
		{
			bool feedRateOnlyCheck = checkCommandForFlag(writemessage, 'F');

			if (feedRateOnlyCheck)
			{
				feedRateOnlyCheck = !(checkCommandForFlag(writemessage, 'X') || checkCommandForFlag(writemessage, 'Y') || checkCommandForFlag(writemessage, 'Z') || checkCommandForFlag(writemessage, 'E'));
			}

			return feedRateOnlyCheck;
		}

		private List<string> parseGcode(string writemessage) //Because of different valid input I am now breaking the gcode command into a list of strings with each letter and values in their own string
		{
			List<string> gCodeList = new List<string>();
			StringBuilder singleParameterValuePair = new StringBuilder();
			foreach (char c in writemessage)
			{
				if (Char.IsDigit(c))
				{
					singleParameterValuePair.Append(c);
				}
				else
				{
					if ((Char.IsLetter(c) && singleParameterValuePair.Length == 0) || Char.IsPunctuation(c))
					{
						singleParameterValuePair.Append(c);
					}
					else
					{
						if (singleParameterValuePair.Length != 0)
						{
							gCodeList.Add(singleParameterValuePair.ToString());
							singleParameterValuePair.Clear();
							if (Char.IsLetter(c))
							{
								singleParameterValuePair.Append(c);
							}
						}

					}

				}//End else
			}

			return gCodeList;
		}

		private bool moveIsExtrudeOnly(List<string> commands)
		{
			int numParamsNotExtrude = commands.Count((x => x[0] != 'E' && x[0] != 'F'));

			return numParamsNotExtrude <= 1;//The G command will count as 1 so if there are more then there are other params
		}

		private float CalculateMoveInMM(List<string> commands)
		{
			float move;
			if (moveIsExtrudeOnly(commands))
			{
				if (relativePos)
				{
					move = printerDetails.targetExtruderPosition;
				}
				else
				{
					move = (printerDetails.targetExtruderPosition - printerDetails.activeExtruderPosition);
				}
			}
			else
			{
				if (relativePos)
				{
					move = (float)printerDetails.targetMovePosition.Length;
				}
				else
				{
					move = (float)(printerDetails.targetMovePosition - printerDetails.currentPosition).Length;
				}
			}

			return move;
		}

		private bool commandHasNoParameters(string writemessage)
		{
			char[] whitespace = { ' ', '\r', '\n' };
			int firstWhitspace = writemessage.IndexOfAny(whitespace);
			return (firstWhitspace + 2) >= writemessage.Length;
		}

		private bool commandHasNoParameters(List<string> commands)
		{
			return commands.Count <= 1;
		}

		private void updateFeedRate(int newFeedrate)
		{
			if (newFeedrate != 0)
			{
				feedrate = newFeedrate;
			}
		}

		private long CalculateDDA()
		{
			return ((60000000 / (long)(printerDetails.stepsPerMm.X * feedrate)));
		}

		private void updateTargetPostition(List<string> commands)
		{
			float temp;
			if (checkCommandForFlag(commands, 'X'))
			{
				temp = getParameterValue(commands, 'X');
				if (!relativePos)
				{
					if (printerDetails.activeExtruderIndex == 0)
					{
						printerDetails.targetMovePosition.X = temp;
					}
					else
					{
						printerDetails.targetMovePosition.X = (temp - printerDetails.extruderOffset.X);
					}
				}
				else
				{
					printerDetails.targetMovePosition.X = temp;
				}

			}
			else
			{
				if (relativePos)
				{
					printerDetails.targetMovePosition.X = 0;
				}
				else
				{
					printerDetails.targetMovePosition.X = printerDetails.currentPosition.X;
				}
			}//End X position
			if (checkCommandForFlag(commands, 'Y'))
			{
				temp = getParameterValue(commands, 'Y');
				if (!relativePos)
				{
					if (printerDetails.activeExtruderIndex == 0)
					{
						printerDetails.targetMovePosition.Y = temp;
					}
					else
					{
						printerDetails.targetMovePosition.Y = (temp - printerDetails.extruderOffset.Y);
					}
				}
				else
				{
					printerDetails.targetMovePosition.Y = temp;
				}

			}
			else
			{
				if (relativePos)
				{
					printerDetails.targetMovePosition.Y = 0;
				}
				else
				{
					printerDetails.targetMovePosition.Y = printerDetails.currentPosition.Y;
				}
			}//End Y position

			if (checkCommandForFlag(commands, 'Z'))
			{
				temp = getParameterValue(commands, 'Z');
				printerDetails.targetMovePosition.Z = temp;
			}
			else
			{
				if (relativePos)
				{
					printerDetails.targetMovePosition.Z = 0;
				}
				else
				{
					printerDetails.targetMovePosition.Z = printerDetails.currentPosition.Z;
				}
			}//end Zposition
			if (checkCommandForFlag(commands, 'E'))
			{
				temp = getParameterValue(commands, 'E');
				printerDetails.targetExtruderPosition = (temp * -1); //Extruder is inverted on makerbot. not sure why.     
			}
			else
			{
				if (printerDetails.extruderRelativePos)
				{
					printerDetails.targetExtruderPosition = 0;
				}
				else
				{
					printerDetails.targetExtruderPosition = printerDetails.activeExtruderPosition;
				}
			}//End Extruder Pos

		}

		private bool checkCommandForFlag(string command, char p)
		{
			return command.Contains(p);
		}

		private bool checkCommandForFlag(List<string> commands, char p)
		{
			return commands.Exists(x => x != "" && x[0] == p);
		}

		private int getCommandValue(string command)
		{
			string intValueString = command.Substring(1);
			return int.Parse(intValueString);
		}

		private float getParameterValue(string command, char targetParam)
		{
			float i = 0;
			int index = command.IndexOf(targetParam);

			if (index != -1)
			{
				char[] whitespace = { ' ', '\n', '\t' };
				string str = command.Substring(index + 1, command.IndexOfAny(whitespace, index) - index);
				i = float.Parse(str);
			}

			return i;
		}

		private float getParameterValue(List<string> commandList, char targetParam)
		{
			float paramValue = 0;

			string targetCommand = commandList.Find(x => x != "" && x[0] == targetParam);

			if (targetCommand != null && targetCommand != "")
			{
				targetCommand = targetCommand.Substring(1);
				paramValue = float.Parse(targetCommand);
			}

			return paramValue;
		}

		private class X3GPacketFactory
		{
			private const int MAX_PACKET_SIZE = 256;

			private byte[] packetOutline;

			private int index;
			private X3GCrc crc;

			public X3GPacketFactory(byte printerCommand)
			{
				packetOutline = new byte[MAX_PACKET_SIZE];
				index = 2;
				crc = new X3GCrc();
				addByte(printerCommand);
			}

			public void addByte(byte command)
			{
				packetOutline[index] = command;
				index++;
				crc.update(command);
			}

			public void add16bits(int command)
			{
				addByte((byte)(command & 0xff));
				addByte((byte)((command >> 8) & 0xff));
			}

			public void add32bits(long command)
			{
				add16bits((int)command & 0xffff);
				add16bits((int)(command >> 16) & 0xffff);
			}

			public void addFloat(float command)
			{
				//Convert float into bits:
				//Bit  31 = Sign
				//Bits 30-23 = Exponent (8bits)
				//Bits 22-0 = Mantissa  (23bits)
				byte[] bits = BitConverter.GetBytes(command);
				add32bits((long)BitConverter.ToInt32(bits, 0));
			}

			public byte[] getX3GPacket()
			{
				byte packetLength = (byte)(index + 1);

				byte[] packetArray = new byte[packetLength];
				packetArray[0] = 0xD5;
				packetArray[1] = (byte)(packetLength - (byte)3); //Length does not count packet header or crc

				for (int i = 2; i < packetLength - 1; i++)
				{
					packetArray[i] = packetOutline[i];
				}

				packetArray[packetLength - 1] = crc.getCrc();

				return packetArray;
			}

			public void clear()
			{
				index = 0;
				packetOutline = new byte[MAX_PACKET_SIZE];
				crc.clear();
			}
		}
	}
}
