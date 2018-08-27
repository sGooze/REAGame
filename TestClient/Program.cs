using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ReaGame;

namespace TestClient
{
    public class UrgentMessageException : Exception
    {
        public RemoteSession.MsgType msgType { get; private set; }
        public string body { get; private set; }
        public UrgentMessageException(RemoteSession.MsgType msg, string msg_body) : base()
        {
            msgType = msg;
            body = msg_body;
        }
    }





    public class RemoteServer : RemoteSession
    {
        public Guid ServerID { get; private set; }
        public IPEndPoint ServerIp { get; set; }
        public RemoteServer(TcpClient cli) : base(new RemoteClient(cli))
        {
            Active = true;
        }

        public void ResumeConnection()
        {
            var cli = new TcpClient();
            // TODO: try-catch: соединение не установлено, повторите позже
            cli.Connect(ServerIp);
            Reconnect(new RemoteClient(cli));

            SendMessage(MsgType.RestoreSession, ServerID.ToString());
            string g;
            ReadResponse(out g);
        }

        public void PauseConnection()
        {
            if (!Active) return;
            SendMessage(MsgType.PauseSession);
            if (ReadMessage(out string g) != MsgType.PauseSession) throw new UrgentMessageException(MsgType.Error, "something fukked up");
            Client.Close();
        }

        public bool Authorize(string login, string password)
        {
            List<byte> msg = new List<byte>() /*{ (byte)login.Length }*/;
            msg.AddRange(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes((char)login.Length + login + password))));
            SendMessage(MsgType.Login, msg.ToArray());

            string sessionId;
            var status = ReadMessage(out sessionId);
            if (status == MsgType.Login)
            {
                ServerID = new Guid(sessionId);
                // После успешного логина отправляем остальные сообщения, предназначенные для сбора нужной для первого запуска информации
                SendMessage(MsgType.GetQuizList);
                string res;
                ReadResponse(out res);
                Console.WriteLine(res);
                SendMessage(MsgType.PauseSession);
                if (ReadResponse(out res) != MsgType.PauseSession)
                    Console.WriteLine("PauseSession behaved strangely");
            }

            Client.Close();
            return (status == MsgType.Login);
        }

        public List<MsgType> exceptionalMsgs = new List<MsgType>() { MsgType.Null, MsgType.Logout, MsgType.Error };

        /// <summary>
        /// Считывает сообщение от сервера; прерывает текущий обработчик при получении сообщения критического характера
        /// </summary>
        /// <param name="body">Тело полученного сообщения</param>
        /// <returns>Тип полученного сообщения</returns>
        public MsgType ReadResponse(out string body)
        {
            string o;
            MsgType res = ReadMessage(out o);
            if (exceptionalMsgs.Contains(res)) throw new UrgentMessageException(res, o);
            body = o;
            return res;
        }
    }






    class Program
    {
        private static bool stop = false;
        private static RemoteServer server;
        private static void CancelEventHandler(object sender, ConsoleCancelEventArgs e)
        {
            stop = true;
            server.Close();
        }

        
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelEventHandler);

            // Read IP address from the console
            start:
            IPEndPoint serverEndPoint;
            while (true)
            {
                try
                {
                    Console.WriteLine("Server IP address (leave empty for localhost):");
                    string input = Console.ReadLine();
                    IPAddress addr = IPAddress.Parse(input.Length != 0 ? input : "127.0.0.1");
                    serverEndPoint = new IPEndPoint(addr, 31337);
                    break;
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid input!");
                }
            }

            Console.WriteLine("Connecting...");

            var tcpclient = new TcpClient();
            try { tcpclient.Connect(serverEndPoint); }
            catch (SocketException)
            {
                Console.WriteLine("Unable to connect to server.");
                goto start;
            }
            server = new RemoteServer(tcpclient) { ServerIp = serverEndPoint };
            Console.WriteLine("Done.");

            // Attempt to login
            string login, password;
            Console.Write("Login: "); login = Console.ReadLine();
            Console.Write("Password: "); password = Console.ReadLine();
            if (!server.Authorize(login, password))
            {
                Console.WriteLine("Authorization failed!");
                server.Close();
                return;
            }
            Console.WriteLine("Authorization completed!");

            // Main loop
            while (server.Active)
            {
                Console.Write(":> ");
                string cl_msg = Console.ReadLine();
                RemoteSession.MsgType type;

                // 😟😟😟
                if (cl_msg == "who") type = RemoteSession.MsgType.Test;
                else if (cl_msg == "quit") type = RemoteSession.MsgType.Logout;
                else if (cl_msg == "get-list") type = RemoteSession.MsgType.GetQuizList;
                else if (cl_msg == "get-quiz") type = RemoteSession.MsgType.GetQuizInfo;
                else if (cl_msg == "crash")  type = RemoteSession.MsgType.Error;  
                else { Console.WriteLine("Unknown command"); continue; }

                DateTime cmdstart = DateTime.Now;
                server.ResumeConnection();
                if (!server.SendMessage(type))
                {
                    Console.WriteLine("An error has occured, unable to send a message to server.");
                    return;
                }
                System.Threading.Thread.Sleep(11000);
                if (type == RemoteSession.MsgType.Error) Environment.FailFast(":^)");

                string in_msg = "";
                try
                {
                    
                    type = server.ReadResponse(out in_msg);
                    switch (type)
                    {
                        case RemoteSession.MsgType.Json:
                        case RemoteSession.MsgType.Str:
                            Console.WriteLine("Server says: " + in_msg);
                            break;
                        case RemoteSession.MsgType.Test:
                            DateTime cmdsrv = DateTime.FromBinary(long.Parse(in_msg));
                            Console.WriteLine($"0 -- {(cmdsrv - cmdstart).Milliseconds} -- {(DateTime.Now - cmdstart).Milliseconds}");
                            break;
                        case RemoteSession.MsgType.Null: break;
                        case 
                            RemoteSession.MsgType.No: Console.WriteLine(in_msg);
                            break;
                        default:
                            Console.WriteLine($"Unknown message recieved: ({type})({in_msg})");
                            break;
                    }
                    server.PauseConnection();
                } catch (UrgentMessageException e)
                {
                    switch (e.msgType)
                    {
                        case RemoteSession.MsgType.Error:
                            Console.WriteLine("Socket error.");
                            server.Close();
                            break;
                        case RemoteSession.MsgType.Logout:
                            Console.WriteLine("Logged out by server: {0}", in_msg);
                            server.Close(); break;
                        case RemoteSession.MsgType.Null: break;
                    }
                }
                
            }
            
            Console.Write("\nSession ended.\n");
            goto start;
        }
    }
}
