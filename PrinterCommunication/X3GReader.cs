using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrinterCommunication
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


    class X3GReader
    {
        public static string translate(byte[] x3gResponse)
        {
            
            X3GPacketAnalyzer analyzer = new X3GPacketAnalyzer(x3gResponse); 
            

            return analyzer.analyze();
        }

        private class X3GPacketAnalyzer
        {
            private byte[] response;
            private X3GCrc crc;


            public X3GPacketAnalyzer(byte[] x3gResponse)
            {
                response = x3gResponse;
                crc = new X3GCrc();
            }

            public string analyze()
            {
                string gCodeResponse = "";
                int payloadLength;

                if (response[0] == 0xD5)
                {
                    payloadLength = response[1];
                    gCodeResponse += analyzePayload(payloadLength);
                    checkCrc(payloadLength + 2);
                }

                gCodeResponse += "\r\n";

                return gCodeResponse;
            }

            private string analyzePayload(int payloadLength)
            {
                string payloadStr = "";
                for (int i = 2; i < payloadLength; i++)
                {
                    if (i == 2)
                    {
                        switch(response[i])
                        {
                            case 0x81:
                                payloadStr += "ok";
                                break;
                            case 0x80:
                            case 0x83:
                            case 0x88:
                            case 0x89:
                            case 0x8C:
                                payloadStr += "rs";
                                break;
                        }
                        
                    }

                    //TODO - analyze payload
                    crc.update(response[i]);
                }

                return payloadStr;
            }

            private bool checkCrc(int crcIndex)
            {
                return crc.getCrc() == response[crcIndex];
            }
        }


    }
    
}
