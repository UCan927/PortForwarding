﻿using Mina.Core.Service;
using Mina.Filter.Codec;
using Mina.Transport.Socket;
using System;
using System.Configuration;
using System.Net;
using UCanSoft.PortForwarding.Common.Codec.Direct;
using UCanSoft.PortForwarding.Common.Utility.Helper;
using UCanSoft.PortForwarding.Tcp2Udp.Core;

namespace UCanSoft.PortForwarding.Tcp2Udp
{
    class Program
    {
        static void Main(string[] args)
        {
            String nlogConfigFile = ".\\Config\\NLog.config";
            try
            {
                NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(nlogConfigFile);
            }
            catch { }

            var ip = IPAddress.Parse(ConfigurationManager.AppSettings["ListenHost"]);
            var port = Int32.Parse(ConfigurationManager.AppSettings["ListenPort"]);
            var host = new IPEndPoint(ip, port);

            IoAcceptor acceptor = new AsyncSocketAcceptor();
            acceptor.FilterChain.AddFirst("codec", new ProtocolCodecFilter(new DirectCodecFactory()));
            acceptor.Handler = SingleInstanceHelper<AcceptorHandler>.Instance;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("按S键启动监听;");
            Console.WriteLine("按T键停止监听;");
            Console.WriteLine("按ESC键退出;");
            Console.ForegroundColor = ConsoleColor.Gray;
            acceptor.Bind(host);
            Console.WriteLine("已开启对地址[{0}]的监听", host);

            ConsoleKey key = ConsoleKey.NoName;
            do
            {
                try
                {
                    key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        acceptor.Unbind();
                        break;
                    }
                    else if (key == ConsoleKey.S)
                    {
                        acceptor.Bind(host);
                        Console.WriteLine("已开启对地址[{0}]的监听", host);
                    }
                    else if (key == ConsoleKey.T)
                    {
                        acceptor.Unbind();
                        Console.WriteLine("已停止对地址[{0}]的监听", host);
                    }
                    else
                        continue;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("发生异常: {0}", ex.Message);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            } while (true);
        }
    }
}
