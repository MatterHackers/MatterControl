using System;
using System.Collections.Generic;
using System.Text;
using CommonTypes;
using RemotingLite;
using System.Net.Sockets;
using System.Net;

namespace RemotingLiteExampleClient
{
    /// <summary>
    /// This is an example of how to use ProxyFactory directly.
    /// </summary>
    public class ExampleUsingProxyFactory
    {
        public void Start(int port)
        {
            // This is an example that connects to the local host. See MSDN documentation.
            // This will work if you run both client and host on the same machine.
#if true
            IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            IPAddress address = null;
            foreach (var a in addressList)
                if (a.AddressFamily == AddressFamily.InterNetwork)
                {
                    address = a;
                    break;
                }
            IPEndPoint endpoint = new IPEndPoint(address, port);
#else
            IPEndPoint endpoint = new IPEndPoint(Dns.GetHostEntry(Dns.GetHostName()).AddressList[0], 8000);
#endif

            // When using the proxy factory we do not need to write any client side code for the
            // proxy. This might be usefull if you:
            // 1) Want more control of when the connection is closed. This is done when the object is
            //    disposed.
            // 2) Are lazy :-)
            
            //create the proxy
            IService client = ProxyFactory.CreateProxy<IService>(endpoint);

            //make a few calls to the host
            Console.WriteLine(client.Sum(2, 3));
            Console.WriteLine(client.Sum(2, 3, 4, 5, 6, 7, 8, 9));
            Console.WriteLine(client.ToUpper("this string used to be lower case"));
            string str = "this was a lower case string";
            client.MakeStringUpperCase(ref str);
            Console.WriteLine(str);
            string lowerCaseString;
            client.MakeStringLowerCase("THIS WAS AN UPPER CASE STRING", out lowerCaseString);
            Console.WriteLine(lowerCaseString);
            Rectangle rect = new Rectangle(30, 40);
            Console.WriteLine(String.Format("Area before call : {0}", rect.Area));
            client.CalculateArea(ref rect);
            Console.WriteLine(String.Format("Area after call : {0}", rect.Area));

            long b;
            client.Square(123, out b);
            Console.WriteLine(string.Format("123 squared is {0}", b));

            // You can either dispose (and thus close the connection) the object yourself, or wait
            // for the garbage collector to do it for you when going out of scope of this method.
            // Here we dispose it explicitly,
            ((IDisposable)client).Dispose();
        }
    }
}
