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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.VersionManagement
{
    public class WebRequestBase
    {
        protected string uri;
        protected Dictionary<string, string> requestValues;
        public event EventHandler RequestSucceeded;
        public event EventHandler RequestFailed;
        public event EventHandler RequestComplete;

        void OnRequestSuceeded()
        {
            if (RequestSucceeded != null)
            {
                RequestSucceeded(this, null);
            }
        }

        //This gets called after failure or success
        void OnRequestComplete()            
        {
            if (RequestComplete != null)
            {
                RequestComplete(this, null);
            }
        }

        void OnRequestFailed()
        {
            if (RequestFailed != null)
            {
                RequestFailed(this, null);
            }
        }

        public WebRequestBase()
        {
            requestValues = new Dictionary<string, string>();
        }

        protected void SendRequest(object sender, DoWorkEventArgs e)
        {
            JsonResponseDictionary responseValues;

            RequestManager requestManager = new RequestManager();

            string jsonToSend = Newtonsoft.Json.JsonConvert.SerializeObject(requestValues);

            requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);
            if (requestManager.LastResponse == null)
            {
                responseValues = new JsonResponseDictionary();
                responseValues["Status"] = "error";
                responseValues["ErrorMessage"] = "Unable to connect to server";
            }
            else
            {
                responseValues = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
            }

            e.Result = responseValues;
        }

        protected void ProcessResponse(object sender, RunWorkerCompletedEventArgs e)
        {
            JsonResponseDictionary responseValues = e.Result as JsonResponseDictionary;

            string requestSuccessStatus = responseValues.get("Status");
            if (responseValues != null && requestSuccessStatus != null && requestSuccessStatus == "success")
            {
                ProcessSuccessResponse(responseValues);
                OnRequestSuceeded();
            }
            else
            {
                ProcessErrorResponse(responseValues);
                OnRequestFailed();
            }

            OnRequestComplete();
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
    public class NotificationRequest : WebRequestBase
    {
        public NotificationRequest(string printName)
        {

            if (UserSettings.Instance.get("AfterPrintFinishedSendEmail") == "true")
            {
                string emailAddress = UserSettings.Instance.get("NotificationEmailAddress");
                requestValues["EmailAddress"] = emailAddress;
            }
            if (UserSettings.Instance.get("AfterPrintFinishedSendTextMessage") == "true")
            {
                string phoneNumber = UserSettings.Instance.get("NotificationPhoneNumber");
                requestValues["PhoneNumber"] = phoneNumber;
            }
            requestValues["PrintItemName"] = printName;
            uri = "https://mattercontrol.appspot.com/api/1/send-print-notification";
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

    //To do - move this
    class ContactFormRequest : WebRequestBase
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

    class RequestLatestVersion : WebRequestBase
    {
        public RequestLatestVersion()
        {
            string feedType = ApplicationSettings.Instance.get("UpdateFeedType");
            if (feedType == null)
            {
                feedType = "release";
                ApplicationSettings.Instance.set("UpdateFeedType", feedType);
            }
            requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
            requestValues["FeedType"] = feedType;
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
