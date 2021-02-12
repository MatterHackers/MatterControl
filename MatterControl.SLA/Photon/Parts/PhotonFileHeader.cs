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

using System.Collections.Generic;
using System.IO;

/**
*  by bn on 30/06/2018.
*/

namespace Photon.Parts
{
	public class PhotonFileHeader : IFileHeader
	{
		public PhotonFileMachineInfo photonFileMachineInfo;
		public PhotonFilePrintParameters photonFilePrintParameters;
		private int antiAliasingLevel;
		private readonly float bedXmm;
		private readonly float bedYmm;
		private readonly float bedZmm;
		private int bottomLayers;
		private short bottomLightPWM;
		private float exposureBottomTimeSeconds;
		private float exposureTimeSeconds;
		private readonly int header1;
		private readonly float layerHeightMilimeter;
		private int layersDefinitionOffsetAddress;
		private short lightPWM;
		private int machineInfoOffsetAddress;
		private readonly int machineInfoSize;
		private readonly int numberOfLayers;
		private float offTimeSeconds;
		private int previewOneOffsetAddress;
		private int previewTwoOffsetAddress;
		private int printParametersOffsetAddress;
		private readonly int printParametersSize;
		private readonly int printTimeSeconds;
		private readonly PhotonProjectType projectType;
		private readonly int resolutionX;
		private readonly int resolutionY;
		private readonly int unknown1;
		private readonly int unknown2;
		private readonly int unknown3;
		private readonly int unknown4;
		private int version;

		public PhotonFileHeader(byte[] fileContent)
		{
			var reader = new BinaryReader(new MemoryStream(fileContent));

			header1 = reader.ReadInt32();
			version = reader.ReadInt32();

			bedXmm = reader.ReadSingle();
			bedYmm = reader.ReadSingle();
			bedZmm = reader.ReadSingle();

			unknown1 = reader.ReadInt32();
			unknown2 = reader.ReadInt32();
			unknown3 = reader.ReadInt32();

			layerHeightMilimeter = reader.ReadSingle();
			exposureTimeSeconds = reader.ReadSingle();
			exposureBottomTimeSeconds = reader.ReadSingle();

			offTimeSeconds = reader.ReadSingle();
			bottomLayers = reader.ReadInt32();

			resolutionX = reader.ReadInt32();
			resolutionY = reader.ReadInt32();

			previewOneOffsetAddress = reader.ReadInt32();
			layersDefinitionOffsetAddress = reader.ReadInt32();

			numberOfLayers = reader.ReadInt32();

			previewTwoOffsetAddress = reader.ReadInt32();
			printTimeSeconds = reader.ReadInt32();

			projectType = (PhotonProjectType)reader.ReadInt32();

			printParametersOffsetAddress = reader.ReadInt32();
			printParametersSize = reader.ReadInt32();
			antiAliasingLevel = reader.ReadInt32();

			lightPWM = reader.ReadInt16();
			bottomLightPWM = reader.ReadInt16();

			unknown4 = reader.ReadInt32();
			machineInfoOffsetAddress = reader.ReadInt32();
			if (version > 1)
			{
				machineInfoSize = reader.ReadInt32();
			}
		}

		public int GetAALevels()
		{
			if (GetVersion() > 1)
			{
				return GetAntiAliasingLevel();
			}
			return 1;
		}

		public int GetAntiAliasingLevel()
		{
			return antiAliasingLevel;
		}

		public float GetBottomExposureTimeSeconds()
		{
			return exposureBottomTimeSeconds;
		}

		public int GetBottomLayers()
		{
			return bottomLayers;
		}

		public float GetBuildAreaX()
		{
			return bedXmm;
		}

		public float GetBuildAreaY()
		{
			return bedYmm;
		}

		public int GetByteSize()
		{
			return 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 4 + 2 + 2 + 4 + 4 + (version > 1 ? 4 : 0);
		}

		public float GetExposureTimeSeconds()
		{
			return exposureTimeSeconds;
		}

		public string GetInformation()
		{
			return string.Format("T: %.3f", layerHeightMilimeter) +
					", E: " + exposureTimeSeconds +
					", O: " + offTimeSeconds +
					", BE: " + exposureBottomTimeSeconds +
					string.Format(", BL: %d", bottomLayers);
		}

		public float GetLayerHeight()
		{
			return layerHeightMilimeter;
		}

		public int GetLayersDefinitionOffsetAddress()
		{
			return layersDefinitionOffsetAddress;
		}

		public int GetMachineInfoOffsetAddress()
		{
			return machineInfoOffsetAddress;
		}

		public int GetMachineInfoSize()
		{
			return machineInfoSize;
		}

		public float GetNormalExposure()
		{
			return exposureTimeSeconds;
		}

		public int GetNumberOfLayers()
		{
			return numberOfLayers;
		}

		public float GetOffTime()
		{
			return offTimeSeconds;
		}

		public float GetOffTimeSeconds()
		{
			return offTimeSeconds;
		}

		public int GetPreviewOneOffsetAddress()
		{
			return previewOneOffsetAddress;
		}

		public int GetPreviewTwoOffsetAddress()
		{
			return previewTwoOffsetAddress;
		}

		public int GetPrintParametersOffsetAddress()
		{
			return printParametersOffsetAddress;
		}

		public int GetPrintParametersSize()
		{
			return printParametersSize;
		}

		public int GetPrintTimeSeconds()
		{
			return printTimeSeconds;
		}

		public int GetResolutionX()
		{
			return resolutionX;
		}

		public int GetResolutionY()
		{
			return resolutionY;
		}

		public int GetVersion()
		{
			return version;
		}

		public bool HasAA()
		{
			return (GetVersion() > 1 && GetAntiAliasingLevel() > 1);
		}

		public bool IsMirrored()
		{
			return projectType == PhotonProjectType.lcdMirror;
		}

		public void ReadParameters(byte[] file)
		{
			photonFilePrintParameters = new PhotonFilePrintParameters(GetPrintParametersOffsetAddress(), file);
			photonFileMachineInfo = new PhotonFileMachineInfo(GetMachineInfoOffsetAddress(), GetMachineInfoSize(), file);
		}

		public void Save(BinaryWriter os, int previewOnePos, int previewTwoPos, int layerDefinitionPos, int parametersPos, int machineInfoPos)
		{
			previewOneOffsetAddress = previewOnePos;
			previewTwoOffsetAddress = previewTwoPos;
			layersDefinitionOffsetAddress = layerDefinitionPos;
			printParametersOffsetAddress = parametersPos;
			machineInfoOffsetAddress = machineInfoPos;

			os.Write(header1);
			os.Write(version);

			os.Write(bedXmm);
			os.Write(bedYmm);
			os.Write(bedZmm);

			os.Write(unknown1);
			os.Write(unknown2);
			os.Write(unknown3);

			os.Write(layerHeightMilimeter);
			os.Write(exposureTimeSeconds);
			os.Write(exposureBottomTimeSeconds);

			os.Write(offTimeSeconds);
			os.Write(bottomLayers);

			os.Write(resolutionX);
			os.Write(resolutionY);

			os.Write(previewOneOffsetAddress);
			os.Write(layersDefinitionOffsetAddress);

			os.Write(numberOfLayers);

			os.Write(previewTwoOffsetAddress);
			os.Write(printTimeSeconds);

			os.Write((int)projectType);

			os.Write(printParametersOffsetAddress);
			os.Write(printParametersSize);
			os.Write(antiAliasingLevel);

			os.Write(lightPWM);
			os.Write(bottomLightPWM);

			os.Write(unknown4);
			os.Write(machineInfoOffsetAddress);
			if (version > 1)
			{
				os.Write(machineInfoSize);
			}
		}

		public void SetAALevels(int levels, List<PhotonFileLayer> layers)
		{
			if (GetVersion() > 1)
			{
				if (levels < GetAntiAliasingLevel())
				{
					ReduceAaLevels(levels, layers);
				}
				if (levels > GetAntiAliasingLevel())
				{
					IncreaseAaLevels(levels, layers);
				}
			}
		}

		public void SetAntiAliasingLevel(int antiAliasingLevel)
		{
			this.antiAliasingLevel = antiAliasingLevel;
		}

		public void SetBottomLayers(int bottomLayers)
		{
			this.bottomLayers = bottomLayers;
		}

		public void SetExposureBottomTimeSeconds(float exposureBottomTimeSeconds)
		{
			this.exposureBottomTimeSeconds = exposureBottomTimeSeconds;
		}

		public void SetExposureTimeSeconds(float exposureTimeSeconds)
		{
			this.exposureTimeSeconds = exposureTimeSeconds;
		}

		public void SetFileVersion(int i)
		{
			version = i;
			antiAliasingLevel = 1;
			lightPWM = 255;
			bottomLightPWM = 255;

			photonFilePrintParameters = new PhotonFilePrintParameters(GetBottomLayers());
		}

		public void SetOffTimeSeconds(float offTimeSeconds)
		{
			this.offTimeSeconds = offTimeSeconds;
		}

		public void UnLink()
		{
		}

		private void IncreaseAaLevels(int levels, List<PhotonFileLayer> layers)
		{
			// insert base layer to the correct count, as we are to recalculate the AA anyway
			foreach (var photonFileLayer in layers)
			{
				while (photonFileLayer.GetAntiAlias().Count < (levels - 1))
				{
					photonFileLayer.GetAntiAlias().Add(new PhotonFileLayer(photonFileLayer, this));
				}
			}
			SetAntiAliasingLevel(levels);
		}

		private void ReduceAaLevels(int levels, List<PhotonFileLayer> layers)
		{
			// delete any layers to the correct count, as we are to recalculate the AA anyway
			foreach (var photonFileLayer in layers)
			{
				while (photonFileLayer.GetAntiAlias().Count > (levels - 1))
				{
					photonFileLayer.GetAntiAlias().RemoveAt(0);
				}
			}
			SetAntiAliasingLevel(levels);
		}
	}
}