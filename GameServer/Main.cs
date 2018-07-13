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
        private List<Session> activeSessions;
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
                    Session session = new Session(client);
                    //create a thread to handle communication
                    //with connected client
                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleSession));
                    clientThread.Name = String.Format("[{0}]", session.ID);
                    clientThread.Start(session);
                }
            catch (InvalidOperationException) {
                    active = false;
                }
        }

        private void MaintainSessionList(object callback)
        {
            lock (activeSessions)
            {
                var timedOut = activeSessions.Where(x => ((DateTime.Now - x.LastActivity).TotalSeconds >= Session.timeout));
                foreach(var ses in timedOut)
                {
                    ses.Close("Session timed out");
                }
                Console.WriteLine("Cleaned {0} inactive sessions, {1} were timed out", 
                    activeSessions.RemoveAll(x => (!x.Active)), timedOut.Count());
            }
        }

        private void HandleSession(object clientSession)
        {
            //TcpClient tcpClient = (TcpClient)client;
            Session session = (Session)clientSession;
            activeSessions.Add(session);
            Console.WriteLine("[{0}]: created, awaiting authentication...", session.ID);

            // session handles client data transmission!
            // send authentication request
            // close session on failure

            // Авторизация
            if (!session.Authenticate()) {
                Console.WriteLine("[{0}]: authentication failed, disconnected.", session.ID);
                session.Close("Authentication failed");
                activeSessions.Remove(session);
                return;
            }

            // После успешной авторизации проверим, не активна ли другая сессия для этого же пользователя
            foreach(var ses in activeSessions)
            {
                if ((ses.UserID == session.UserID) && (ses.Active))
                {
                    if (ses.ID == session.ID) continue;
                    Console.WriteLine("[{0}]: authentication failed, user {1} logged in on session {2}.", session.ID, session.UserID, ses.ID);
                    session.Close(String.Format("Пользователь уже авторизован ({0})", ses.ID));
                    activeSessions.Remove(session);
                    return;
                }
            }

            Console.WriteLine("[{0}]: authentication completed.", session.ID);
            session.SendMessage(Session.MsgType.Login, session.ID.ToString());
            while (session.Active)
            {
                session.Listen();
            }

            //session.Close();
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
            activeSessions = new List<Session>();
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
