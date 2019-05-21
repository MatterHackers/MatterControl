/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library.Widgets.HardwarePage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class AddPrinterWidget : FlowLayoutWidget
	{
		private SectionWidget nameSection;
		private SearchInputBox searchBox;

		private TreeView treeView;
		private FlowLayoutWidget rootColumn;
		private MHTextEditWidget printerNameInput;
		private bool usingDefaultName = true;
		private ThemeConfig theme;
		private TextButton nextButton;
		private FlowLayoutWidget printerInfo;

		public AddPrinterWidget(ThemeConfig theme, TextButton nextButton)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.nextButton = nextButton;
			this.ExistingPrinterNames = ProfileManager.Instance.ActiveProfiles.Select(p => p.Name).ToList();
			this.Name = "AddPrinterWidget";
			this.TreeLoaded = false;

			var searchIcon = AggContext.StaticData.LoadIcon("icon_search_24x24.png", 16, 16, theme.InvertIcons).AjustAlpha(0.3);

			searchBox = new SearchInputBox(theme)
			{
				Name = "Search",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(bottom: 4),
			};

			searchBox.ResetButton.Visible = false;

			var searchInput = searchBox.searchInput;

			searchInput.BeforeDraw += (s, e) =>
			{
				if (!searchBox.ResetButton.Visible)
				{
					e.Graphics2D.Render(
						searchIcon,
						searchInput.Width - searchIcon.Width - 5,
						searchInput.LocalBounds.Bottom + searchInput.Height / 2 - searchIcon.Height / 2);
				}
			};

			searchBox.ResetButton.Click += (s, e) =>
			{
				this.ClearSearch();
			};

			searchBox.searchInput.ActualTextEditWidget.TextChanged += (s, e) =>
			{
				if (string.IsNullOrWhiteSpace(searchBox.Text))
				{
					this.ClearSearch();
				}
				else
				{
					this.PerformSearch();
				}
			};

			this.AddChild(searchBox);

			var horizontalSplitter = new Splitter()
			{
				SplitterDistance = Math.Max(UserSettings.Instance.LibraryViewWidth, 20),
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			horizontalSplitter.AnchorAll();

			horizontalSplitter.DistanceChanged += (s, e) =>
			{
				UserSettings.Instance.LibraryViewWidth = Math.Max(horizontalSplitter.SplitterDistance, 20);
			};

			this.AddChild(horizontalSplitter);

			treeView = new TreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			treeView.AfterSelect += async (s, e) =>
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

							printerInfo.CloseAllChildren();

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
										StoreID = storePrinterID?.SID,
										HAnchor = HAnchor.Stretch,
										VAnchor = VAnchor.Fit
									});
							}

							nextButton.Enabled = treeView.SelectedNode != null
								&& !string.IsNullOrWhiteSpace(printerNameInput.Text);
						}
					});
				}
				else
				{
					nextButton.Enabled = false;
				}
			};
			horizontalSplitter.Panel1.AddChild(treeView);

			rootColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(left: 2)
			};
			treeView.AddChild(rootColumn);

			UiThread.RunOnIdle(() =>
			{
				foreach (var oem in OemSettings.Instance.OemProfiles.OrderBy(o => o.Key))
				{
					var rootNode = this.CreateTreeNode(oem);
					rootNode.Expandable = true;
					rootNode.TreeView = treeView;
					rootNode.Load += (s, e) =>
					{
						var image = OemSettings.Instance.GetIcon(oem.Key);

						SetImage(rootNode, image);
					};

					rootColumn.AddChild(rootNode);
				}

				this.TreeLoaded = true;
			});

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(theme.DefaultContainerPadding).Clone(top: 0)
			};

			nameSection = new SectionWidget("New Printer Name".Localize(), container, theme, expandingContent: false)
			{
				HAnchor = HAnchor.Stretch,
				Padding = theme.ToolbarPadding,
				Enabled = false
			};
			theme.ApplyBoxStyle(nameSection);

			printerInfo = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			nameSection.BackgroundColor = theme.MinimalShade;
			nameSection.Margin = new BorderDouble(top: theme.DefaultContainerPadding);

			horizontalSplitter.Panel2.Padding = theme.DefaultContainerPadding;

			var panel2Column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			panel2Column.AddChild(new TextWidget("Select a printer to continue".Localize(), pointSize: theme.DefaultFontSize,  textColor: theme.TextColor));
			panel2Column.AddChild(nameSection);
			panel2Column.AddChild(PrinterNameError = new TextWidget("", 0, 0, 10)
			{
				TextColor = Color.Red,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			});

			panel2Column.AddChild(printerInfo);

			horizontalSplitter.Panel2.AddChild(panel2Column);

			printerNameInput = new MHTextEditWidget("", theme)
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

		public IReadOnlyList<string> ExistingPrinterNames { get; private set; }

		public TextWidget PrinterNameError { get; private set; }


		public bool ValidateControls()
		{
			bool selectionValid = this.SelectedPrinter is AddPrinterWidget.MakeModelInfo selectedPrinter;

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

			nextButton.Enabled = allValid;

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

		private void PerformSearch()
		{
			var matches = new List<TreeNode>();

			Console.WriteLine("Filter for: " + searchBox.Text);

			foreach (var rootNode in rootColumn.Children.OfType<TreeNode>())
			{
				FilterTree(rootNode, searchBox.Text, false, matches);
			}

			if (matches.Count == 1)
			{
				treeView.SelectedNode = matches.First();
			}
			else
			{
				treeView.SelectedNode = null;
			}

			searchBox.ResetButton.Visible = true;
		}

		private void ClearSearch()
		{
			foreach (var rootNode in rootColumn.Children.OfType<TreeNode>())
			{
				ResetTree(rootNode);
			}

			searchBox.Text = "";
			searchBox.ResetButton.Visible = false;
			treeView.SelectedNode = null;
		}

		private bool FilterTree(TreeNode context, string filter, bool parentVisible, List<TreeNode> matches)
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

		private void ResetTree(TreeNode context)
		{
			context.Visible = true;
			context.Expanded = false;

			foreach (var child in context.Nodes)
			{
				ResetTree(child);
			}
		}

		public MakeModelInfo SelectedPrinter { get; private set; }

		public string NewPrinterName => printerNameInput.Text;

		public bool TreeLoaded { get; private set; }

		public class MakeModelInfo
		{
			public string Make { get; set; }

			public string Model { get; set; }

			public override string ToString() => $"{Make} {Model}";
		}
	}
}
