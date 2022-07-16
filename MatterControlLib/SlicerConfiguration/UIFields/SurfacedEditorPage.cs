﻿/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SurfacedEditorPage : DialogPage
	{
		public event EventHandler ValueChanged;

		private ThemedTextEditWidget editWidget;

		public SurfacedEditorPage(IObject3D selectedItem)
		{
			this.WindowTitle = "MatterControl - " + "Editor Selector".Localize();
			this.HeaderText = "Surfaced Editor".Localize();

			contentRow.Padding = 0;

			var editContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = theme.DefaultContainerPadding,
				BackgroundColor = theme.BackgroundColor
			};

			editWidget = new ThemedTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch,
				Name = this.Name,
				SelectAllOnFocus = true
			};
			editWidget.DrawFromHintedCache();
			UiThread.RunOnIdle(() =>
			{
				editWidget.ActualTextEditWidget.Focus();
				editWidget.ActualTextEditWidget.InternalTextEditWidget.SelectAll();
			});

			editContainer.AddChild(editWidget);

			contentRow.AddChild(editContainer);

			// add the tree view
			var treeView = new TreeView(theme)
			{
				Margin = new BorderDouble(left: 18),
			};
			treeView.AfterSelect += (s, e) =>
			{
				if (treeView.SelectedNode.Tag is IObject3D contextNode)
				{
					editWidget.Text = "$." + string.Join(".", contextNode.AncestorsAndSelf().TakeWhile(o => !(o is ComponentObject3D)).Select(o => $"Children<{o.GetType().Name.ToString()}>").Reverse().ToArray());
				}
			};
			treeView.ScrollArea.ChildAdded += (s, e) =>
			{
				if (e is GuiWidgetEventArgs childEventArgs
					&& childEventArgs.Child is TreeNode treeNode)
				{
					treeNode.AlwaysExpandable = true;
				}
			};

			treeView.Click += (s, e) =>
			{
				if (treeView.IsDoubleClick(e))
				{
					Console.WriteLine();
				}
			};

			treeView.ScrollArea.CloseChildren();

			var keyValues = new Dictionary<IObject3D, TreeNode>();
	
			var rootNode = Object3DTreeBuilder.BuildTree(selectedItem, keyValues, theme);
			treeView.AddChild(rootNode);
			rootNode.TreeView = treeView;

			editContainer.AddChild(treeView);
			var dummyWidget = new GuiWidget()
			{
				BackgroundColor = Color.Red
			};

			var saveButton = theme.CreateDialogButton("Save".Localize());
			saveButton.Click += (s, e) =>
			{
				this.ValueChanged?.Invoke(this, null);

				this.DialogWindow.CloseOnIdle();
			};
			this.AddPageAction(saveButton);

			editWidget.ActualTextEditWidget.EnterPressed += (s, e) => saveButton.InvokeClick();
		}

		public string EditorString
		{
			get => editWidget.Text;
			set
			{
				editWidget.Text = value;
			}
		}
	}
}
