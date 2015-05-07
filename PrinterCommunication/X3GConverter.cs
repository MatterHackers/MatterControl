using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterCommunication
{
    class X3GConverter
    {
        //Axes bitfeilds
        byte xAxis = 0x00;
        byte yAxis = 0x01;
        byte zAxis = 0x02;
        byte aAxis = 0x02; //Unknown functionality
        byte bAxis = 0x03; //Unknown functionality

        public static string translate(string writemessage)
        {            
            string convertedMessage = writemessage;
            X3GPacketFactory binaryPacket;            
            //Convert Connect message to X3G
            if (writemessage == "M115\r\n")
            {
                
                binaryPacket = new X3GPacketFactory(0x00);
                binaryPacket.add16bits(0x28);
                //Convert M115 to 0x00 (host query call for X3G)

                convertedMessage = System.Text.Encoding.ASCII.GetString(binaryPacket.getX3GPacket());
            }
            else if (writemessage == "M105\r\n")
            {
                binaryPacket = new X3GPacketFactory(0x0A);
                binaryPacket.addByte(0x00);
                binaryPacket.addByte(0x02);
                convertedMessage = System.Text.Encoding.ASCII.GetString(binaryPacket.getX3GPacket());
            }
            else if (writemessage[0] == 'X')
            {
                writemessage = writemessage.Substring(1);
                string[] commands = writemessage.Split(' ');
                int arraySize = commands.Length;
                //binaryMessage = new byte[arraySize];

                //for(int i=0; i < arraySize; i++)
                //{
                //    binaryMessage[i] = Byte.Parse(commands[i]);
                //}

                binaryPacket = new X3GPacketFactory(Byte.Parse(commands[0]));
                convertedMessage = System.Text.Encoding.ASCII.GetString(binaryPacket.getX3GPacket());
                //binaryPacket = new X3GPacket(Byte.Parse(writemessage));
                //convertedMessage = System.Text.Encoding.UTF8.GetString(binaryPacket.toByteArray());
            }


            return convertedMessage;
        }

        //Possibly replace packet class with helper method

        private class X3GPacketFactory
        {
            private const int MAX_PACKET_SIZE = 256;

            private byte[] packetOutline;

            private int index;
            private uint crc;

            public X3GPacketFactory(byte printerCommand)
            {
                packetOutline = new byte[MAX_PACKET_SIZE];
                index = 2;
                crc = 0;
                addByte(printerCommand);
            }

            public void addByte(int command)
            {
                packetOutline[index] = (byte)command;
                index++;
                generateNewCRC((byte)command);
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

                packetArray[packetLength - 1] = (byte)crc;

                return packetArray;
            }

            private void generateNewCRC(byte command)
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


        }


    }
}
