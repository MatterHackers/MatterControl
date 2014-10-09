using System;
using System.Collections.Generic;
using System.Text;
using RemotingLite;

namespace RemotingLiteExampleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (ServiceHost host = new ServiceHost(typeof(ServiceImpl), 8000))
            {
                host.UseThreadPool = true;
                host.Open();

                Console.WriteLine("Host is running. Press <ENTER> to terminate.");
                Console.WriteLine(String.Format("Address is {0}", host.EndPoint.Address));
                Console.WriteLine("Press <ENTER> to terminate.");
                Console.ReadLine();
            }
        }
    }
}
