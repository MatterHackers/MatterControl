﻿/*
Copyright (c) 2023, Lars Brubaker
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

namespace MatterHackers.MatterControl.DesignTools
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class SetDateButtonAttribute : Attribute
    {
        public string ButtonName { get; }
        public string ButtonHint { get; }

        public DateTime Date { get; set; }


        /// <summary>
        /// Set the date to a specific date
        /// </summary>
        /// <param name="buttonName">The name of the button</param>
        /// <param name="buttonHint">The hint to show when the button is hovered over</param>
        /// <param name="dateyyyyMMdd">The date to set the property to in yyyyMMdd format, or "Now" to set it to the current date</param>
        public SetDateButtonAttribute(string buttonName, string buttonHint, string dateyyyyMMdd)
        {
            this.ButtonName = buttonName;
            this.ButtonHint = buttonHint;
            if (dateyyyyMMdd == "Now")
            {
                this.Date = DateTime.Now;
            }
            else
            {
                this.Date = DateTime.ParseExact(dateyyyyMMdd, "yyyyMMdd", null);
            }
        }
    }
}