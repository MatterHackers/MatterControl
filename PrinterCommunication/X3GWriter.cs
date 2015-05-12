using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterCommunication
{
    class X3GWriter
    {
        //Axes bitfeilds
        byte xAxis = 0x01;
        byte yAxis = 0x02;
        byte zAxis = 0x04;
        byte xyAxes = 0x03;
        byte aAxis; //Unknown functionality
        byte bAxis; //Unknown functionality

        public static byte[] translate(string writemessage)
        {
            byte[] convertedMessage;
            X3GPacketFactory binaryPacket;
            char commandC = writemessage[0];

            //Convert Connect message to X3G
            switch(commandC)
            {
                case 'M':

                    if (writemessage == "M115\r\n")
                    {
                
                        binaryPacket = new X3GPacketFactory(0x00);
                        binaryPacket.add16bits(0x28);
                        //Convert M115 to 0x00 (host query call for X3G)
                        convertedMessage = binaryPacket.getX3GPacket();                
                    }
                    else if (writemessage == "M105\r\n")
                    {
                        binaryPacket = new X3GPacketFactory(0x0A);
                        binaryPacket.addByte(0x00);
                        binaryPacket.addByte(0x02);
                        convertedMessage = binaryPacket.getX3GPacket();                
                    }
                    else
                    {
                        convertedMessage = new byte[] { 0 };
                    }
                    break;

                case 'G': //TODO
                    convertedMessage = new byte[] { 0 };
                    break;

                case 'X':
                    writemessage = writemessage.Substring(1);
                    string[] commands = writemessage.Split(' ');
                    int arraySize = commands.Length;               

                    binaryPacket = new X3GPacketFactory(Byte.Parse(commands[0]));

                    for (int i = 1; i < commands.Length; i++ )
                    {
                        char c = commands[i][0];
                        commands[i] = commands[i].Substring(1);
                        switch(c)
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

                    convertedMessage = binaryPacket.getX3GPacket();
                    break;
                default:
                    convertedMessage = new byte[] { 0 };
                    break;
            }

                       


            return convertedMessage;
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

            public void addByte(int command)
            {
                packetOutline[index] = (byte)command;
                index++;
                crc.update((byte)command);
            }

            public void add16bits(int command)
            {
                addByte((byte)(command & 0xff));
                addByte((byte)(command >> 8) & 0xff);
            }

            public void add32bits(long command)
            {
                add16bits((int)command & 0xffff);
                add16bits((int)(command >> 16) & 0xffff);
            }

            public byte[] getX3GPacket()
            {
                byte packetLength = (byte)(index +1);

                byte[] packetArray = new byte[packetLength];
                packetArray[0] = 0xD5;
                packetArray[1] = (byte)(packetLength - (byte)3); //Length does not count packet header or crc

                for (int i = 2; i < packetLength-1; i++ )
                {
                    packetArray[i] = packetOutline[i];
                }

                packetArray[packetLength - 1] = crc.getCrc();

                return packetArray;
            }
                                           
        }


    }
}
