using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

// Base class for REAGame client/server

namespace ReaGame
{
    public sealed class RemoteClient : IDisposable{
        /// <summary>
        /// Тайм-аут сетевых операций ввода-вывода
        /// </summary>
        private const int IOTimeout = 10000;
        public TcpClient Client { get; private set; }
        public NetworkStream clientStream { get; set; }

        public RemoteClient(TcpClient cli) { Client = cli; clientStream = cli.GetStream(); cli.ReceiveTimeout = cli.SendTimeout = IOTimeout; }
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

        public MsgType ReadMessage(out string body)
        {
            MsgType sv;
            try
            {
                sv = (MsgType)client.ReadMessage(out body);
                LastActivity = DateTime.Now;
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
            if (!Active) return;
            Dispose();
            Active = false;
        }

        /// <summary>
        /// Отключить клиента, но не помечать сессию как неактивную. Вызывается в конце передачи пакета сообщений.
        /// </summary>
        public void Pause()
        {
            Paused = true;
            client.Close();
            client = null;
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
                Close();
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