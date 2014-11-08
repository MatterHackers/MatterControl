using System;
using System.Collections.Generic;
using System.Text;

namespace RemotingLiteExampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Using our own implementation that inherits from ClientBase<>\n");
            ExampleUsingClientProxyImpl example1 = new ExampleUsingClientProxyImpl();
            example1.Start(8000);

            Console.WriteLine("\nUsing ProxyFactory to create a proxy directly.\n");
            ExampleUsingProxyFactory example2 = new ExampleUsingProxyFactory();
            example2.Start(8000);

            Console.ReadLine();
        }
    }
}
