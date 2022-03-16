/*
Copyright (c) 2018, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library.Widgets.HardwarePage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Library.Widgets
{
	public class PrintersTabPage : FlowLayoutWidget
	{
		public PrintersTabPage(ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.Padding = 0;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			var horizontalSplitter = new Splitter()
			{
				SplitterDistance = UserSettings.Instance.LibraryViewWidth,
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};

			horizontalSplitter.DistanceChanged += (s, e) =>
			{
				UserSettings.Instance.LibraryViewWidth = horizontalSplitter.SplitterDistance;
			};

			this.AddChild(horizontalSplitter);

			var treeView = new HardwareTreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Width = 300,
				Margin = 5
			};

			treeView.NodeMouseDoubleClick += (s, e) =>
			{
				if (e is MouseEventArgs mouseEvent
					&& s is GuiWidget clickedWidget
					&& mouseEvent.Button == MouseButtons.Left
						&& mouseEvent.Clicks == 2)
				{
					if (treeView?.SelectedNode.Tag is PrinterInfo printerInfo)
					{
						ApplicationController.Instance.OpenPrinter(printerInfo);
					}
				}
			};

			treeView.NodeMouseClick += (s, e) =>
			{
				if (e is MouseEventArgs mouseEvent
					&& s is GuiWidget clickedWidget
					&& mouseEvent.Button == MouseButtons.Right)
				{
					UiThread.RunOnIdle(() =>
					{
						var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

						var openMenuItem = popupMenu.CreateMenuItem("Open".Localize());
						openMenuItem.Click += (s2, e2) =>
						{
							if (treeView?.SelectedNode.Tag is PrinterInfo printerInfo)
							{
								ApplicationController.Instance.OpenPrinter(printerInfo);
							}
						};

						popupMenu.CreateSeparator();

						var deleteMenuItem = popupMenu.CreateMenuItem("Delete".Localize());
						deleteMenuItem.Click += (s2, e2) =>
						{
							if (treeView.SelectedNode.Tag is PrinterInfo printerInfo)
							{
								// Delete printer
								StyledMessageBox.ShowMessageBox(
								(deletePrinter) =>
								{
									if (deletePrinter)
									{
										ProfileManager.Instance.DeletePrinter(printerInfo.ID);
									}
								},
								"Are you sure you want to delete printer '{0}'?".Localize().FormatWith(printerInfo.Name),
								"Delete Printer?".Localize(),
								StyledMessageBox.MessageType.YES_NO,
								"Delete Printer".Localize());
							}
						};

						popupMenu.ShowMenu(clickedWidget, mouseEvent);
					});
				}
			};

			treeView.ScrollArea.HAnchor = HAnchor.Stretch;

			treeView.AfterSelect += (s, e) =>
			{
				if (treeView.SelectedNode.Tag is PrinterInfo printerInfo)
				{
					horizontalSplitter.Panel2.CloseChildren();
					horizontalSplitter.Panel2.AddChild(new PrinterDetails(printerInfo, theme, true)
					{
						HAnchor = HAnchor.MaxFitOrStretch,
						VAnchor = VAnchor.Stretch,
						Padding = theme.DefaultContainerPadding
					});
				}
			};
			horizontalSplitter.Panel1.AddChild(treeView);

			horizontalSplitter.Panel2.AddChild(new GuiWidget()
			{
				HAnchor =HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			});
		}
	}
}
