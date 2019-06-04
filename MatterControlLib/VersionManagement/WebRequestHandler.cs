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

//#define TRACEREQUESTS

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.VersionManagement
{
	public class ResponseErrorEventArgs : EventArgs
	{
		public JsonResponseDictionary ResponseValues { get; set; }
	}

	public class WebRequestBase<ResponseType> where ResponseType : class
	{
		protected Dictionary<string, string> requestValues;
		protected string uri;
		private static object locker = new object();

		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}

		public int Timeout { get; set; } = 100000;

		public event EventHandler RequestComplete;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler<ResponseType> RequestSucceeded;

		public event EventHandler<ResponseType> ReloadRequest;

		public static void Request(string requestUrl, string[] requestStringPairs)
		{
			WebRequestBase<ResponseType> tempRequest = new WebRequestBase<ResponseType>();

			tempRequest.SetRquestValues(requestUrl, requestStringPairs);

			tempRequest.Request();
		}

		public void SetRquestValues(string requestUrl, string[] requestStringPairs)
		{
			this.uri = requestUrl;
			for (int i = 0; i < requestStringPairs.Length; i += 2)
			{
				this.requestValues[requestStringPairs[i]] = requestStringPairs[i + 1];
			}
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

		public async void Request(ulong longHash = 0)
		{
			await Task.Run(() =>
			{
				SendRequest(longHash);
			});
		}

		// This gets called after failure or success
		protected void OnRequestComplete()
		{
			RequestComplete?.Invoke(this, null);
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			RequestFailed?.Invoke(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });

			ApplicationController.WebRequestFailed?.Invoke();
		}

		protected void OnRequestSucceeded(ResponseType responseItem)
		{
			RequestSucceeded?.Invoke(this, responseItem);
			ApplicationController.WebRequestSucceeded?.Invoke();
		}

		protected void SendRequest(ulong longHash)
		{
			string cacheFileName = ApplicationController.CacheablePath("Text", longHash.ToString() + ".txt");

			ResponseType cacheResponse = null;

			if (longHash != 0
				&& ReloadRequest != null)
			{
				if (File.Exists(cacheFileName))
				{
					try
					{
						string cacheText = null;
						cacheText = File.ReadAllText(cacheFileName);
						cacheResponse = JsonConvert.DeserializeObject<ResponseType>(cacheText);

						OnRequestSucceeded(cacheResponse);
					}
					catch
					{
					}
				}
			}

			RequestManager requestManager = new RequestManager();

			// Prevent constant exceptions on debug builds when stepping through code. In debug, let requests stay in limbo until resumed and prevent the timeout exceptions
#if !DEBUG
			requestManager.Timeout = this.Timeout;
#endif
			string jsonToSend = JsonConvert.SerializeObject(requestValues);

#if TRACEREQUESTS
			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));
#endif

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);

			ResponseType responseItem = null;
			JsonResponseDictionary errorResults = null;

			if (requestManager.LastResponse != null)
			{
				try
				{
					responseItem = JsonConvert.DeserializeObject<ResponseType>(requestManager.LastResponse);
				}
				catch
				{
					errorResults = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
				}
			}

			if (responseItem != null)
			{
				if (ReloadRequest != null
					&& longHash != 0)
				{
					if (cacheResponse == null || !cacheResponse.Equals(responseItem))
					{
						lock (locker)
						{
							File.WriteAllText(cacheFileName, requestManager.LastResponse);
						}

						if (cacheResponse != null)
						{
							// we already sent back the succeeded response, send a cache miss
							ReloadRequest(this, responseItem);
						}
						else
						{
							// send back the succeeded response
							OnRequestSucceeded(responseItem);
						}
					}
				}
				else
				{
					OnRequestSucceeded(responseItem);
				}
			}
			else
			{
				OnRequestFailed(errorResults);
			}

			OnRequestComplete();
		}
	}

	/// <summary>
	/// Provides a WebReqeustBase implementation that allows the caller to specify the serialization object used by the WebRequestBase http post
	/// </summary>
	/// <typeparam name="TRequestType">The type which will be passed to the Request method, stored in a local instance and serialized for the http post</typeparam>
	public class WebRequest2<TRequestType> : WebRequestBase where TRequestType : class
	{
		private TRequestType localRequestValues;

		public void Request(string requestUrl, TRequestType requestValues)
		{
			this.uri = requestUrl;
			localRequestValues = requestValues;
			this.Request();
		}

		protected override string getJsonToSend()
		{
			return JsonConvert.SerializeObject(localRequestValues);
		}
	}


	public class WebRequestBase
	{
		protected Dictionary<string, string> requestValues;
		protected string uri;

		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}

		/// <summary>
		/// Gets or sets the time-out value in milliseconds
		/// </summary>
		/// <value>The timeout.</value>
		public int Timeout { get; set; } = 100000;

		public event EventHandler RequestComplete;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler RequestSucceeded;

		public static void Request(string requestUrl, string[] requestStringPairs)
		{
			WebRequestBase tempRequest = new WebRequestBase();

			tempRequest.uri = requestUrl;
			for (int i = 0; i < requestStringPairs.Length; i += 2)
			{
				tempRequest.requestValues[requestStringPairs[i]] = requestStringPairs[i + 1];
			}

			tempRequest.Request();
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

		public virtual void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			//Do Stuff
		}

		public virtual void Request()
		{
			BackgroundWorker doRequestWorker = new BackgroundWorker();
			doRequestWorker.DoWork += new DoWorkEventHandler(SendRequest);
			doRequestWorker.RunWorkerCompleted += ProcessResponse;
			doRequestWorker.RunWorkerAsync();
		}

		protected virtual string getJsonToSend()
		{
			return SerializeObject(requestValues);
		}

		//This gets called after failure or success
		protected void OnRequestComplete()
		{
			RequestComplete?.Invoke(this, null);
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			RequestFailed?.Invoke(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });
		}

		protected void OnRequestSuceeded()
		{
			RequestSucceeded?.Invoke(this, null);
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
				// Don't do anything, there was no response.
			}
		}

		protected virtual void SendRequest(object sender, DoWorkEventArgs e)
		{
			JsonResponseDictionary responseValues;

			RequestManager requestManager = new RequestManager() { Timeout = this.Timeout };

			string jsonToSend = getJsonToSend();

#if TRACEREQUESTS
			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));
#endif

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);
			if (requestManager.LastResponse == null)
			{
				responseValues = new JsonResponseDictionary();
				responseValues["Status"] = "error";
				responseValues["ErrorMessage"] = "Unable to connect to server";
				responseValues["ErrorCode"] = "00";

				ApplicationController.WebRequestFailed?.Invoke();
			}
			else
			{
				try
				{
					responseValues = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);

					string errorMessage;
					if (responseValues.TryGetValue("ErrorMessage", out errorMessage)
					    && errorMessage.IndexOf("expired session",  StringComparison.OrdinalIgnoreCase) != -1)
					{
						// Notify connection status changed and now invalid
						UiThread.RunOnIdle(() =>
						{
							ApplicationController.Instance.ChangeCloudSyncStatus(userAuthenticated: false, reason: "Session Expired".Localize());
						});
					}

					ApplicationController.WebRequestSucceeded?.Invoke();
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

		protected string SerializeObject(object requestObject)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(requestObject);
		}
	}
}