﻿/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class PrinterOutputCache
    {
        static PrinterOutputCache instance = null;
        public static PrinterOutputCache Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PrinterOutputCache();
                }

                return instance;
            }
        }

        public List<string> PrinterLines = new List<string>();

        public RootedObjectEventHandler HasChanged = new RootedObjectEventHandler();

        event EventHandler unregisterEvents;
        PrinterOutputCache()
        {
            PrinterConnectionAndCommunication.Instance.ConnectionFailed.RegisterEvent(Instance_ConnectionFailed, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(FromPrinter, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.RegisterEvent(ToPrinter, ref unregisterEvents);
        }

        void OnHasChanged(EventArgs e)
        {
            HasChanged.CallEvents(this, e);
        }

        void FromPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            StringEventArgs eventArgs = new StringEventArgs("<-" + lineString.Data);
            PrinterLines.Add(eventArgs.Data);
            OnHasChanged(eventArgs);
        }

        void ToPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            StringEventArgs eventArgs = new StringEventArgs("->" + lineString.Data);
            PrinterLines.Add(eventArgs.Data);
            OnHasChanged(eventArgs);
            OnHasChanged(eventArgs);
        }

        void Instance_ConnectionFailed(object sender, EventArgs e)
        {
            OnHasChanged(null);
            StringEventArgs eventArgs = new StringEventArgs("Lost connection to printer.");
            PrinterLines.Add(eventArgs.Data);
            OnHasChanged(eventArgs);
        }

        public void Clear()
        {
            PrinterLines.Clear();
            OnHasChanged(null);
        }
    }
}
