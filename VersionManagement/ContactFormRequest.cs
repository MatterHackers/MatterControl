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
	//To do - move this
	internal class ContactFormRequest : WebRequestBase
	{
		public ContactFormRequest(string question, string details, string email, string firstName, string lastName)
		{
			requestValues["FirstName"] = firstName;
			requestValues["LastName"] = lastName;
			requestValues["Email"] = email;
			requestValues["FeedbackType"] = "Question";
			requestValues["Comment"] = string.Format("{0}\n{1}", question, details);
			uri = $"{MatterControlApplication.MCWSBaseUri}/api/1/submit-feedback";
		}

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			JsonResponseDictionary response = responseValues;
		}

		public override void Request()
		{
			//If the client token exists, use it, otherwise wait for client token before making request
			if (ApplicationSettings.Instance.GetClientToken() == null)
			{
				ClientTokenRequest request = new ClientTokenRequest();
				request.RequestSucceeded += new EventHandler(onClientTokenRequestSucceeded);
				request.Request();
			}
			else
			{
				onClientTokenReady();
			}
		}

		private void onClientTokenRequestSucceeded(object sender, EventArgs e)
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
	}
}