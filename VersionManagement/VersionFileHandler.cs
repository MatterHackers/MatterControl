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

using MatterHackers.Agg.PlatformAbstract;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class VersionInfo
	{
		private static VersionInfoContainer globalInstance;

		public static VersionInfoContainer Instance
		{
			get
			{
				if (globalInstance == null)
				{
					VersionInfoHandler versionInfoHandler = new VersionInfoHandler();
					globalInstance = versionInfoHandler.ImportVersionInfoFromJson();
				}
				return globalInstance;
			}
		}
	}

	public class VersionInfoContainer
	{
		public VersionInfoContainer()
		{
		}

		public string ReleaseVersion { get; set; }

		public string BuildVersion { get; set; }

		public string BuildToken { get; set; }

		public string ProjectToken { get; set; }
	}

	internal class VersionInfoHandler
	{
		public VersionInfoHandler()
		{
		}

		public VersionInfoContainer ImportVersionInfoFromJson(string loadedFileName = null)
		{
			// TODO: Review all cases below. What happens if we return a default instance of VersionInfoContainer or worse yet, null. It seems likely we end up with a null
			// reference error when someone attempts to use VersionInfo.Instance and it's invalide. Consider removing the error handing below and throwing an error when
			// an error condition is found rather than masking it until the user goes to a section that relies on the instance - thus moving detection of the problem to
			// an earlier stage and expanding the number of cases where it would be noticed.
			string content = loadedFileName == null ?
					StaticData.Instance.ReadAllText(Path.Combine("BuildInfo.txt")) :
					System.IO.File.Exists(loadedFileName) ? System.IO.File.ReadAllText(loadedFileName) : "";

			if (!string.IsNullOrWhiteSpace(content))
			{
				VersionInfoContainer versionInfo = (VersionInfoContainer)Newtonsoft.Json.JsonConvert.DeserializeObject(content, typeof(VersionInfoContainer));
				if (versionInfo == null)
				{
					return new VersionInfoContainer();
				}
				return versionInfo;
			}
			else
			{
				return null;
			}
		}
	}
}