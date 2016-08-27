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

using MatterHackers.Localizations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.VersionManagement
{
	public class LatestVersionRequest : WebRequestBase
	{
		public static class VersionKey
		{
			public const string CurrentBuildToken = nameof(CurrentBuildToken);
			public const string CurrentBuildNumber = nameof(CurrentBuildNumber);
			public const string CurrentBuildUrl = nameof(CurrentBuildUrl);
			public const string CurrentReleaseVersion = nameof(CurrentReleaseVersion);
			public const string CurrentReleaseDate = nameof(CurrentReleaseDate);
			public const string UpdateRequired = nameof(UpdateRequired);
		}

		public static string[] VersionKeys = 
		{
			VersionKey.CurrentBuildToken,
			VersionKey.CurrentBuildNumber,
			VersionKey.CurrentBuildUrl,
			VersionKey.CurrentReleaseVersion,
			VersionKey.CurrentReleaseDate,
			VersionKey.UpdateRequired,
		};

		public LatestVersionRequest()
		{
			string feedType = UserSettings.Instance.get("UpdateFeedType");
			if (feedType == null)
			{
				feedType = "release";
				UserSettings.Instance.set("UpdateFeedType", feedType);
			}
			requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
			//requestValues["TestCode"] = "100"; // will cause response of update available and not required
			//requestValues["TestCode"] = "101"; // will cause response of update available and required
			requestValues["UpdateFeedType"] = feedType;
			uri = $"{MatterControlApplication.MCWSBaseUri}/api/1/get-current-release-version";
		}

		public override void Request()
		{
			//If the client token exists, use it, otherwise wait for client token before making request
			if (ApplicationSettings.Instance.GetClientToken() == null)
			{
				ClientTokenRequest request = new ClientTokenRequest();
				request.RequestSucceeded += new EventHandler(onRequestSucceeded);
				request.Request();
			}
			else
			{
				onClientTokenReady();
			}
		}

		private void onRequestSucceeded(object sender, EventArgs e)
		{
			onClientTokenReady();
		}

		public void onClientTokenReady()
		{
			string clientToken = ApplicationSettings.Instance.GetClientToken();
			requestValues["ClientToken"] = clientToken;
			if (clientToken != null)
			{
				base.Request();
			}
		}

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			foreach (string key in VersionKeys)
			{
				saveResponse(key, responseValues);
			}
		}

		private void saveResponse(string key, JsonResponseDictionary responseValues)
		{
			string value = responseValues.get(key);
			if (value != null)
			{
				ApplicationSettings.Instance.set(key, value);
			}
		}
	}
}