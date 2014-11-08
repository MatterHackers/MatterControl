using System;
using System.Collections.Generic;
using System.Text;
using CommonTypes;
using RemotingLite;
using System.Net;

namespace RemotingLiteExampleClient
{
    /// <summary>
    /// This class is an example of how to subclass ClientBase in order to provide more
    /// control over the calls.
    /// Notice that any class inheriting from ClientBase has a Proxy-property, which
    /// is a proxy of the interface specified. This proxy is generated with RemotingLite.ProxyFactory
    /// and provides the contact to the host.
    /// 
    /// Note the constructor!
    /// </summary>
    public class ClientProxyImpl : ClientBase<IService>, IService
    {
        /// <summary>
        /// The class inheriting from ClientBase must define a constructor which takes
        /// an end point to the host. You have to call the base constructor.
        /// </summary>
        /// <param name="endpoint"></param>
        public ClientProxyImpl(IPEndPoint endpoint)
            : base(endpoint)
        {
        }

        #region IService Members

        public int Sum(int a, int b)
        {
            return Proxy.Sum(a, b);
        }

        public int Sum(params int[] values)
        {
            return Proxy.Sum(values);
        }

        public string ToUpper(string str)
        {
            return Proxy.ToUpper(str);
        }

        public void MakeStringUpperCase(ref string str)
        {
            Proxy.MakeStringUpperCase(ref str);
        }

        public void MakeStringLowerCase(string str, out string lowerCaseString)
        {
            Proxy.MakeStringLowerCase(str, out lowerCaseString);
        }

        public void CalculateArea(ref Rectangle rectangle)
        {
            Proxy.CalculateArea(ref rectangle);
        }

        public void Square(long a, out long b)
        {
            Proxy.Square(a, out b);
        }

        #endregion
    }
}
