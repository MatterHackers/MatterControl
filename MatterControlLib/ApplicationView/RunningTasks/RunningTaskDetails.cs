﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
    public class RunningTaskDetails
    {
        public event EventHandler<(double ratio, string message)> ProgressChanged;

        public Func<GuiWidget> DetailsItemAction { get; set; }

        private CancellationTokenSource tokenSource;

        private bool? _isExpanded = null;

        public RunningTaskDetails(CancellationTokenSource tokenSource)
        {
            this.tokenSource = tokenSource;
        }

        public string Title { get; set; }

        public object Owner { get; set; }

        public RunningTaskOptions Options { get; internal set; }

        public bool IsExpanded
        {
            get
            {
                if (_isExpanded == null)
                {
                    if (this.Options is RunningTaskOptions options
                        && !string.IsNullOrWhiteSpace(options.ExpansionSerializationKey))
                    {
                        string dbValue = UserSettings.Instance.get(options.ExpansionSerializationKey);
                        _isExpanded = dbValue != "0";
                    }
                    else
                    {
                        _isExpanded = false;
                    }
                }

                return _isExpanded ?? false;
            }

            set
            {
                _isExpanded = value;

                if (this.Options?.ExpansionSerializationKey is string expansionKey
                    && !string.IsNullOrWhiteSpace(expansionKey))
                {
                    UserSettings.Instance.set(expansionKey, (_isExpanded ?? false) ? "1" : "0");
                }
            }
        }

        public void Report(double ratio, string message)
        {
            this.ProgressChanged?.Invoke(this, (ratio, message));
        }

        public void CancelTask()
        {
            this.tokenSource.Cancel();
        }
    }
}