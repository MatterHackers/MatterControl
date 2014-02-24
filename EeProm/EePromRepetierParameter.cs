/*
Copyright (c) 2014, Lars Brubaker
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.EeProm
{
    public class EePromRepetierParameter : EventArgs
    {
        public string description;
        public int type;
        public int position;
        string val = "";
        bool changed = false;

        public EePromRepetierParameter(string line)
        {
            update(line);
        }

        public void update(string line)
        {
            string[] lines = line.Substring(4).Split(' ');
            int.TryParse(lines[0], out type);
            int.TryParse(lines[1], out position);
            val = lines[2];
            description = line.Substring(7 + lines[0].Length + lines[1].Length + lines[2].Length);
            changed = false;
        }
        
        public void save()
        {
            if (!changed)
            {
                return;
            }

            string cmd = "M206 T" + type + " P" + position + " ";
            if (type == 3) cmd += "X" + val;
            else cmd += "S" + val;
            PrinterCommunication.Instance.QueueLineToPrinter(cmd);
            changed = false;
        }

        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        public string Value
        {
            get { return val; }
            set
            {
                value = value.Replace(',', '.').Trim();
                if (val.Equals(value)) return;
                val = value;
                changed = true;
            }
        }
    }
}
