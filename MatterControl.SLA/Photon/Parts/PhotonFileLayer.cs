/*
 * MIT License
 *
 * Copyright (c) 2018 Bonosoft, 2021 Lars Brubaker c# port
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/**
* by bn on 01/07/2018.
*/

namespace Photon.Parts
{
	public class PhotonFileLayer
	{
		public bool isCalculated;
		private readonly List<PhotonFileLayer> antiAliasLayers = new List<PhotonFileLayer>();
		private int dataAddress;
		private int dataSize;
		private bool extendsMargin;
		private byte[] imageData;
		private List<BitArray> islandRows;
		private int isLandsCount;
		private float layerExposure;
		private float layerOffTimeSeconds;
		private float layerPositionZ;
		private byte[] packedLayerImage;
		private PhotonFileHeader photonFileHeader;
		private long pixels;
		private readonly int unknown1;
		private readonly int unknown2;
		private readonly int unknown3;
		private readonly int unknown4;

		public PhotonFileLayer(PhotonFileLayer photonFileLayer, PhotonFileHeader photonFileHeader)
		{
			layerPositionZ = photonFileLayer.layerPositionZ;
			layerExposure = photonFileLayer.layerExposure;
			layerOffTimeSeconds = photonFileLayer.layerOffTimeSeconds;
			dataAddress = photonFileLayer.dataAddress;
			dataAddress = photonFileLayer.dataSize;

			this.photonFileHeader = photonFileHeader;

			// Dont copy data, we are building new AA layers anyway
			//this.imageData = copy();
			//this.packedLayerImage = copy();
		}

		private PhotonFileLayer(BinaryReader ds)
		{
			layerPositionZ = ds.ReadSingle();
			layerExposure = ds.ReadSingle();
			layerOffTimeSeconds = ds.ReadSingle();

			dataAddress = ds.ReadInt32();
			dataSize = ds.ReadInt32();

			unknown1 = ds.ReadInt32();
			unknown2 = ds.ReadInt32();
			unknown3 = ds.ReadInt32();
			unknown4 = ds.ReadInt32();
		}

		public static void CalculateAALayers(PhotonFileHeader photonFileHeader, List<PhotonFileLayer> layers, PhotonAaMatrix photonAaMatrix, Action<string> reportProgress)
		{
			var photonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
			int[,] source = new int[photonFileHeader.GetResolutionY(), photonFileHeader.GetResolutionX()];

			int i = 0;
			foreach (var layer in layers)
			{
				List<BitArray> unpackedImage = layer.UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());

				reportProgress?.Invoke("Calculating AA for photon file layer " + i + "/" + photonFileHeader.GetNumberOfLayers());

				for (int y = 0; y < photonFileHeader.GetResolutionY(); y++)
				{
					for (int x = 0; x < photonFileHeader.GetResolutionX(); x++)
					{
						source[y, x] = 0;
					}
				}

				for (int y = 0; y < unpackedImage.Count; y++)
				{
					BitArray currentRow = unpackedImage[y];
					if (currentRow != null)
					{
						for (int x = 0; x < currentRow.Length; x++)
						{
							if (currentRow[x])
							{
								source[y, x] = 255;
							}
						}
					}
				}

				// Calc
				int[,] target = photonAaMatrix.Calc(source);

				int aaTresholdDiff = 255 / photonFileHeader.GetAntiAliasingLevel();
				int aaTreshold = 0;
				foreach (var aaFileLayer in layer.antiAliasLayers)
				{
					photonLayer.Clear();
					aaTreshold += aaTresholdDiff;

					for (int y = 0; y < photonFileHeader.GetResolutionY(); y++)
					{
						for (int x = 0; x < photonFileHeader.GetResolutionX(); x++)
						{
							if (target[y, x] >= aaTreshold)
							{
								photonLayer.Supported(x, y);
							}
						}
					}

					aaFileLayer.SaveLayer(photonLayer);
				}

				i++;
			}
			photonLayer.UnLink();
		}

		public static void CalculateLayers(PhotonFileHeader photonFileHeader, List<PhotonFileLayer> layers, int margin, Action<string> reportProgress)
		{
			var photonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
			List<BitArray> previousUnpackedImage = null;
			for (int i=0; i<layers.Count; i++)
			{
				var layer = layers[i];

				List<BitArray> unpackedImage = layer.UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());

				reportProgress?.Invoke("Calculating photon file layer " + i + "/" + photonFileHeader.GetNumberOfLayers());

				if (margin > 0)
				{
					layer.extendsMargin = layer.CheckMargin(unpackedImage, margin);
				}

				layer.UnknownPixels(unpackedImage, photonLayer);

				layer.Calculate(unpackedImage, previousUnpackedImage, photonLayer);

				if (previousUnpackedImage != null)
				{
					previousUnpackedImage.Clear();
				}
				previousUnpackedImage = unpackedImage;

				layer.packedLayerImage = photonLayer.PackLayerImage();
				layer.isCalculated = true;

				if (photonFileHeader.GetVersion() > 1)
				{
					foreach (var aaFileLayer in layer.antiAliasLayers)
					{
						List<BitArray> aaUnpackedImage = aaFileLayer.UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
						var aaPhotonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
						aaFileLayer.UnknownPixels(aaUnpackedImage, aaPhotonLayer);
						aaFileLayer.packedLayerImage = aaPhotonLayer.PackLayerImage();
						aaFileLayer.isCalculated = false;
					}
				}
			}
			photonLayer.UnLink();
		}

		public static void CalculateLayers(PhotonFileHeader photonFileHeader, List<PhotonFileLayer> layers, int margin, int layerIndex)
		{
			var photonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
			List<BitArray> previousUnpackedImage = null;

			if (layerIndex > 0)
			{
				previousUnpackedImage = layers[layerIndex - 1].UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
			}

			for (int i = 0; i < 2; i++)
			{
				PhotonFileLayer layer = layers[layerIndex + i];
				List<BitArray> unpackedImage = layer.UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());

				if (margin > 0)
				{
					layer.extendsMargin = layer.CheckMargin(unpackedImage, margin);
				}

				layer.UnknownPixels(unpackedImage, photonLayer);

				layer.Calculate(unpackedImage, previousUnpackedImage, photonLayer);

				if (previousUnpackedImage != null)
				{
					previousUnpackedImage.Clear();
				}
				previousUnpackedImage = unpackedImage;

				layer.packedLayerImage = photonLayer.PackLayerImage();
				layer.isCalculated = true;

				i++;
			}
			photonLayer.UnLink();
		}

		public static int GetByteSize()
		{
			return 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4;
		}

		public static List<PhotonFileLayer> ReadLayers(PhotonFileHeader photonFileHeader, byte[] fileContent, int margin, Action<string> reportProgress)
		{
			var photonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());

			var layers = new List<PhotonFileLayer>();

			int antiAliasLevel = 1;
			if (photonFileHeader.GetVersion() > 1)
			{
				antiAliasLevel = photonFileHeader.GetAntiAliasingLevel();
			}

			int layerCount = photonFileHeader.GetNumberOfLayers();

			var start = photonFileHeader.GetLayersDefinitionOffsetAddress();
			var ds = new BinaryReader(new MemoryStream(fileContent, start, fileContent.Length - start));
			{
				var layerMap = new Dictionary<int, PhotonFileLayer>();
				for (int i = 0; i < layerCount; i++)
				{
					reportProgress?.Invoke("Reading photon file layer " + (i + 1) + "/" + photonFileHeader.GetNumberOfLayers());

					var layer = new PhotonFileLayer(ds)
					{
						photonFileHeader = photonFileHeader
					};
					layer.imageData = ArraysEmulation.CopyOfRange(fileContent, layer.dataAddress, layer.dataAddress + layer.dataSize);
					layers.Add(layer);
					layerMap[i] = layer;
				}

				if (antiAliasLevel > 1)
				{
					for (int a = 0; a < (antiAliasLevel - 1); a++)
					{
						for (int i = 0; i < layerCount; i++)
						{
							reportProgress?.Invoke("Reading photon file AA " + (2 + a) + "/" + antiAliasLevel + " layer " + (i + 1) + "/" + photonFileHeader.GetNumberOfLayers());

							var layer = new PhotonFileLayer(ds)
							{
								photonFileHeader = photonFileHeader
							};
							layer.imageData = ArraysEmulation.CopyOfRange(fileContent, layer.dataAddress, layer.dataAddress + layer.dataSize);

							layerMap[i].AddAntiAliasLayer(layer);
						}
					}
				}
			}

			photonLayer.UnLink();

			return layers;
		}

		public bool DoExtendMargin()
		{
			return extendsMargin;
		}

		public PhotonFileLayer GetAntiAlias(int a)
		{
			if (antiAliasLayers.Count > a)
			{
				return antiAliasLayers[a];
			}
			return null;
		}

		public List<PhotonFileLayer> GetAntiAlias()
		{
			return antiAliasLayers;
		}

		public List<BitArray> GetIslandRows()
		{
			return islandRows;
		}

		public int GetIsLandsCount()
		{
			return isLandsCount;
		}

		public PhotonLayer GetLayer()
		{
			var photonLayer = new PhotonLayer(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
			photonLayer.UnpackLayerImage(packedLayerImage);
			return photonLayer;
		}

		public float GetLayerExposure()
		{
			return layerExposure;
		}

		public float GetLayerOffTime()
		{
			return layerOffTimeSeconds;
		}

		public float GetLayerPositionZ()
		{
			return layerPositionZ;
		}

		public long GetPixels()
		{
			return pixels;
		}

		public List<PhotonRow> GetRows()
		{
			return PhotonLayer.GetRows(packedLayerImage, photonFileHeader.GetResolutionX(), isCalculated);
		}

		public List<BitArray> GetUnknownRows()
		{
			return UnpackImage(photonFileHeader.GetResolutionX(), photonFileHeader.GetResolutionY());
		}

		public void GetUpdateLayer(PhotonLayer photonLayer)
		{
			photonLayer.UnpackLayerImage(packedLayerImage);
		}

		public void Save(BinaryWriter writer)
		{
			writer.Write(layerPositionZ);
			writer.Write(layerExposure);
			writer.Write(layerOffTimeSeconds);

			writer.Write(dataAddress);
			writer.Write(dataSize);

			writer.Write(unknown1);
			writer.Write(unknown2);
			writer.Write(unknown3);
			writer.Write(unknown4);
		}

		public void SaveData(BinaryWriter writer)
		{
			writer.Write(imageData, 0, dataSize);
		}

		public void SaveLayer(PhotonLayer photonLayer)
		{
			this.packedLayerImage = photonLayer.PackLayerImage();
			this.imageData = photonLayer.PackImageData();
			this.dataSize = imageData.Length;
			islandRows = new List<BitArray>();
			isLandsCount = photonLayer.SetIslands(islandRows);
		}

		public int SavePos(int dataPosition)
		{
			dataAddress = dataPosition;
			return dataPosition + dataSize;
		}

		public void SetLayerExposure(float layerExposure)
		{
			this.layerExposure = layerExposure;
		}

		public void SetLayerOffTimeSeconds(float layerOffTimeSeconds)
		{
			this.layerOffTimeSeconds = layerOffTimeSeconds;
		}

		public void SetLayerPositionZ(float layerPositionZ)
		{
			this.layerPositionZ = layerPositionZ;
		}

		public void UnLink()
		{
			imageData = null;
			packedLayerImage = null;
			if (islandRows != null)
			{
				islandRows.Clear();
			}
			photonFileHeader = null;
		}

		public List<BitArray> UnpackImage(int resolutionX, int resolutionY)
		{
			pixels = 0;
			resolutionX -= 1;
			var unpackedImage = new List<BitArray>(resolutionY);
			var currentRow = new BitArray(resolutionX);
			unpackedImage.Add(currentRow);
			int x = 0;
			foreach (var rle in imageData)
			{
				int length = rle & 0x7F;
				bool color = (rle & 0x80) == 0x80;
				if (color)
				{
					pixels += length;
				}
				int endPosition = x + (length - 1);
				int lineEnd = Math.Min(endPosition, resolutionX);
				if (color)
				{
					currentRow.Set(x, 1 + lineEnd);
				}
				if (endPosition > resolutionX)
				{
					currentRow = new BitArray(resolutionX);
					unpackedImage.Add(currentRow);
					lineEnd = endPosition - (resolutionX + 1);
					if (color)
					{
						currentRow.Set(0, 1 + lineEnd);
					}
				}
				x = lineEnd + 1;
				if (x > resolutionX)
				{
					currentRow = new BitArray(resolutionX);
					unpackedImage.Add(currentRow);
					x = 0;
				}
			}
			return unpackedImage;
		}

		public void UpdateLayerIslands(PhotonLayer photonLayer)
		{
			islandRows = new List<BitArray>();
			isLandsCount = photonLayer.SetIslands(islandRows);
		}

		private void AaPixels(List<BitArray> unpackedImage, PhotonLayer photonLayer)
		{
			photonLayer.Clear();

			for (int y = 0; y < unpackedImage.Count; y++)
			{
				BitArray currentRow = unpackedImage[y];
				if (currentRow != null)
				{
					for (int x = 0; x < currentRow.Length; x++)
					{
						if (currentRow[x])
						{
							photonLayer.UnSupported(x, y);
						}
					}
				}
			}
		}

		private void AddAntiAliasLayer(PhotonFileLayer layer)
		{
			antiAliasLayers.Add(layer);
		}

		private void Calculate(List<BitArray> unpackedImage, List<BitArray> previousUnpackedImage, PhotonLayer photonLayer)
		{
			islandRows = new List<BitArray>();
			isLandsCount = 0;

			photonLayer.Clear();

			for (int y = 0; y < unpackedImage.Count; y++)
			{
				BitArray currentRow = unpackedImage[y];
				BitArray prevRow = previousUnpackedImage?[y];
				if (currentRow != null)
				{
					int x = 0;
					while ((x = currentRow.NextSetBit(x)) >= 0)
					{
						if (prevRow == null || prevRow[x])
						{
							photonLayer.Supported(x, y);
						}
						else
						{
							photonLayer.Island(x, y);
						}
						++x;
					}
				}
			}

			photonLayer.Reduce();

			isLandsCount = photonLayer.SetIslands(islandRows);
		}

		private bool CheckMargin(List<BitArray> unpackedImage, int margin)
		{
			if (unpackedImage.Count > margin)
			{
				// check top margin rows
				for (int i = 0; i < margin; i++)
				{
					if (unpackedImage[i].Count > 0)
					{
						return true;
					}
				}
				// check bottom margin rows
				for (int i = unpackedImage.Count - margin; i < unpackedImage.Count; i++)
				{
					if (unpackedImage[i].Count > 0)
					{
						return true;
					}
				}

				for (int i = margin; i < unpackedImage.Count - margin; i++)
				{
					BitArray row = unpackedImage[i];
					int nextBit = row.NextSetBit(0);
					if (nextBit >= 0 && nextBit < margin)
					{
						return true;
					}
					nextBit = row.NextSetBit(photonFileHeader.GetResolutionX() - margin);
					if (nextBit > photonFileHeader.GetResolutionX() - margin)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void UnknownPixels(List<BitArray> unpackedImage, PhotonLayer photonLayer)
		{
			photonLayer.Clear();

			for (int y = 0; y < unpackedImage.Count; y++)
			{
				BitArray currentRow = unpackedImage[y];
				if (currentRow != null)
				{
					int x = 0;
					while ((x = currentRow.NextSetBit(x)) >= 0)
					{
						photonLayer.Supported(x, y);
						++x;
					}
				}
			}
		}
	}
}