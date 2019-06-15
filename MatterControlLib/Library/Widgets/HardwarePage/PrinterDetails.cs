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
using System.Diagnostics;
using System.IO;
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library.Widgets.HardwarePage
{
	public class PrinterDetails : FlowLayoutWidget
	{
		private ThemeConfig theme;
		private GuiWidget headingRow;
		private PrinterInfo printerInfo;
		private FlowLayoutWidget productDataContainer;

		public PrinterDetails(PrinterInfo printerInfo, ThemeConfig theme, bool showOpenButton)
			: base(FlowDirection.TopToBottom)
		{
			this.printerInfo = printerInfo;
			this.theme = theme;

			headingRow = this.AddHeading(
				OemSettings.Instance.GetIcon(printerInfo.Make),
				printerInfo.Name);

			headingRow.AddChild(new HorizontalSpacer());
			headingRow.HAnchor = HAnchor.Stretch;

			if (showOpenButton)
			{
				var openButton = new TextButton("Open".Localize(), theme)
				{
					BackgroundColor = theme.AccentMimimalOverlay
				};
				openButton.Click += (s, e) =>
				{
					ApplicationController.Instance.OpenPrinter(printerInfo);
				};
				headingRow.AddChild(openButton);
			}

			this.AddChild(headingRow);
		}

		public string StoreID { get; set; }

		public bool ShowHeadingRow { get; set; } = true;

		public bool ShowProducts { get; set; } = true;

		public override async void OnLoad(EventArgs args)
		{
			if (string.IsNullOrEmpty(this.StoreID)
				&& File.Exists(printerInfo.ProfilePath))
			{
				// load up the printer profile so we can get the MatterHackers Skew-ID out of it
				var printerSettings = PrinterSettings.LoadFile(printerInfo.ProfilePath);

				// Use the make-model mapping table
				if (OemSettings.Instance.OemPrinters.TryGetValue($"{printerInfo.Make}-{ printerInfo.Model}", out StorePrinterID storePrinterID))
				{
					this.StoreID = storePrinterID?.SID;
				}
			}

			if (!string.IsNullOrWhiteSpace(StoreID))
			{
				try
				{
					// put in controls from the feed that show relevant printer information
					WebCache.RetrieveText($"https://mh-pls-prod.appspot.com/p/1/product-sid/{StoreID}?IncludeListingData=True",
						(json) =>
						{
							UiThread.RunOnIdle(() =>
							{
								var result = JsonConvert.DeserializeObject<ProductSidData>(json);
								productDataContainer.RemoveAllChildren();
								CreateProductDataWidgets(result.ProductSku);
							});
						});
				}
				catch (Exception ex)
				{
					Trace.WriteLine("Error collecting or loading printer details: " + ex.Message);
				}

				// add a section to hold the data about the printer
				this.AddChild(productDataContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch
				});
			}

			headingRow.Visible = this.ShowHeadingRow;

			base.OnLoad(args);
		}

		//private void CreateProductDataWidgets(PrinterSettings printerSettings, ProductSkuData product)
		private void CreateProductDataWidgets(ProductSkuData product)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: theme.DefaultContainerPadding)
			};
			productDataContainer.AddChild(row);

			var image = new ImageBuffer(150, 10);
			row.AddChild(new ImageWidget(image)
			{
				Margin = new BorderDouble(right: theme.DefaultContainerPadding),
				VAnchor = VAnchor.Top
			});

			WebCache.RetrieveImageAsync(image, product.FeaturedImage.ImageUrl, scaleToImageX: true);

			var descriptionBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = theme.DefaultContainerPadding
			};

			var description = new MarkdownWidget(theme)
			{
				MinimumSize = new VectorMath.Vector2(50, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				AutoScroll = false,
				Markdown = product.ProductDescription.Trim()
			};
			descriptionBackground.AddChild(description);
			descriptionBackground.BeforeDraw += (s, e) =>
			{
				var rect = new RoundedRect(descriptionBackground.LocalBounds, 3);
				e.Graphics2D.Render(rect, theme.SlightShade);
			};

			row.AddChild(descriptionBackground);

			if (this.ShowProducts)
			{
				var padding = theme.DefaultContainerPadding;

				var addonsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Padding = new BorderDouble(padding, padding, padding, 0),
					HAnchor = HAnchor.Stretch
				};

				var addonsSection = new SectionWidget("Upgrades and Accessories", addonsColumn, theme);
				productDataContainer.AddChild(addonsSection);
				theme.ApplyBoxStyle(addonsSection);
				addonsSection.Margin = addonsSection.Margin.Clone(left: 0);



				foreach (var item in product.ProductListing.AddOns)
				{
					var icon = new ImageBuffer(80, 0);
					WebCache.RetrieveImageAsync(icon, item.FeaturedImage.ImageUrl, scaleToImageX: true);

					var addOnRow = new AddOnRow(item.AddOnTitle, theme, null, icon)
					{
						HAnchor = HAnchor.Stretch,
						Cursor = Cursors.Hand
					};

					foreach (var child in addOnRow.Children)
					{
						child.Selectable = false;
					}

					addOnRow.Click += (s, e) =>
					{
						ApplicationController.Instance.LaunchBrowser($"https://www.matterhackers.com/store/l/{item.AddOnListingReference}/sk/{item.AddOnSkuReference}");
					};

					addonsColumn.AddChild(addOnRow);
				}
			}

			//if (false)
			//{
			//	var settingsPanel = new GuiWidget()
			//	{
			//		HAnchor = HAnchor.Stretch,
			//		VAnchor = VAnchor.Stretch,
			//		MinimumSize = new VectorMath.Vector2(20, 20),
			//		DebugShowBounds = true
			//	};

			//	settingsPanel.Load += (s, e) =>
			//	{
			//		var printer = new PrinterConfig(printerSettings);

			//		var settingsContext = new SettingsContext(
			//			printer,
			//			null,
			//			NamedSettingsLayers.All);

			//		settingsPanel.AddChild(
			//			new ConfigurePrinterWidget(settingsContext, printer, theme)
			//			{
			//				HAnchor = HAnchor.Stretch,
			//				VAnchor = VAnchor.Stretch,
			//			});
			//	};

			//	this.AddChild(new SectionWidget("Settings", settingsPanel, theme, expanded: false, setContentVAnchor: false)
			//	{
			//		VAnchor = VAnchor.Stretch
			//	});
			//}
		}

		private GuiWidget AddHeading(ImageBuffer icon, string text)
		{
			var row = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(top: 5)
			};

			row.AddChild(new ImageWidget(icon, false)
			{
				Margin = new BorderDouble(right: 4),
				VAnchor = VAnchor.Center
			});

			row.AddChild(
				new TextWidget(
					text,
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize)
					{
						VAnchor = VAnchor.Center
					});

			return row;
		}

		private GuiWidget AddHeading(string text)
		{
			var row = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(top: 5)
			};

			row.AddChild(
				new TextWidget(
					text,
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize));

			return row;
		}

		private class AddOnRow : SettingsItem
		{
			public AddOnRow(string text, ThemeConfig theme, GuiWidget optionalControls, ImageBuffer icon)
				: base(text, theme, null, optionalControls, icon)
			{
				imageWidget.Cursor = Cursors.Hand;
				imageWidget.Margin = 4;
				imageWidget.Click += (s, e) =>
				{
					optionalControls.InvokeClick();
				};
			}
		}
	}
}
