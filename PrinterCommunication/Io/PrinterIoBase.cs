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

// This is the base class for translators and sources to the printer communication. Things like bed leveling,
// temperature injection, etc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
    public abstract class PrinterIoBase
    {
        public PrinterIoBase()
        {
        }

        public virtual PrinterMachineInstruction PeekNextInstruction()
        {
            throw new NotImplementedException();
        }

        public virtual PrinterMachineInstruction PopNextInstruction()
        {
            throw new NotImplementedException();
        }

        public virtual void AddInstruction(PrinterMachineInstruction newCommand)
        {
            throw new NotImplementedException();
        }

        public virtual int NumberOfInstruction
        {
            get { throw new NotImplementedException(); }
        }

        public virtual double TotalSecondsInPrint 
        {
            get { throw new NotImplementedException(); }
        }

        public virtual double SecondsRemaining
        {
            get { throw new NotImplementedException(); }
        }
    }
}
