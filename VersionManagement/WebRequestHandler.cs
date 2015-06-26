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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MatterHackers.MatterControl.VersionManagement
{
	public class ResponseErrorEventArgs : EventArgs
	{
		public JsonResponseDictionary ResponseValues { get; set; }
	}

	public class ResponseSuccessEventArgs<T> : EventArgs
	{
		public T ResponseItem { get; set; }
	}

	public class WebRequestBase<T> where T : class 
	{
		protected string uri;
		protected Dictionary<string, string> requestValues;

		public event EventHandler<ResponseSuccessEventArgs<T>> RequestSucceeded;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler RequestComplete;

		protected void OnRequestSuceeded(T responseItem)
		{
			if (RequestSucceeded != null)
			{
				RequestSucceeded(this, new ResponseSuccessEventArgs<T>() { ResponseItem = responseItem });
			}
		}

		//This gets called after failure or success
		protected void OnRequestComplete()
		{
			if (RequestComplete != null)
			{
				RequestComplete(this, null);
			}
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			if (RequestFailed != null)
			{
				RequestFailed(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });
			}
		}

		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}
		
		protected virtual void SendRequest(object sender, DoWorkEventArgs e)
		{
			RequestManager requestManager = new RequestManager();
			string jsonToSend = JsonConvert.SerializeObject(requestValues);

			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);

			if (requestManager.LastResponse != null)
			{
				try
				{
					e.Result = JsonConvert.DeserializeObject<T>(requestManager.LastResponse);
				}
				catch
				{
					e.Result = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
				}
			}

			T responseItem = e.Result as T;
			if (responseItem != null)
			{
				OnRequestSuceeded(responseItem);
			}
			else
			{
				OnRequestFailed(e.Result as JsonResponseDictionary);
			}

			OnRequestComplete();
		}

		public virtual void ProcessErrorResponse(JsonResponseDictionary responseValues)
		{
			string errorMessage = responseValues.get("ErrorMessage");
			if (errorMessage != null)
			{
				Console.WriteLine(string.Format("Request Failed: {0}", errorMessage));
			}
			else
			{
				Console.WriteLine(string.Format("Request Failed: Unknown Reason"));
			}
		}

		public virtual void Request()
		{
			BackgroundWorker doRequestWorker = new BackgroundWorker();
			doRequestWorker.DoWork += new DoWorkEventHandler(SendRequest);
			//doRequestWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ProcessResponse);
			doRequestWorker.RunWorkerAsync();
		}
	}


	public class WebRequestBase
	{
		protected string uri;
		protected Dictionary<string, string> requestValues;

		public event EventHandler RequestSucceeded;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler RequestComplete;

		protected void OnRequestSuceeded()
		{
			if (RequestSucceeded != null)
			{
				RequestSucceeded(this, null);
			}
		}

		//This gets called after failure or success
		protected void OnRequestComplete()
		{
			if (RequestComplete != null)
			{
				RequestComplete(this, null);
			}
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			if (RequestFailed != null)
			{
				RequestFailed(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });
			}
		}

		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}

		protected virtual string getJsonToSend()
		{
			return SerializeObject(requestValues);
		}

		protected string SerializeObject(object requestObject)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(requestObject);
		}

		protected virtual void SendRequest(object sender, DoWorkEventArgs e)
		{
			JsonResponseDictionary responseValues;

			RequestManager requestManager = new RequestManager();
			string jsonToSend = getJsonToSend();

			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);
			if (requestManager.LastResponse == null)
			{
				responseValues = new JsonResponseDictionary();
				responseValues["Status"] = "error";
				responseValues["ErrorMessage"] = "Unable to connect to server";
				responseValues["ErrorCode"] = "00";
			}
			else
			{
				try
				{
					responseValues = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
				}
				catch
				{
					responseValues = new JsonResponseDictionary();
					responseValues["Status"] = "error";
					responseValues["ErrorMessage"] = "Unexpected response";
					responseValues["ErrorCode"] = "01";
				}
			}

			e.Result = responseValues;
		}

		protected virtual void ProcessResponse(object sender, RunWorkerCompletedEventArgs e)
		{
			JsonResponseDictionary responseValues = e.Result as JsonResponseDictionary;
			if (responseValues != null)
			{
				string requestSuccessStatus = responseValues.get("Status");
				if (responseValues != null && requestSuccessStatus != null && requestSuccessStatus == "success")
				{
					ProcessSuccessResponse(responseValues);
					OnRequestSuceeded();
				}
				else
				{
					ProcessErrorResponse(responseValues);
					OnRequestFailed(responseValues);
				}

				OnRequestComplete();
			}
			else
			{
				// Don't do anything, there was no respones.
			}
		}

		public virtual void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			//Do Stuff
		}

		public virtual void ProcessErrorResponse(JsonResponseDictionary responseValues)
		{
			string errorMessage = responseValues.get("ErrorMessage");
			if (errorMessage != null)
			{
				Console.WriteLine(string.Format("Request Failed: {0}", errorMessage));
			}
			else
			{
				Console.WriteLine(string.Format("Request Failed: Unknown Reason"));
			}
		}

		public virtual void Request()
		{
			BackgroundWorker doRequestWorker = new BackgroundWorker();
			doRequestWorker.DoWork += new DoWorkEventHandler(SendRequest);
			doRequestWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ProcessResponse);
			doRequestWorker.RunWorkerAsync();
		}
	}

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
			uri = "https://mattercontrol.appspot.com/api/1/submit-feedback";
		}

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			JsonResponseDictionary response = responseValues;
		}

		public override void Request()
		{
			//If the client token exists, use it, otherwise wait for client token before making request
			if (ApplicationSettings.Instance.get("ClientToken") == null)
			{
				RequestClientToken request = new RequestClientToken();
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
			string clientToken = ApplicationSettings.Instance.get("ClientToken");
			requestValues["ClientToken"] = clientToken;
			if (clientToken != null)
			{
				base.Request();
			}
		}
	}

	public class RequestClientToken : WebRequestBase
	{
		public RequestClientToken()
		{
			requestValues["RequestToken"] = "ekshdsd5d5ssss5kels";
			requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
			uri = "https://mattercontrol.appspot.com/api/1/get-client-consumer-token";
		}

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			string clientToken = responseValues.get("ClientToken");
			if (clientToken != null)
			{
				ApplicationSettings.Instance.set("ClientToken", clientToken);
			}
		}
	}

	internal class RequestLatestVersion : WebRequestBase
	{
		public RequestLatestVersion()
		{
			string feedType = UserSettings.Instance.get("UpdateFeedType");
			if (feedType == null)
			{
				feedType = "release";
				UserSettings.Instance.set("UpdateFeedType", feedType);
			}
			requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
			requestValues["UpdateFeedType"] = feedType;
			uri = "https://mattercontrol.appspot.com/api/1/get-current-release-version";
		}

		public override void Request()
		{
			//If the client token exists, use it, otherwise wait for client token before making request
			if (ApplicationSettings.Instance.get("ClientToken") == null)
			{
				RequestClientToken request = new RequestClientToken();
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
			string clientToken = ApplicationSettings.Instance.get("ClientToken");
			requestValues["ClientToken"] = clientToken;
			if (clientToken != null)
			{
				base.Request();
			}
		}

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			List<string> responseKeys = new List<string> { "CurrentBuildToken", "CurrentBuildNumber", "CurrentBuildUrl", "CurrentReleaseVersion", "CurrentReleaseDate" };
			foreach (string key in responseKeys)
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