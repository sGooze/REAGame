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
        public RemoteServer(IPEndPoint connection)
        {
            Client = new TcpClient();
            try { Client.Connect(connection); }
            catch (SocketException)
            {
                return;
            }
            clientStream = Client.GetStream();
            Active = true;
        }

        public bool Authorize(string login, string password)
        {
            int login_length = login.Length;
            byte[] msg = Encoding.UTF8.GetBytes("  " + Convert.ToBase64String(Encoding.UTF8.GetBytes(login + password)));
            msg[0] = (byte)(msg.Length - 2);
            msg[1] = (byte)login_length;
            clientStream.Write(msg, 0, msg.Length);

            string sessionId; int sid;
            var status = ReadMessage(out sessionId);
            int.TryParse(sessionId, out sid);
            ID = sid;
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
            
            // Attempt to connect
            server = new RemoteServer(serverEndPoint);
            if (!server.Active)
            {
                Console.WriteLine("Unable to connect to server.");
                return;
            }
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
                else { Console.WriteLine("Unknown command"); continue; }

                if (!server.SendMessage(type))
                {
                    Console.WriteLine("An error has occured, unable to send a message to server.");
                    return;
                }

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
                        case RemoteSession.MsgType.Null: break;
                        default:
                            Console.WriteLine("Unknown message recieved");
                            break;
                    }
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
