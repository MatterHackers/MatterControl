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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class ApplicationWidget : GuiWidget
    {
        static ApplicationWidget globalInstance;
        public RootedObjectEventHandler ReloadPanelTrigger = new RootedObjectEventHandler();
        public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();

        public SlicePresetsWindow EditSlicePresetsWindow { get; set;} 

        event EventHandler unregisterEvents;

        public bool WidescreenMode { get; set; }

        public ApplicationWidget()
        {
            Name = "MainSlidePanel";
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ReloadAll, ref unregisterEvents);
        }

        WidescreenPanel widescreenPanel;
        void AddElements()
        {
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.AnchorAll();

            ApplicationMenuRow menuRow = new ApplicationMenuRow();
            container.AddChild(menuRow);

            GuiWidget menuSeparator = new GuiWidget();
            menuSeparator.BackgroundColor = new RGBA_Bytes(200, 200, 200);
            menuSeparator.Height = 2;
            menuSeparator.HAnchor = HAnchor.ParentLeftRight;
            menuSeparator.Margin = new BorderDouble(3, 6,3,3);

            container.AddChild(menuSeparator);

            widescreenPanel = new WidescreenPanel();
            container.AddChild(widescreenPanel);

            this.AddChild(container);
        }

        public void ReloadAll(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                widescreenPanel.StoreUiState();
                this.CloseAndRemoveAllChildren();
                AddElements();
            });
        }

        void Initialize()
        {
            this.AnchorAll();
        }

        public static ApplicationWidget Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new ApplicationWidget();                    
                    globalInstance.AddElements();
                    globalInstance.Initialize();
                }
                return globalInstance;
            }
        }

        public void ReloadAdvancedControlsPanel()
        {
            ReloadPanelTrigger.CallEvents(this, null);
        }

        public void ChangeCloudSyncStatus()
        {
            CloudSyncStatusChanged.CallEvents(this, null);            
        }
    }
}
