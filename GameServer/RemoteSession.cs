using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

// Base class for REAGame client/server

namespace ReaGame
{
    /// <summary>
    /// Удалённый клиент. Реализует функции приёма и передачи данных между клиентом и сервером.
    /// Не обрабатывает возникающие исключения!
    /// </summary>
    public sealed class RemoteClient : IDisposable{
#if DEBUG
        /// <summary>
        /// Тайм-аут сетевых операций ввода-вывода
        /// </summary>
        private const int IOTimeout = 100000;
#else
        /// <summary>
        /// Тайм-аут сетевых операций ввода-вывода
        /// </summary>
        private const int IOTimeout = 10000;
#endif

        public TcpClient Client { get; private set; }
        public NetworkStream clientStream { get; set; }
        private static int id = 0;
        public int ID { get; private set; }

        public RemoteClient(TcpClient cli) { Client = cli; clientStream = cli.GetStream(); cli.ReceiveTimeout = cli.SendTimeout = IOTimeout; ID = id++; }
        public void Dispose() { Client.Dispose(); }
        public void Close() { Dispose(); }

        /// <summary>
        /// Чтение сообщения, посланного клиентом
        /// </summary>
        /// <param name="body">Тело сообщения</param>
        /// <returns>Тип сообщения</returns>
        /// <exception cref="System.IO.IOException"></exception>
        public byte ReadMessage(out string body)
        {
            byte sv;
            sv = (byte)clientStream.ReadByte();
            int length = (clientStream.ReadByte() << 8) + clientStream.ReadByte();
            if (length > 0)
            {
                byte[] strb = new byte[length];
                clientStream.Read(strb, 0, length);
                body = Encoding.UTF8.GetString(strb);
            }
            else body = "";
            return sv;
        }

#region SendMessage
        /// <summary>
        /// Отправка сообщения клиенту
        /// </summary>
        /// <param name="msg_type">Тип сообщения</param>
        /// <param name="msg">Содержимое сообщения, длиной не больше 2^16-2 байт</param>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool SendMessage(byte msg_type, byte[] msg)
        {
            
            if (msg.Length > 65536 - 2) return false; // TODO: Throw
            clientStream.WriteByte((byte)msg_type);
            clientStream.WriteByte((byte)(msg.Length >> 8));
            clientStream.WriteByte((byte)(msg.Length));
            clientStream.Write(msg, 0, msg.Length);
            
            return true;
        }

        /// <summary>
        /// Отправка форматированного сообщения клиенту
        /// </summary>
        /// <param name="msg_type">Тип сообщения</param>
        /// <param name="message">Строка сообщения, длиной не больше 2^16-2 байт</param>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public bool SendMessage(byte msg_type, string message)
        {
            byte[] response;
            UTF8Encoding utf_encoder = new UTF8Encoding();
            response = utf_encoder.GetBytes(message);
            return SendMessage(msg_type, response);
        }
#endregion
    }

    public class InvalidMessageTypeException : Exception
    {
        public RemoteSession.MsgType RecievedMessageType;
        public InvalidMessageTypeException(RemoteSession.MsgType msgType) : base($"Полученное сообщение имеет неверный тип ({msgType})") { RecievedMessageType = msgType; }

    }

    public abstract class RemoteSession : IDisposable
    {
        // TODO: Separate into REQUEST and RESPONSE types
        public enum MsgType : byte {
            Null, Yes, No, Login, Logout, Test, Str, Error,
            GetQuizList, GetQuizInfo, 
            Json,
            PauseSession, RestoreSession
        };
        protected Dictionary<MsgType, Action<string>> MsgReaders;

        protected RemoteClient client;
        public TcpClient Client { get { return client.Client; } }
        protected NetworkStream clientStream { get { return client.clientStream; } }
        public DateTime LastActivity { get; private set; }

        public Guid ID { get; private set; }
        public bool Active { get; protected set; }
        public bool Paused { get; set; }

        /*public RemoteSession(IPEndPoint connection)
        {
            client = new TcpClient();
            try { client.Connect(connection); }
            catch (SocketException)
            {
                return;
            }
            clientStream = client.GetStream();
            Active = true;
        }
        */

        protected RemoteSession()
        {
            ID = Guid.NewGuid();
            LastActivity = DateTime.Now;
            Active = true;
        }

        protected RemoteSession(RemoteClient cli) : this()
        {
            client = cli;
        }

        protected void Reconnect(RemoteClient cli)
        {
            client.Close();
            client = null;
            client = cli;
        }

        /// <summary>
        /// Считывает переданное клиентом сообщение
        /// </summary>
        /// <param name="body">Полученное сообщение</param>
        /// <param name="acceptedTypes">(опционально) Список разрешённых типов сообщений. Если тип полученного сообщения не будет входить в этот список, будет брошено исключение <c></c>InvalidMessageTypeException</c></param>
        /// <returns>Тип полученного сообщения</returns>
        /// <exception cref="InvalidMessageTypeException"></exception>
        public MsgType ReadMessage(out string body, MsgType[] acceptedTypes = null)
        {
            MsgType sv;
            try
            {
                sv = (MsgType)client.ReadMessage(out body);
                LastActivity = DateTime.Now;
                if ((acceptedTypes != null))
                {
                    foreach (MsgType type in acceptedTypes)
                        if (sv == type)
                            return sv;
                    throw new InvalidMessageTypeException(sv);
                }
            }
            catch (System.IO.IOException)
            {
                // when socket error occurs
                body = "";
                return MsgType.Error;
            }
            return sv;
        }

        public void Dispose()
        {
            if (client != null) { client.Dispose(); client = null; }
        }

        /// <summary>
        /// Отключить клиента (при необходимости) и пометить сессию как неактивную (помеченную на удаление).
        /// </summary>
        /// <param name="logout_msg">не используется</param>
        public virtual void Close(string logout_msg = "")
        {
            Console.WriteLine("[{0}]: session closed, reason: {1}", ID, logout_msg);
            if (!Active) return;
            Dispose();
            Active = false;
            Paused = true;
        }

        /// <summary>
        /// Отключить клиента, но не помечать сессию как неактивную. Вызывается в конце передачи пакета сообщений.
        /// </summary>
        public void Pause()
        {
            Paused = true;
            client.Close();
            client = null;
            Console.WriteLine($"[{ID}]: session paused");
        }

        ~RemoteSession()
        {
            Dispose();
        }

        // TODO: Cleanup??
#region SendMessage
        /// <summary>
        /// Отправка простого (без тела) сообщения клиенту
        /// </summary>
        /// <param name="msg_type">Тип сообщения</param>
        public bool SendMessage(MsgType msg_type)
        {
            return SendMessage(msg_type, new byte[0]);
        }
        /// <summary>
        /// Отправка сообщения клиенту
        /// </summary>
        /// <param name="msg_type">Тип сообщения</param>
        /// <param name="msg">Содержимое сообщения, длиной не больше 2^16-2 байт</param>
        protected bool SendMessage(MsgType msg_type, byte[] msg)
        {
            try
            {
                client.SendMessage((byte)msg_type, msg);
                LastActivity = DateTime.Now;
            } catch (System.IO.IOException)
            {
                //Close("Ошибка IO");
                Console.WriteLine($"[{ID}]: IO exception occured on SendMessage, session on pause");
                Pause();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Отправка форматированного сообщения клиенту
        /// </summary>
        /// <param name="msg_type">Тип сообщения</param>
        /// <param name="message">Строка сообщения, длиной не больше 2^16-2 байт</param>
        public bool SendMessage(MsgType msg_type, string message)
        {
            byte[] response;
            UTF8Encoding utf_encoder = new UTF8Encoding();
            response = utf_encoder.GetBytes(message);
            return SendMessage(msg_type, response);
        }
#endregion
    }
}