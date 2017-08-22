/*
Copyright (c) 2014, Lars Brubaker
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
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class RequestManager
	{
		public string LastResponse { protected set; get; }

		/// <summary>
		/// Gets or sets the time-out value in milliseconds 
		/// </summary>
		/// <value>The timeout.</value>
		public int Timeout { get; internal set; } = 100000;

		private CookieContainer cookies = new CookieContainer();

		internal string GetCookieValue(Uri SiteUri, string name)
		{
			Cookie cookie = cookies.GetCookies(SiteUri)[name];
			return (cookie == null) ? null : cookie.Value;
		}

		public string GetResponseContent(HttpWebResponse response)
		{
			if (response == null)
			{
				throw new ArgumentNullException("response");
			}

			string responseFromServer = null;

			try
			{
				// Get the stream containing content returned by the server.
				using (Stream dataStream = response.GetResponseStream())
				{
					// Open the stream using a StreamReader for easy access.
					using (StreamReader reader = new StreamReader(dataStream))
					{
						// Read the content.
						responseFromServer = reader.ReadToEnd();
						// Cleanup the streams and the response.
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			finally
			{
				response.Close();
			}
			LastResponse = responseFromServer;
			return responseFromServer;
		}

		public HttpWebResponse SendPOSTRequest(string uri, string content, string signIn, string password, bool allowAutoRedirect)
		{
			HttpWebRequest request = GenerateRequest(uri, content, "POST", null, null, allowAutoRedirect);
			return GetResponse(request);
		}

		public HttpWebResponse SendGETRequest(string uri, string signIn, string password, bool allowAutoRedirect)
		{
			HttpWebRequest request = GenerateRequest (uri, null, "GET", null, null, allowAutoRedirect);
			return GetResponse(request);
		}

		internal HttpWebRequest GenerateRequest(string uri, string content, string method, string signIn, string password, bool allowAutoRedirect)
		{
			if (uri == null)
			{
				throw new ArgumentNullException("uri");
			}
			// Create a request using a URL that can receive a post.
			HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
			request.Method = method;

			request.Timeout = this.Timeout;

			// Set cookie container to maintain cookies
			request.CookieContainer = cookies;
			request.AllowAutoRedirect = false;
			// If sign in is empty use defaul credentials
			if (string.IsNullOrEmpty(signIn))
			{
				request.Credentials = CredentialCache.DefaultNetworkCredentials;
			}
			else
			{
				request.Credentials = new NetworkCredential(signIn, password);
			}

			if (method == "POST")
			{
				// Convert POST data to a byte array.
				byte[] byteArray = Encoding.UTF8.GetBytes(content);
				// Set the ContentType property of the WebRequest.
				request.ContentType = "application/json";
				// Set the ContentLength property of the WebRequest.
				request.ContentLength = byteArray.Length;
				// Get the request stream.
				Stream dataStream = null;
				try
				{
					dataStream = request.GetRequestStream();
					// Write the data to the request stream.
					dataStream.Write(byteArray, 0, byteArray.Length);
					// Close the Stream object.
					dataStream.Close();
				}
				catch (WebException ex)
				{
					if(ex.Status == WebExceptionStatus.Timeout)
					{
						LastResponse = JsonConvert.SerializeObject(new { status = "error", statuscode = 408 });
					}
					Console.WriteLine("Web exception occurred. Status code: {0}", ex.Status);
				}
				catch (IOException ioException)
				{
					Console.WriteLine("Web exception occurred. Message: {0}", ioException.Message);
				}
				catch (Exception e)
				{
                    System.Diagnostics.Trace.WriteLine(e.Message);
				}
			}
			return request;
		}

		internal HttpWebResponse GetResponse(HttpWebRequest request)
		{
			if (request == null)
			{
				return null;
			}
			HttpWebResponse response = null;
			try
			{
				response = (HttpWebResponse)request.GetResponse();
				cookies.Add(response.Cookies);

				GetResponseContent(response);
			}
			catch (WebException ex)
			{
				Console.WriteLine("Web exception occurred. Status code: {0}", ex.Status);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return response;
		}
	}
}