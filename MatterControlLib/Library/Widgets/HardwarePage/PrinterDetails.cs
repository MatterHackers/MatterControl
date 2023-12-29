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
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.Library.Widgets.HardwarePage
{
    public class PrinterDetails : FlowLayoutWidget
	{
		private ThemeConfig theme;
		private GuiWidget headingRow;
		private PrinterInfo printerInfo;
		public GuiWidget ProductDataContainer { get; private set; }
		public event EventHandler AfterLoad;

		public PrinterDetails(PrinterInfo printerInfo, ThemeConfig theme, bool showOpenButton)
			: base(FlowDirection.TopToBottom)
		{
			this.printerInfo = printerInfo;
			this.theme = theme;

			headingRow = this.AddHeading(
				OemSettings.Instance.GetIcon(printerInfo.Make, theme),
				printerInfo.Name);

			headingRow.AddChild(new HorizontalSpacer());
			headingRow.HAnchor = HAnchor.Stretch;

			if (showOpenButton)
			{
				var openButton = new ThemedTextButton("Open".Localize(), theme)
				{
					BackgroundColor = theme.AccentMimimalOverlay,
					Margin = Margin.Clone(right: 17)
				};
				openButton.Click += (s, e) =>
				{
                    throw new NotImplementedException();
                };
				headingRow.AddChild(openButton);
			}

			this.AddChild(headingRow);
		}

		public string StoreID { get; set; }

		public bool ShowHeadingRow { get; set; } = true;

		public bool ShowProducts { get; set; } = true;

		public override void OnLoad(EventArgs args)
		{
			if (string.IsNullOrEmpty(this.StoreID)
				&& File.Exists(printerInfo.ProfilePath))
			{
				// load up the printer profile so we can get the MatterHackers SKU-ID out of it
				var printerSettings = PrinterSettings.LoadFile(printerInfo.ProfilePath);

				this.StoreID = printerSettings.GetValue(SettingsKey.printer_sku);

				// Use the make-model mapping table
				if (string.IsNullOrEmpty(this.StoreID)
					&& OemSettings.Instance.OemPrinters.TryGetValue($"{printerInfo.Make}-{ printerInfo.Model}", out StorePrinterID storePrinterID))
				{
					this.StoreID = storePrinterID?.SID;
				}
			}

			// add a section to hold the data about the printer
			var scrollableWidget = new ScrollableWidget(true);
			scrollableWidget.ScrollArea.HAnchor |= HAnchor.Stretch;
			scrollableWidget.ScrollArea.VAnchor = VAnchor.Fit;
			scrollableWidget.AnchorAll();

			scrollableWidget.AddChild(ProductDataContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch
			});

			this.AddChild(scrollableWidget);

			void DoAfterLoad()
            {
				AfterLoad?.Invoke(this, null);

				scrollableWidget.Width += 1;
				scrollableWidget.Width -= 1;

				scrollableWidget.TopLeftOffset = new Vector2(0, 0);
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
								if (!json.Contains("ErrorCode")
									&& !json.Contains("404 Not Found"))
								{
									try
									{
										var result = JsonConvert.DeserializeObject<ProductSidData>(json);
										result.ProductSku.ProductDescription = result.ProductSku.ProductDescription.Replace("•", "-");
										ProductDataContainer.RemoveChildren();

										foreach (var addOn in result.ProductSku.ProductListing.AddOns)
										{
											WebCache.RetrieveText($"https://mh-pls-prod.appspot.com/p/1/product-sid/{addOn.AddOnSkuReference}?IncludeListingData=True",
												(addOnJson) =>
												{
													var addOnResult = JsonConvert.DeserializeObject<ProductSidData>(addOnJson);

													var icon = new ImageBuffer(80, 0);

													if (addOnResult?.ProductSku?.FeaturedImage?.ImageUrl != null)
													{
														WebCache.RetrieveImageAsync(icon, addOnResult.ProductSku.FeaturedImage.ImageUrl, scaleToImageX: true);
													}

													addOn.Icon = icon;
												});
										}

										CreateProductDataWidgets(result.ProductSku, StoreID);
									}
									catch
									{
									}
								}

								DoAfterLoad();
							});
						});
				}
				catch (Exception ex)
				{
					Trace.WriteLine("Error collecting or loading printer details: " + ex.Message);
				}
			}
			else
			{
				DoAfterLoad();
			}

			headingRow.Visible = this.ShowHeadingRow;

			base.OnLoad(args);
		}

		private void CreateProductDataWidgets(ProductSkuData product, string sku)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: theme.DefaultContainerPadding, right: 5)
			};
			ProductDataContainer.AddChild(row);

			var image = new ImageBuffer(150, 10);

			WebCache.RetrieveImageAsync(image, product.FeaturedImage.ImageUrl, scaleToImageX: true);

			var whiteBackgroundWidget = new WhiteBackground(image);
			row.AddChild(whiteBackgroundWidget);

			if (sku?.Length == 8)
			{
				whiteBackgroundWidget.ImageWidget.Cursor = Cursors.Hand;
				whiteBackgroundWidget.Selectable = true;
				whiteBackgroundWidget.ImageWidget.Click += (s, e) =>
				{
					ApplicationController.LaunchBrowser($"https://www.matterhackers.com/qr/{sku}");
				};
			}

			var descriptionBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Top,
				Padding = theme.DefaultContainerPadding
			};

			var description = new MarkdownWidget(theme)
			{
				MinimumSize = new Vector2(50, 0),
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
				ProductDataContainer.AddChild(addonsSection);
				theme.ApplyBoxStyle(addonsSection);
				addonsSection.Margin = addonsSection.Margin.Clone(left: 0);

				foreach (var item in product.ProductListing.AddOns)
				{
					if (item.Icon.Height == 0)
                    {
						continue;
                    }

					var addOnRow = new AddOnRow(item.AddOnTitle, theme, null, item.Icon)
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
						ApplicationController.LaunchBrowser($"https://www.matterhackers.com/store/l/{item.AddOnListingReference}/sk/{item.AddOnSkuReference}");
					};

					addonsColumn.AddChild(addOnRow);
				}
			}
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
