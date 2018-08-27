using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using ReaGame;

namespace GameServer
{
    public static class Constant
    {
        public static Dictionary<string, string> UserList = new Dictionary<string, string>()
        {
            {"a", "b" },
            { "test", "aaaa" },
            {"test2", "bbbb" },
        };
    }

    partial class GameServer
    {
        #region Handle console window closure
        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                Console.WriteLine("Console window closing, death imminent");
                stop = true;
            }
            return false;
        }
        static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        #endregion

        private static ReaServer server;
        private static bool stop = false;
        private static void CancelEventHandler(object sender, ConsoleCancelEventArgs e)
        {
            //stop = true;
            server.Stop();
        }
        static void Main(string[] args)
        {
            // https://stackoverflow.com/questions/4646827/on-exit-for-a-console-application
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);
            if ((args.Length > 1)&&(args[1] == "-db")) {
                if (!DbEditor(args)) return;
            }

            // TODO: Startup settings!
            server = new ReaServer();
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelEventHandler);
            Console.WriteLine("Server online");
            Console.WriteLine("Registered users: ");
            int i = 0;
            foreach (var reg_user in Constant.UserList)
            {
                Console.WriteLine("[{0}]:\t{1}\t{2}", i++, reg_user.Key, reg_user.Value);
            }


            /*while (true)
            {
                if (stop) { server.Stop(); break; };
                // TODO: Local server console (optional)
            }*/
            server.tcpThread.Join();
        }
    }

    class ReaServer
    {
        private static int port = 31337;
        private TcpListener tcpListener;
        public Thread tcpThread;
        Random random;
        bool active = false;

        public bool Status { get { return active; } }
        private List<IntermittentSession> activeSessions;
        private System.Threading.Timer sessionTimer;

        private void Listen()
        {
            tcpListener.Start();
            active = true;
            Console.WriteLine("Listening to tcp port #" + port.ToString());

            while (active)
                try {
                    //blocks until a client has connected to the server
                    if (!tcpListener.Pending())
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    TcpClient client = tcpListener.AcceptTcpClient();
                    //create a thread to handle communication
                    //with connected client
                    Task newClientTask = Task.Run(() => HandleSession(new RemoteClient(client)));
                }
            catch (InvalidOperationException) {
                    active = false;
                }
        }

        private void MaintainSessionList(object callback)
        {
            lock (activeSessions)
            {
                var timedOut = activeSessions.Where(x => ((DateTime.Now - x.LastActivity).TotalSeconds >= IntermittentSession.timeout));
                foreach(var ses in timedOut)
                {
                    ses.Close("Session timed out");
                }
                Console.WriteLine("Session cleanup: {0} inactive, {1} timed out", 
                    activeSessions.RemoveAll(x => (!x.Active)), timedOut.Count());
            }
        }

        static byte[] sessionNotFound = new byte[3] { (byte)ReaGame.RemoteSession.MsgType.No, 0, 0 };
        private void HandleSession(RemoteClient client)
        {
            IntermittentSession session = null;
            string body;

            Console.WriteLine($"({client.ID}): new client connected");
            RemoteSession.MsgType loginMsg = RemoteSession.MsgType.Null;
            try { loginMsg = (RemoteSession.MsgType)client.ReadMessage(out body); }
            catch (System.IO.IOException)
            {
                Console.WriteLine($"({client.ID}): login message hasn't arrived, disconnecting");
                client.Close();
                return;
            }
            
            // TODO: Проверка на уникальность сессии при вводе логина-пароля? (если сессии такого пользователя уже есть, то закрываем их)
            // TODO: Для новых пользователей отправляем уникальное сообщение об успешном входе, чтобы клиент мог отобразить справку (и т.п.)
            if (loginMsg == ReaGame.RemoteSession.MsgType.Login) {
                session = new IntermittentSession(client);
                if (!session.Authenticate(body))
                {
                    Console.WriteLine("[{0}]: authentication failed, disconnected.", session.ID);
                    try { session.SendMessage(RemoteSession.MsgType.No, "Invalid login or password."); } catch (System.IO.IOException) { }
                    session.Close("Аутентификация не пройдена");
                    return;
                }
                else {
                    session.SendMessage(RemoteSession.MsgType.Login, session.ID.ToString());
                    foreach (var ses in activeSessions)
                    {
                        if ((ses.UserID == session.UserID) && (ses.Active))
                        {
                            ses.Close($"Пользователь [{session.UserID}] подключился к другой сессии [{session.ID}]");
                        }
                    }
                    Console.WriteLine("[{0}]: authentication completed.", session.ID);
                    session.Paused = false;
                    activeSessions.Add(session);
                }
                // Check other sessions for this user id
            }
            else if (loginMsg == ReaGame.RemoteSession.MsgType.RestoreSession)
            {
                // TODO: Check for session state, base returned message on it
                session = activeSessions.FirstOrDefault(x => x.ID == new Guid(body) && x.Active);
                if ((session == null))
                {
                    Console.WriteLine($"({client.ID}): reconnection failed (using bad id [{body}]), disconnected.");
                    client.SendMessage((byte)RemoteSession.MsgType.No, "Session token invalid or obsolete.");
                    client.Dispose();
                    return;
                }
                else {
                    session.Reconnect(client);
                    Console.WriteLine($"({client.ID}) Reconnected to session [{session.ID}]");
                    session.SendMessage(RemoteSession.MsgType.RestoreSession);
                    session.Paused = false;
                }
            }
            while (!session.Paused)
            {
                session.GetMessages();
            }
        }

        /// <summary>
        /// Запуск нового экземпляра сервера
        /// </summary>
        /// <param name="slist_timeout">Таймаут автоматического закрытия неактивных сессий (в сек.)</param>
        public ReaServer(int slist_timeout = 5 * 60)
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpThread = new Thread(new ThreadStart(Listen));
            tcpThread.Name = "TCP Listen Thread";
            random = new Random();
            activeSessions = new List<IntermittentSession>();
            sessionTimer = new Timer(this.MaintainSessionList, null, 1000, slist_timeout * 1000);
            tcpThread.Start();
        }

        public void Stop()
        {
            Console.WriteLine("Server is stopping...");
            active = false;

            //sessionTimer.Dispose();
            sessionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            foreach (var session in activeSessions)
            {
                session.Close("Server shutting down");
            }
            tcpListener.Stop();
            
            Console.WriteLine("done.");
        }
    }
}
