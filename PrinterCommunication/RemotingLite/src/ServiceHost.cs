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
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection.Emit;
using System.IO.Compression;

namespace RemotingLite
{
    [Serializable]
    internal class MethodSyncInfo
    {
		public int MethodIdent { get; set; }
		public string MethodName { get; set; }
		public Type[] ParameterTypes { get; set; }
    }

    public class ServiceHost : IDisposable
    {
        private bool _isOpen = false;
        private TcpListener _listener;
        private List<TcpClient> _clients;
        private bool _continueListening;
        private object _continueListeningLock = new object();
        private object _singletonInstance;
        private IPEndPoint _endPoint;
        private bool _useThreadPool = false;
        private Dictionary<int, MethodInfo> _interfaceMethods;
		private Dictionary<int, bool[]> _methodParametersByRef;
		private ParameterTransferHelper _parameterTransferHelper = new ParameterTransferHelper();

        private bool Continue
        {
            get
            {
                lock (_continueListeningLock)
                {
                    return _continueListening;
                }
            }
            set
            {
                lock (_continueListeningLock)
                {
                    _continueListening = value;
                }
            }
        }

        /// <summary>
        /// Get or set whether the host should use regular or thread pool threads.
        /// </summary>
        public bool UseThreadPool
        {
            get { return _useThreadPool; }
            set
            {
                if (_isOpen)
                    throw new Exception("The host is already open");
                _useThreadPool = value;
            }
        }

        /// <summary>
        /// Constructs an instance of the host and starts listening for incoming connections.
        /// All listener threads are regular background threads.
        /// 
        /// NOTE: the instance created from the specified type is not automatically thread safe!
        /// </summary>
        /// <param name="remotedType">The remoted type. This must have a default constructor</param>
        /// <param name="port">The port number for incoming requests</param>
        public ServiceHost(Type remotedType, int port)
            : this(Activator.CreateInstance(remotedType), port)
        {
        }

        /// <summary>
        /// Constructs an instance of the host and starts listening for incoming connections.
        /// All listener threads are regular background threads.
        /// 
        /// NOTE: the instance is not automatically thread safe!
        /// </summary>
        /// <param name="singletonInstance">The singleton instance of the service</param>
        /// <param name="port">The port number for incoming requests</param>
        public ServiceHost(object singletonInstance, int port)
        {
            IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
			IPAddress address = null;
			foreach (var a in addressList)
				if (a.AddressFamily == AddressFamily.InterNetwork)
				{
					address = a;
					break;
				}
			_endPoint = new IPEndPoint(address, port);
            _listener = new TcpListener(_endPoint);
            _clients = new List<TcpClient>();
            _continueListening = true;
            _singletonInstance = singletonInstance;
			CreateMethodMap();
        }

		/// <summary>
		/// Loads all methods from interfaces and assigns an identifier
		/// to each. These are later synchronized with the client.
		/// </summary>
		private void CreateMethodMap()
		{
			var interfaces = _singletonInstance.GetType().GetInterfaces();
			_interfaceMethods = new Dictionary<int, MethodInfo>();
			_methodParametersByRef = new Dictionary<int, bool[]>();
			var currentMethodIdent = 0;
			foreach (var interfaceType in interfaces)
			{
				var methodInfos = interfaceType.GetMethods();
				foreach (var mi in methodInfos)
				{
					_interfaceMethods.Add(currentMethodIdent, mi);
					var parameterInfos = mi.GetParameters();
					var isByRef = new bool[parameterInfos.Length];
					for (int i = 0; i < isByRef.Length; i++)
						isByRef[i] = parameterInfos[i].ParameterType.IsByRef;
					_methodParametersByRef.Add(currentMethodIdent, isByRef);
					currentMethodIdent++;
				}
			}
		}

        /// <summary>
        /// Gets the end point this host is listening on
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return _endPoint; }
        }

        /// <summary>
        /// Opens the host and starts a listener thread. This listener thread spawns a new thread (or uses a
        /// thread pool thread) for each incoming connection.
        /// </summary>
        public void Open()
        {
            //start listening in the background
            if (_useThreadPool)
                ThreadPool.QueueUserWorkItem(ListenerThreadProc);
            else
            {
                Thread t = new Thread(ListenerThreadProc);
                t.IsBackground = true;
                t.Start();
            }
            _isOpen = true;
        }

        /// <summary>
        /// Closes the host and calls Dispose().
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Listens for incoming requests.
        /// 
        /// This method runs in a separate thread
        /// </summary>
        /// <param name="state"></param>
        private void ListenerThreadProc(object state)
        {
            _listener.Start();
            while (Continue)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    _clients.Add(client);

                    if (_useThreadPool)
                        ThreadPool.QueueUserWorkItem(ClientThreadProc, client);
                    else
                    {
                        Thread t = new Thread(ClientThreadProc);
                        t.IsBackground = true;
                        t.Start(client);
                    }
                }
                catch (SocketException) //this is normal since the thread is a background thread
                {
                    Continue = false;
                }
            }
        }

        /// <summary>
        /// This method handles all requests from a single client.
        /// 
        /// There is one thread running this method for each connected client.
        /// </summary>
        /// <param name="state"></param>
        private void ClientThreadProc(object state)
        {
            TcpClient client = (TcpClient)state;
            client.NoDelay = true;
            Stream stream = client.GetStream();
            BinaryReader binReader = new BinaryReader(stream);
            BinaryWriter binWriter = new BinaryWriter(stream);
            bool doContinue = true;
            do
            {
                try
                {
                    MemoryStream ms;
                    BinaryFormatter formatter = new BinaryFormatter();
                    //read message type
                    MessageType messageType = (MessageType)binReader.ReadInt32();
                    switch (messageType)
                    {
                        case MessageType.SyncInterface:
                            //Create a list of sync infos from the dictionary
							var syncInfos = new List<MethodSyncInfo>();
							foreach(var kvp in _interfaceMethods)
							{
								var parameters = kvp.Value.GetParameters();
								var parameterTypes = new Type[parameters.Length];
								for (var i = 0; i < parameters.Length; i++)
									parameterTypes[i] = parameters[i].ParameterType;
								syncInfos.Add(new MethodSyncInfo { MethodIdent = kvp.Key, MethodName = kvp.Value.Name, ParameterTypes = parameterTypes });
							}

                            //send the sync data back to the client
                            ms = new MemoryStream();
                            formatter.Serialize(ms, syncInfos);
							ms.Seek(0, SeekOrigin.Begin);
                            binWriter.Write((int)ms.Length);
                            binWriter.Write(ms.ToArray());
                            binWriter.Flush();
                            stream.Flush();
                            break;
                        case MessageType.MethodInvocation:
                            //read the method identifier
                            int methodHashCode = binReader.ReadInt32();
                            if (_interfaceMethods.ContainsKey(methodHashCode))
                            {
                                var method = _interfaceMethods[methodHashCode];
                                var isByRef = _methodParametersByRef[methodHashCode];

								//read parameter data
                                var parameters = _parameterTransferHelper.ReceiveParameters(binReader);

                                //invoke the method
                                object[] returnParameters;
                                var returnMessageType = MessageType.ReturnValues;
                                try
                                {
                                    object returnValue = method.Invoke(_singletonInstance, parameters);
                                    //the result to the client is the return value (null if void) and the input parameters
                                    returnParameters = new object[1 + parameters.Length];
                                    returnParameters[0] = returnValue;
                                    for (int i = 0; i < parameters.Length; i++)
                                        returnParameters[i + 1] = isByRef[i] ? parameters[i] : null;
                                }
                                catch (Exception ex)
                                {
                                    //an exception was caught. Rethrow it client side
                                    returnParameters = new object[] { ex };
                                    returnMessageType = MessageType.ThrowException;
                                }

                                //send the result back to the client
                                // (1) write the message type
                                binWriter.Write((int)returnMessageType);
                                // (2) write the return parameters
                                _parameterTransferHelper.SendParameters(binWriter, returnParameters);
                            }
                            else
                                binWriter.Write((int)MessageType.UnknownMethod);

                            //flush
                            binWriter.Flush();
                            stream.Flush();
                            break;
                        case MessageType.TerminateConnection:
                            doContinue = false;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception) //do not resume operation on this thread if any errors are unhandled.
                {
                    doContinue = false;
                }
            }
            while (doContinue);

            client.Close();
            binReader.Close();
            stream.Close();
            lock (_clients)
            {
                _clients.Remove(client);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _isOpen = false;
			Continue = false;
            _listener.Stop();
            foreach (TcpClient client in _clients)
                client.Close();
        }

        #endregion
    }
}
