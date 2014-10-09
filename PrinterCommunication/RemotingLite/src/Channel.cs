/*************************************************************************************************
 * RemotingLite
 * ------
 * A light framework for making remote method invocations using TCP/IP. It is based loosely on
 * Windows Communication Foundation, and is meant to provide programmers with the same API
 * regardless of whether they write software for the Microsoft .NET platform or the Mono .NET
 * platform.
 * Consult the documentation and example applications for information about how to use this API.
 * 
 * Author       : Frank Thomsen
 * http         : http://sector0.dk
 * Concact      : http://sector0.dk/?q=contact
 * Information  : http://sector0.dk/?q=node/27
 * Licence      : Free. If you use this, please let me know.
 * 
 *          Please feel free to contact me with ideas, bugs or improvements.
 *************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace RemotingLite
{
	public class Channel : IDisposable
	{
		private TcpClient _client;
		private NetworkStream _stream;
		private BinaryReader _binReader;
		private BinaryWriter _binWriter;
		private BinaryFormatter _formatter;
		private ParameterTransferHelper _parameterTransferHelper = new ParameterTransferHelper();
		private List<MethodSyncInfo> _syncInfos;

		/// <summary>
		/// Creates a connection to the concrete object handling method calls on the server side
		/// </summary>
		/// <param name="endpoint"></param>
		public Channel(IPEndPoint endpoint)
		{
			_client = new TcpClient(AddressFamily.InterNetwork);
			_client.Connect(endpoint);
			_client.NoDelay = true;
			_stream = _client.GetStream();
			_binReader = new BinaryReader(_stream);
			_binWriter = new BinaryWriter(_stream);
			_formatter = new BinaryFormatter();
			SyncInterface();
		}

		/// <summary>
		/// This method asks the server for a list of identifiers paired with method
		/// names and -parameter types. This is used when invoking methods server side.
		/// </summary>
		private void SyncInterface()
		{
			//write the message type
			_binWriter.Write((int)MessageType.SyncInterface);

			//read sync data
			var ms = new MemoryStream(_binReader.ReadBytes(_binReader.ReadInt32()));
			_syncInfos = (List<MethodSyncInfo>)_formatter.Deserialize(ms);
		}

		/// <summary>
		/// Closes the connection to the server
		/// </summary>
		public void Close()
		{
			Dispose();
		}

		/// <summary>
		/// Invokes the method with the specified parameters.
		/// </summary>
		/// <param name="methodName">The name of the method</param>
		/// <param name="parameters">Parameters for the method call</param>
		/// <returns>An array of objects containing the return value (index 0) and the parameters used to call
		/// the method, including any marked as "ref" or "out"</returns>
		protected object[] InvokeMethod(params object[] parameters)
		{
			//write the message type
			_binWriter.Write((int)MessageType.MethodInvocation);

			//find the mathing server side method ident
			var callingMethod = (new StackFrame(1)).GetMethod();
			var methodName = callingMethod.Name;
			var methodParams = callingMethod.GetParameters();
			var ident = -1;
			foreach (var si in _syncInfos)
			{
				//first of all the method names must match
				if (si.MethodName == methodName)
				{
					//second of all the parameter types and -count must match
					if (methodParams.Length == si.ParameterTypes.Length)
					{
						var matchingParameterTypes = true;
						for (int i = 0; i < methodParams.Length; i++)
							if (!methodParams[i].ParameterType.FullName.Equals(si.ParameterTypes[i].FullName))
							{
								matchingParameterTypes = false;
								break;
							}
						if (matchingParameterTypes)
						{
							ident = si.MethodIdent;
							break;
						}
					}
				}
			}

			if (ident < 0)
				throw new Exception(string.Format("Cannot match method '{0}' to its server side equivalent", callingMethod.Name));

			//write the method ident to the server
			_binWriter.Write(ident);

			//send the parameters
			_parameterTransferHelper.SendParameters(_binWriter, parameters);

			_binWriter.Flush();
			_stream.Flush();

			// Read the result of the invocation.
			MessageType messageType = (MessageType)_binReader.ReadInt32();
			if (messageType == MessageType.UnknownMethod)
				throw new Exception("Unknown method.");

			object[] outParams = _parameterTransferHelper.ReceiveParameters(_binReader);

			if (messageType == MessageType.ThrowException)
				throw (Exception)outParams[0];

			return outParams;
		}

		#region IDisposable Members

		public void Dispose()
		{
			_binWriter.Write((int)MessageType.TerminateConnection);
			_binWriter.Flush();
			_binWriter.Close();
			_binReader.Close();
			_stream.Flush();
			_stream.Close();
			_client.Close();
		}

		#endregion
	}
}
