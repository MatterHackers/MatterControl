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

namespace RemotingLite
{
    public abstract class ClientBase<TInterface> : IDisposable where TInterface : class
    {
        private TInterface _proxy;

        public TInterface Proxy { get { return _proxy; } }

        public ClientBase(IPEndPoint endpoint)
        {
            _proxy = ProxyFactory.CreateProxy<TInterface>(endpoint);
        }

        #region IDisposable Members

        public void Dispose()
        {
            (_proxy as Channel).Dispose();
        }

        #endregion
    }
}
