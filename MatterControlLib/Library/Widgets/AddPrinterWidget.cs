/*
Copyright (c) 2022, John Lewin, Lars Brubaker
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library.Widgets.HardwarePage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Library.Widgets
{
	public class AddPrinterWidget : SearchableTreePanel
	{
		private SectionWidget nameSection;
		private ThemedTextEditWidget printerNameInput;
		private bool usingDefaultName = true;
		private Action<bool> nextButtonEnabled;
		private FlowLayoutWidget printerInfo;

		public AddPrinterWidget(GuiWidget nextButton, ThemeConfig theme, Action<bool> nextButtonEnabled, bool filterToPulse)
			: base(theme)
		{
			this.nextButtonEnabled = nextButtonEnabled;
			this.ExistingPrinterNames = new HashSet<string>(ProfileManager.Instance.ActiveProfiles.Select(p => p.Name));
			this.Name = "AddPrinterWidget";

			horizontalSplitter.Panel2.Padding = theme.DefaultContainerPadding;

			treeView.AfterSelect += this.TreeView_AfterSelect;

			treeView.NodeMouseDoubleClick += (s, e) =>
			{
				if (e is MouseEventArgs mouseEvent
					&& mouseEvent.Button == MouseButtons.Left
						&& mouseEvent.Clicks == 2
						&& treeView?.SelectedNode is TreeNode treeNode)
				{
					nextButton.InvokeClick();
				}
			};			


			UiThread.RunOnIdle(() =>
			{
				foreach (var oem in OemSettings.Instance.OemProfiles.OrderBy(o => o.Key))
				{
					if (filterToPulse && !oem.Key.Contains("Pulse"))
                    {
						continue;
                    }

					var rootNode = this.CreateTreeNode(oem);
					rootNode.Expandable = true;
					rootNode.TreeView = treeView;
					rootNode.Load += (s, e) =>
					{
						var image = OemSettings.Instance.GetIcon(oem.Key, theme);

						SetImage(rootNode, image);
					};

					if (filterToPulse)
					{
						rootNode.Expanded = true;
					}

					contentPanel.AddChild(rootNode);
				}

				this.TreeLoaded = true;
			});

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(theme.DefaultContainerPadding).Clone(top: 0)
			};

			var panel2Column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			panel2Column.AddChild(new TextWidget("Select a printer to continue".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			nameSection = new SectionWidget("New Printer Name".Localize(), container, theme, expandingContent: false)
			{
				HAnchor = HAnchor.Stretch,
				Padding = theme.ToolbarPadding,
				Enabled = false
			};
			theme.ApplyBoxStyle(nameSection);

			// Reset right margin
			nameSection.Margin = nameSection.Margin.Clone(right: theme.DefaultContainerPadding);

			printerInfo = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			nameSection.BackgroundColor = theme.MinimalShade;
			nameSection.Margin = new BorderDouble(theme.DefaultContainerPadding).Clone(left: 0);
			panel2Column.AddChild(nameSection);

			panel2Column.AddChild(PrinterNameError = new TextWidget("", 0, 0, 10)
			{
				TextColor = Color.Red,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			});

			// Padding allows space for scrollbar
			printerInfo.Padding = new BorderDouble(right: theme.DefaultContainerPadding + 2);

			panel2Column.AddChild(printerInfo);

			horizontalSplitter.Panel2.Padding = horizontalSplitter.Panel2.Padding.Clone(right: 0, bottom: 0);

			horizontalSplitter.Panel2.AddChild(panel2Column);

			printerNameInput = new ThemedTextEditWidget("", theme)
			{
				HAnchor = HAnchor.Stretch,
			};
			printerNameInput.ActualTextEditWidget.EditComplete += (s, e) =>
			{
				this.ValidateControls();
				this.usingDefaultName = false;
			};

			container.AddChild(printerNameInput);
		}

		private static void SetImage(TreeNode node, ImageBuffer image)
		{
			node.Image = image;

			// Push to children
			foreach (var child in node.Nodes)
			{
				SetImage(child, image);
			}
		}

		public HashSet<string> ExistingPrinterNames { get; private set; }

		public TextWidget PrinterNameError { get; private set; }

		public bool ValidateControls()
		{
			bool selectionValid = this.SelectedPrinter is MakeModelInfo selectedPrinter;

			if (!selectionValid)
			{
				this.SetError("Invalid printer selection".Localize());
			}

			string printerName = this.NewPrinterName;

			bool nameValid = !string.IsNullOrWhiteSpace(printerName);

			if (!nameValid)
			{
				this.SetError("Printer name cannot be blank".Localize());
			}

			bool nameIsUnique = !this.ExistingPrinterNames.Any(p => p.Equals(printerName, StringComparison.OrdinalIgnoreCase));
			if (!nameIsUnique)
			{
				this.SetError("Printer name already exists".Localize());
			}

			bool allValid = selectionValid
				&& nameValid
				&& nameIsUnique;

			nextButtonEnabled(allValid);

			if (allValid)
			{
				this.ClearError();
			}

			return allValid;
		}

		internal void SetError(string errorMessage)
		{
			this.PrinterNameError.Text = errorMessage;
			this.PrinterNameError.Visible = true;
		}

		protected override bool FilterTree(TreeNode context, string filter, bool parentVisible, List<TreeNode> matches)
		{
			// Filter against make/model for printers or make for top level nodes
			string itemText = (context.Tag as MakeModelInfo)?.ToString() ?? context.Text;

			bool hasFilterText = itemText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) != -1;
			context.Visible = hasFilterText || parentVisible;

			if (context.Visible
				&& context.NodeParent != null)
			{
				context.NodeParent.Visible = true;
				context.NodeParent.Expanded = true;
				context.Expanded = true;
			}

			if (context.NodeParent != null
				&& hasFilterText)
			{
				matches.Add(context);
			}

			bool childMatched = false;

			foreach (var child in context.Nodes)
			{
				childMatched |= FilterTree(child, filter, hasFilterText || parentVisible, matches);
			}

			bool hasMatch = childMatched || hasFilterText;

			if (hasMatch)
			{
				context.Visible = context.Expanded = true;
			}

			return hasMatch;
		}


		private void ClearError()
		{
			this.PrinterNameError.Text = "";
			this.PrinterNameError.Visible = true;
		}

		private TreeNode CreateTreeNode(KeyValuePair<string, Dictionary<string, PublicDevice>> make)
		{
			var treeNode = new TreeNode(theme)
			{
				Text = make.Key,
			};


			string currentGroup = "";

			var context = treeNode;

			foreach (var printer in make.Value.OrderBy(p => p.Key))
			{
				if (make.Key == "Pulse"
					&& currentGroup != printer.Key[0] + " Series")
				{
					currentGroup = printer.Key[0] + " Series";

					treeNode.Nodes.Add(context = new TreeNode(theme, nodeParent: treeNode)
					{
						Text = currentGroup,
					});
				}

				context.Nodes.Add(new TreeNode(theme, nodeParent: treeNode)
				{
					Text = printer.Key,
					Name = $"Node{make.Key}{printer.Key}",
					Tag = new MakeModelInfo()
					{
						Make = make.Key,
						Model = printer.Key
					}
				});
			}

			return treeNode;
		}

		private void TreeView_AfterSelect(object sender, TreeNode e)
		{
			nameSection.Enabled = treeView.SelectedNode != null;
			this.ClearError();

			this.PrinterNameError.Visible = false;

			if (nameSection.Enabled
				&& treeView.SelectedNode.Tag != null)
			{
				UiThread.RunOnIdle(() =>
				{
					if (usingDefaultName
						&& treeView.SelectedNode != null)
					{
						string printerName = treeView.SelectedNode.Tag.ToString();

						printerNameInput.Text = agg_basics.GetNonCollidingName(printerName, this.ExistingPrinterNames);

						this.SelectedPrinter = treeView.SelectedNode.Tag as MakeModelInfo;

						printerInfo.CloseChildren();

						if (this.SelectedPrinter != null
							&& OemSettings.Instance.OemPrinters.TryGetValue($"{SelectedPrinter.Make}-{ SelectedPrinter.Model}", out StorePrinterID storePrinterID))
						{
							printerInfo.AddChild(
								new PrinterDetails(
									new PrinterInfo()
									{
										Make = SelectedPrinter.Make,
										Model = SelectedPrinter.Model,
									},
									theme,
									false)
								{
									ShowProducts = false,
									ShowHeadingRow = false,
									StoreID = storePrinterID?.SID,
									HAnchor = HAnchor.Stretch,
									VAnchor = VAnchor.Stretch
								});
						}

						nextButtonEnabled(treeView.SelectedNode != null
							&& !string.IsNullOrWhiteSpace(printerNameInput.Text));
					}
				});
			}
			else
			{
				nextButtonEnabled(false);
			}
		}

		public MakeModelInfo SelectedPrinter { get; private set; }

		public string NewPrinterName => printerNameInput.Text;

		public class MakeModelInfo
		{
			public string Make { get; set; }

			public string Model { get; set; }

			public override string ToString() => $"{Make} {Model}";
		}
	}
}
