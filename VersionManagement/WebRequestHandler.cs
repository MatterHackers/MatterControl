using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

using System.Net;

namespace MatterHackers.MatterControl.VersionManagement
{

    public class WebRequestBase
    {
        protected string uri;
        protected JsonResponseDictionary responseValues;
        protected Dictionary<string, string> requestValues;
        public event EventHandler RequestSucceeded;
        public event EventHandler RequestFailed;

        void OnRequestSuceeded()
        {
            if (RequestSucceeded != null)
            {
                RequestSucceeded(this, null);
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

        protected void SendRequest()
        {
            RequestManager requestManager = new RequestManager();

            string jsonToSend = Newtonsoft.Json.JsonConvert.SerializeObject(requestValues);

            requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);
			if (requestManager.LastResponse == null)
			{
				responseValues = new JsonResponseDictionary();
				responseValues["Status"] = "error";
				responseValues["ErrorMessage"] = "Unable to connect to server";
			} else {
            	responseValues = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
			}
			ProcessResponse();
        }

        protected void ProcessResponse()
        {
            string requestSuccessStatus = this.responseValues.get("Status");
            if (responseValues != null && requestSuccessStatus != null && requestSuccessStatus == "success")
            {
                ProcessSuccessResponse();
                OnRequestSuceeded();
            }
            else
            {
                ProcessErrorResponse();
                OnRequestFailed();
            }
            
        }

        public virtual void ProcessSuccessResponse()
        {
            //Do Stuff            
        }

        public virtual void ProcessErrorResponse()
        {
            string errorMessage = this.responseValues.get("ErrorMessage");
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
            Thread saveThread = new Thread(SendRequest);
            saveThread.Name = "Check Version";
            saveThread.IsBackground = true;
            saveThread.Start();
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

        public override void ProcessSuccessResponse()
        {
            JsonResponseDictionary response = this.responseValues;
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
        public ContactFormRequest(string question,string details,string email, string firstName, string lastName)
        {
            requestValues["FirstName"] = firstName;
            requestValues["LastName"] = lastName;
            requestValues["Email"] = email;
            requestValues["FeedbackType"] = "Question";
            requestValues["Comment"] = string.Format("{0}\n{1}", question,details);
            uri = "https://mattercontrol.appspot.com/api/1/submit-feedback";
        }

        public override void ProcessSuccessResponse()
        {
            JsonResponseDictionary response = this.responseValues;
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

        public override void ProcessSuccessResponse()
        {
            string clientToken = this.responseValues.get("ClientToken");
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
            requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
            requestValues["FeedType"] = "pre-release";
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

        public override void ProcessSuccessResponse()
        {
            List<string> responseKeys = new List<string> { "CurrentBuildToken", "CurrentBuildNumber", "CurrentBuildUrl", "CurrentReleaseVersion", "CurrentReleaseDate" };
            foreach (string key in responseKeys)
            {
                saveResponse(key);
            }
        }

        private void saveResponse(string key)
        {
            string value = this.responseValues.get(key);
            if (value != null)
            {
                ApplicationSettings.Instance.set(key, value);
            }
        }
    }    
}
