using Mina.Core.Service;
using Mina.Filter.Codec;
using Mina.Transport.Socket;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using UCanSoft.PortForwarding.Common.Codec.Direct;
using UCanSoft.PortForwarding.Common.Utility.Helper;
using UCanSoft.PortForwarding.TestTool.Core;

namespace UCanSoft.PortForwarding.TestTool
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

            String msg = ConfigurationManager.AppSettings["Message"];
            String msgFile = ConfigurationManager.AppSettings["MessageFile"];
            var ip = IPAddress.Parse(ConfigurationManager.AppSettings["ListenHost"]);
            var port = Int32.Parse(ConfigurationManager.AppSettings["ListenPort"]);
            var acceptorHost = new IPEndPoint(ip, port);

            ip = IPAddress.Parse(ConfigurationManager.AppSettings["ProxyHost"]);
            port = Int32.Parse(ConfigurationManager.AppSettings["ProxyPort"]);
            var connectorHost = new IPEndPoint(ip, port);

            IoAcceptor acceptor = new AsyncSocketAcceptor();
            acceptor.FilterChain.AddFirst("codec", new ProtocolCodecFilter(new DirectCodecFactory()));
            acceptor.Handler = SingleInstanceHelper<AcceptorHandler>.Instance;

            AsyncSocketConnector connector = new AsyncSocketConnector();
            connector.FilterChain.AddFirst("codec", new ProtocolCodecFilter(new DirectCodecFactory()));
            connector.SessionConfig.WriteTimeout = 60;
            connector.ConnectTimeout = 5;
            connector.SessionConfig.ReadBufferSize = 2048;
            connector.Handler = SingleInstanceHelper<ConnectorHandler>.Instance;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("按S键启动监听;");
            Console.WriteLine("按T键停止监听;");
            Console.WriteLine("按C键建立连接;");
            Console.WriteLine("按D键断开连接;");
            Console.WriteLine("按ESC键退出;");
            Console.ForegroundColor = ConsoleColor.Gray;
            acceptor.Bind(acceptorHost);
            Console.WriteLine("已开启对地址[{0}]的监听", acceptorHost);

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
                        acceptor.Bind(acceptorHost);
                        Console.WriteLine("已开启对地址[{0}]的监听", acceptorHost);
                    }
                    else if (key == ConsoleKey.T)
                    {
                        acceptor.Unbind();
                        Console.WriteLine("已停止对地址[{0}]的监听", acceptorHost);
                    }
                    else if(key == ConsoleKey.C)
                    {   
                        connector.Connect(connectorHost);
                    }
                    else if (key == ConsoleKey.D)
                    {
                        foreach (var item in connector.ManagedSessions)
                            item.Value.CloseNow();
                    }
                    else if (key == ConsoleKey.M)
                    {
                        if (!String.IsNullOrWhiteSpace(msg))
                        {
                            var buffer = Encoding.UTF8.GetBytes(msg);
                            var bytes = new ArraySegment<Byte>(buffer);
                            connector.Broadcast(bytes);
                        }
                        if (File.Exists(msgFile))
                        {
                            Byte[] buffer = new Byte[2048];
                            using (var stream = File.Open(msgFile, FileMode.Open))
                            {
                                do
                                {
                                    var readLen = stream.Read(buffer, 0, buffer.Length);
                                    if (readLen == 0)
                                        break;
                                    var bytes = new ArraySegment<Byte>(buffer, 0, readLen);
                                    connector.Broadcast(bytes);
                                    System.Threading.Thread.Sleep(50);
                                }
                                while (true);
                            }
                        }
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
