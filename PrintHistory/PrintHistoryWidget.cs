/*
Copyright (c) 2014, Kevin Pope
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
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrintHistory
{
    public class PrintHistoryWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        Button deleteFromLibraryButton;
        Button addToQueueButton;
        Button searchButton;

        public PrintHistoryWidget()
        {
            SetDisplayAttributes();

            textImageButtonFactory.borderWidth = 0;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                FlowLayoutWidget searchPanel = new FlowLayoutWidget();
                searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
                searchPanel.HAnchor = HAnchor.ParentLeftRight;
                searchPanel.Padding = new BorderDouble(0);
                searchPanel.Height = 50;

                FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
                buttonPanel.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel.Padding = new BorderDouble(0, 3);
                {
                    Button addToLibrary = textImageButtonFactory.Generate(LocalizedString.Get("Import"), "icon_import_white_32x32.png");
                    buttonPanel.AddChild(addToLibrary);
                    addToLibrary.Margin = new BorderDouble(0, 0, 3, 0);

                    addToQueueButton = textImageButtonFactory.Generate("Add to Queue");
                    addToQueueButton.Margin = new BorderDouble(3, 0);
                    addToQueueButton.Click += new ButtonBase.ButtonEventHandler(addToQueueButton_Click);
                    addToQueueButton.Visible = false;
                    buttonPanel.AddChild(addToQueueButton);

                    deleteFromLibraryButton = textImageButtonFactory.Generate("Remove");
                    deleteFromLibraryButton.Margin = new BorderDouble(3, 0);
                    deleteFromLibraryButton.Visible = false;
                    buttonPanel.AddChild(deleteFromLibraryButton);

                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel.AddChild(spacer);
                }

                allControls.AddChild(searchPanel);
                allControls.AddChild(PrintHistoryListControl.Instance);
                allControls.AddChild(buttonPanel);
            }
            allControls.AnchorAll();

            this.AddChild(allControls);

            AddHandlers();
        }

        private void AddHandlers()
        {
            //pass
        }

        private void addToQueueButton_Click(object sender, MouseEventArgs e)
        {
            foreach (PrintHistoryListItem item in PrintHistoryListControl.Instance.SelectedItems)
            {
                PrintQueue.PrintQueueItem queueItem = new PrintQueue.PrintQueueItem(item.printItem);
                PrintQueue.PrintQueueControl.Instance.AddChild(queueItem);
            }
            PrintQueue.PrintQueueControl.Instance.EnsureSelection();
        }

        private void onLibraryItemsSelected(object sender, EventArgs e)
        {
            List<PrintHistoryListItem> selectedItemsList = (List<PrintHistoryListItem>)sender;
            if (selectedItemsList.Count > 0)
            {
                addToQueueButton.Visible = true;
                deleteFromLibraryButton.Visible = true;
            }
            else
            {
                addToQueueButton.Visible = false;
                deleteFromLibraryButton.Visible = false;
            }
        }

        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }        
    }
}
