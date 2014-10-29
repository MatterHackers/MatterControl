//-----------------------------------------------------------------------
// <copyright file="ConditionalClickWidget.cs" company="MatterHackers Inc.">
// Copyright (c) 2014, Lars Brubaker
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met: 
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer. 
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// The views and conclusions contained in the software and documentation are those
// of the authors and should not be interpreted as representing official policies, 
// either expressed or implied, of the FreeBSD Project.
// </copyright>
//-----------------------------------------------------------------------
namespace MatterHackers.MatterControl
{
    using System;

    /// <summary>
    /// The ConditionalClickWidget provides a ClickWidget that conditionally appears on the control surface to capture mouse input
    /// </summary>
    public class ConditionalClickWidget : ClickWidget
    {
        /// <summary>
        /// The delegate which provides the Enabled state
        /// </summary>
        private Func<bool> enabledCallback;

        /// <summary>
        /// Initializes a new instance of the ConditionalClickWidget class.
        /// </summary>
        /// <param name="enabledCallback">The delegate to </param>
        public ConditionalClickWidget(Func<bool> enabledCallback)
        {
            this.enabledCallback = enabledCallback;
        }

        /// <summary>
        /// Determines if the control is enabled
        /// </summary>
        public override bool Enabled
        {
            get
            {
                return this.enabledCallback();
            }

            set 
            { 
                throw new InvalidOperationException("Cannot set Enabled on ConditionalClickWidget"); 
            }
        }
    }
}
