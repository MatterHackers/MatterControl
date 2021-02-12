/*
 * MIT License
 *

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
using System.Collections.Generic;
using System.IO;
using System.Text;

/**
* by bn on 30/06/2018.
*/

namespace Photon.Parts
{
	public class PhotonFile
	{
		private IFileHeader iFileHeader;
		private int islandLayerCount;
		private List<int> islandLayers;
		private StringBuilder islandList;
		private List<PhotonFileLayer> layers;
		private int margin;
		private List<int> marginLayers;
		private PhotonFilePreview previewOne;
		private PhotonFilePreview previewTwo;

		public void AdjustLayerSettings()
		{
			for (int i = 0; i < layers.Count; i++)
			{
				PhotonFileLayer layer = layers[i];
				if (i < iFileHeader.GetBottomLayers())
				{
					layer.SetLayerExposure(iFileHeader.GetBottomExposureTimeSeconds());
				}
				else
				{
					layer.SetLayerExposure(iFileHeader.GetNormalExposure());
				}
				layer.SetLayerOffTimeSeconds(iFileHeader.GetOffTimeSeconds());
			}
		}

		public void Calculate(Action<string> reportProgress)
		{
			PhotonFileLayer.CalculateLayers((PhotonFileHeader)iFileHeader, layers, margin, reportProgress);
			ResetMarginAndIslandInfo();
		}

		public void Calculate(int layerNo)
		{
			PhotonFileLayer.CalculateLayers((PhotonFileHeader)iFileHeader, layers, margin, layerNo);
			ResetMarginAndIslandInfo();
		}

		public void CalculateAaLayers(Action<string> reportProgress, PhotonAaMatrix photonAaMatrix)
		{
			PhotonFileLayer.CalculateAALayers((PhotonFileHeader)iFileHeader, layers, photonAaMatrix, reportProgress);
		}

		public void ChangeToVersion2()
		{
			iFileHeader.SetFileVersion(2);
		}

		public void FixAll(Action<string> reportProgress)
		{
			bool layerWasFixed;
			do
			{
				do
				{
					// Repeatedly fix layers until none are possible to fix
					// Fixing some layers can make other layers auto-fixable
					layerWasFixed = FixLayers(reportProgress);
				} while (layerWasFixed);
				if (islandLayers.Count > 0)
				{
					// Nothing can be done further, just remove all layers left
					layerWasFixed = RemoveAllIslands(reportProgress) || layerWasFixed;
				}
				if (layerWasFixed && islandLayers.Count > 0)
				{
					// We could've created new islands by removing islands, repeat fixing process
					// until everything is fixed or nothing can be done
					reportProgress?.Invoke("<br>Some layers were fixed, but " + islandLayers.Count + " still unsupported, repeating...<br>");
				}
			} while (layerWasFixed);
		}

		public void FixLayerHeights()
		{
			int index = 0;
			foreach (var layer in layers)
			{
				layer.SetLayerPositionZ(index * iFileHeader.GetLayerHeight());
				index++;
			}
		}

		public bool FixLayers(Action<string> reportProgress)
		{
			bool layersFixed = false;
			PhotonLayer layer = null;
			foreach (int layerNo in islandLayers)
			{
				reportProgress?.Invoke("Checking layer " + layerNo);

				// Unpack the layer data to the layer utility class
				PhotonFileLayer fileLayer = layers[layerNo];
				if (layer == null)
				{
					layer = fileLayer.GetLayer();
				}
				else
				{
					fileLayer.GetUpdateLayer(layer);
				}

				int changed = Fixit(reportProgress, layer, fileLayer, 10);
				if (changed == 0)
				{
					reportProgress?.Invoke(", but nothing could be done.");
				}
				else
				{
					fileLayer.SaveLayer(layer);
					Calculate(layerNo);
					if (layerNo < GetLayerCount() - 1)
					{
						Calculate(layerNo + 1);
					}
					layersFixed = true;
				}

				reportProgress?.Invoke("<br>");
			}
			FindIslands();
			return layersFixed;
		}

		public int GetAALevels()
		{
			return iFileHeader.GetAALevels();
		}

		public int GetHeight()
		{
			return iFileHeader.GetResolutionX();
		}

		public string GetInformation()
		{
			if (iFileHeader == null) return "";
			return iFileHeader.GetInformation();
		}

		public int GetIslandLayerCount()
		{
			if (islandList == null)
			{
				FindIslands();
			}
			return islandLayerCount;
		}

		public List<int> GetIslandLayers()
		{
			if (islandList == null)
			{
				FindIslands();
			}
			return islandLayers;
		}

		public PhotonFileLayer GetLayer(int i)
		{
			if (layers != null && layers.Count > i)
			{
				return layers[i];
			}
			return null;
		}

		public int GetLayerCount()
		{
			return iFileHeader.GetNumberOfLayers();
		}

		public string GetLayerInformation()
		{
			if (islandList == null)
			{
				FindIslands();
			}
			if (islandLayerCount == 0)
			{
				return "Whoopee, all is good, no unsupported areas";
			}
			else if (islandLayerCount == 1)
			{
				return "Unsupported islands found in layer " + islandList.ToString();
			}
			return "Unsupported islands found in layers " + islandList.ToString();
		}

		public string GetMarginInformation()
		{
			if (marginLayers == null)
			{
				return "No safety margin set, printing to the border.";
			}
			else
			{
				if (marginLayers.Count == 0)
				{
					return "The model is within the defined safety margin (" + this.margin + " pixels).";
				}
				else if (marginLayers.Count == 1)
				{
					return "The layer " + marginLayers[0] + " contains model parts that extend beyond the margin.";
				}
				var marginList = new StringBuilder();
				int count = 0;
				foreach (var layer in marginLayers)
				{
					if (count > 10)
					{
						marginList.Append(", ...");
						break;
					}
					else
					{
						if (marginList.Length > 0) marginList.Append(", ");
						marginList.Append(layer);
					}
					count++;
				}
				return "The layers " + marginList.ToString() + " contains model parts that extend beyond the margin.";
			}
		}

		public List<int> GetMarginLayers()
		{
			if (marginLayers == null)
			{
				return new List<int>();
			}
			return marginLayers;
		}

		public IFileHeader GetPhotonFileHeader()
		{
			return iFileHeader;
		}

		public long GetPixels()
		{
			long total = 0;
			if (layers != null)
			{
				foreach (var layer in layers)
				{
					total += layer.GetPixels();
				}
			}
			return total;
		}

		public PhotonFilePreview GetPreviewOne()
		{
			return previewOne;
		}

		public PhotonFilePreview GetPreviewTwo()
		{
			return previewTwo;
		}

		public int GetVersion()
		{
			return iFileHeader.GetVersion();
		}

		public int GetWidth()
		{
			return iFileHeader.GetResolutionY();
		}

		public float GetZdrift()
		{
			float expectedHeight = iFileHeader.GetLayerHeight() * (iFileHeader.GetNumberOfLayers() - 1);
			float actualHeight = layers[layers.Count - 1].GetLayerPositionZ();
			return expectedHeight - actualHeight;
		}

		public bool HasAA()
		{
			return iFileHeader.HasAA();
		}

		public PhotonFile ReadFile(string fileName, Action<string> reportProgress)
		{
			using (var file = File.OpenRead(fileName))
			{
				return ReadFile(GetBinaryData(file), reportProgress);
			}
		}

		public bool RemoveAllIslands(Action<string> reportProgress)
		{
			bool layersFixed = false;
			reportProgress?.Invoke("Removing islands from " + islandLayers.Count + " layers...<br>");
			PhotonLayer layer = null;
			foreach (var layerNo in islandLayers)
			{
				PhotonFileLayer fileLayer = layers[layerNo];
				if (layer == null)
				{
					layer = fileLayer.GetLayer();
				}
				else
				{
					fileLayer.GetUpdateLayer(layer);
				}
				reportProgress?.Invoke("Removing islands from layer " + layerNo);

				int removed = layer.RemoveIslands();
				if (removed == 0)
				{
					reportProgress?.Invoke(", but nothing could be done.");
				}
				else
				{
					reportProgress?.Invoke(", " + removed + " islands removed");
					fileLayer.SaveLayer(layer);
					Calculate(layerNo);
					if (layerNo < GetLayerCount() - 1)
					{
						Calculate(layerNo + 1);
					}
					layersFixed = true;
				}
				reportProgress?.Invoke("<br>");
			}
			FindIslands();
			return layersFixed;
		}

		public void SaveFile(string fileName)
		{
			using (var fileOutputStream = new BinaryWriter(File.OpenWrite(fileName)))
			{
				WriteFile(fileOutputStream);
			}
		}

		// only call this when recalculating AA levels
		public void SetAALevels(int levels)
		{
			iFileHeader.SetAALevels(levels, layers);
		}

		public void SetMargin(int margin)
		{
			this.margin = margin;
		}

		public void UnLink()
		{
			while (layers.Count > 0)
			{
				PhotonFileLayer layer = layers[0];
				layers.RemoveAt(0);
				layer.UnLink();
			}
			if (islandLayers != null)
			{
				islandLayers.Clear();
			}
			if (marginLayers != null)
			{
				marginLayers.Clear();
			}
			iFileHeader.UnLink();
			iFileHeader = null;
			previewOne.UnLink();
			previewOne = null;
			previewTwo.UnLink();
			previewTwo = null;
		}

		private void FindIslands()
		{
			if (islandLayers != null)
			{
				islandLayers.Clear();
				islandList = new StringBuilder();
				islandLayerCount = 0;
				if (layers != null)
				{
					for (int i = 0; i < iFileHeader.GetNumberOfLayers(); i++)
					{
						PhotonFileLayer layer = layers[i];
						if (layer.GetIsLandsCount() > 0)
						{
							if (islandLayerCount < 11)
							{
								if (islandLayerCount == 10)
								{
									islandList.Append(", ...");
								}
								else
								{
									if (islandList.Length > 0) islandList.Append(", ");
									islandList.Append(i);
								}
							}
							islandLayerCount++;
							islandLayers.Add(i);
						}
					}
				}
			}
		}

		private int Fixit(Action<string> reportProgress, PhotonLayer layer, PhotonFileLayer fileLayer, int loops)
		{
			int changed = layer.Fixlayer();
			if (changed > 0)
			{
				layer.Reduce();
				fileLayer.UpdateLayerIslands(layer);
				reportProgress?.Invoke(", " + changed + " pixels changed");
				if (loops > 0)
				{
					changed += Fixit(reportProgress, layer, fileLayer, loops - 1);
				}
			}
			return changed;
		}

		private byte[] GetBinaryData(FileStream entry)
		{
			int fileSize = (int)entry.Length;
			byte[] fileData = new byte[fileSize];

			var stream = new BinaryReader(entry);
			int bytesRead = 0;
			while (bytesRead < fileSize)
			{
				int readCount = stream.Read(fileData, bytesRead, fileSize - bytesRead);
				if (readCount < 0)
				{
					throw new IOException("Could not read all bytes of the file");
				}
				bytesRead += readCount;
			}

			return fileData;
		}

		private PhotonFile ReadFile(byte[] file, Action<string> reportProgress)
		{
			reportProgress?.Invoke("Reading Photon file header information...");
			var photonFileHeader = new PhotonFileHeader(file);
			iFileHeader = photonFileHeader;

			reportProgress?.Invoke("Reading photon large preview image information...");
			previewOne = new PhotonFilePreview(photonFileHeader.GetPreviewOneOffsetAddress(), file);
			reportProgress?.Invoke("Reading photon small preview image information...");
			previewTwo = new PhotonFilePreview(photonFileHeader.GetPreviewTwoOffsetAddress(), file);
			if (photonFileHeader.GetVersion() > 1)
			{
				reportProgress?.Invoke("Reading Print parameters information...");
				photonFileHeader.ReadParameters(file);
			}
			reportProgress?.Invoke("Reading photon layers information...");
			layers = PhotonFileLayer.ReadLayers(photonFileHeader, file, margin, reportProgress);
			ResetMarginAndIslandInfo();

			return this;
		}

		private void ResetMarginAndIslandInfo()
		{
			islandList = null;
			islandLayerCount = 0;
			islandLayers = new List<int>();

			if (margin > 0)
			{
				marginLayers = new List<int>();
				int i = 0;
				foreach (PhotonFileLayer layer in layers)
				{
					if (layer.DoExtendMargin())
					{
						marginLayers.Add(i);
					}
					i++;
				}
			}
		}

		private void WriteFile(BinaryWriter writer)
		{
			int antiAliasLevel = iFileHeader.GetAALevels();

			int headerPos = 0;
			int previewOnePos = headerPos + iFileHeader.GetByteSize();
			int previewTwoPos = previewOnePos + previewOne.GetByteSize();
			int layerDefinitionPos = previewTwoPos + previewTwo.GetByteSize();

			int parametersPos = 0;
			int machineInfoPos = 0;
			if (iFileHeader.GetVersion() > 1)
			{
				parametersPos = layerDefinitionPos;
				if (((PhotonFileHeader)iFileHeader).photonFileMachineInfo.GetByteSize() > 0)
				{
					machineInfoPos = parametersPos + ((PhotonFileHeader)iFileHeader).photonFilePrintParameters.GetByteSize();
					layerDefinitionPos = machineInfoPos + ((PhotonFileHeader)iFileHeader).photonFileMachineInfo.GetByteSize();
				}
				else
				{
					layerDefinitionPos = parametersPos + ((PhotonFileHeader)iFileHeader).photonFilePrintParameters.GetByteSize();
				}
			}

			int dataPosition = layerDefinitionPos + (PhotonFileLayer.GetByteSize() * iFileHeader.GetNumberOfLayers() * antiAliasLevel);

			((PhotonFileHeader)iFileHeader).Save(writer, previewOnePos, previewTwoPos, layerDefinitionPos, parametersPos, machineInfoPos);
			previewOne.Save(writer, previewOnePos);
			previewTwo.Save(writer, previewTwoPos);

			if (iFileHeader.GetVersion() > 1)
			{
				((PhotonFileHeader)iFileHeader).photonFilePrintParameters.Save(writer);
				((PhotonFileHeader)iFileHeader).photonFileMachineInfo.Save(writer, machineInfoPos);
			}

			// Optimize order for speed read on photon
			for (int i = 0; i < iFileHeader.GetNumberOfLayers(); i++)
			{
				PhotonFileLayer layer = layers[i];
				dataPosition = layer.SavePos(dataPosition);
				if (antiAliasLevel > 1)
				{
					for (int a = 0; a < (antiAliasLevel - 1); a++)
					{
						dataPosition = layer.GetAntiAlias(a).SavePos(dataPosition);
					}
				}
			}

			// Order for backward compatibility with photon/cbddlp version 1
			for (int i = 0; i < iFileHeader.GetNumberOfLayers(); i++)
			{
				layers[i].Save(writer);
			}

			if (antiAliasLevel > 1)
			{
				for (int a = 0; a < (antiAliasLevel - 1); a++)
				{
					for (int i = 0; i < iFileHeader.GetNumberOfLayers(); i++)
					{
						layers[i].GetAntiAlias(a).Save(writer);
					}
				}
			}

			// Optimize order for speed read on photon
			for (int i = 0; i < iFileHeader.GetNumberOfLayers(); i++)
			{
				PhotonFileLayer layer = layers[i];
				layer.SaveData(writer);
				if (antiAliasLevel > 1)
				{
					for (int a = 0; a < (antiAliasLevel - 1); a++)
					{
						layer.GetAntiAlias(a).SaveData(writer);
					}
				}
			}
		}
	}
}