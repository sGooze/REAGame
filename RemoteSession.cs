using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

// Base class for REAGame client/server

namespace ReaGame
{
    public abstract class RemoteSession
    {
        // TODO: Separate into REQUEST and RESPONSE types
        public enum MsgType : byte {
            Null, Yes, No, Login, Logout, Test, Str, Error,
            GetQuizList, GetQuizInfo, 
            Json,
            PauseSession, RestoreSession
        };
        protected Dictionary<MsgType, Action<string>> MsgReaders;
        public TcpClient Client { get; protected set; }
        protected NetworkStream clientStream;
        public int ID { get; protected set; }
        public bool Active { get; protected set; }

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

        public MsgType ReadMessage(out string body)
        {
            MsgType sv;
            try
            {
                sv = (MsgType)clientStream.ReadByte();
            }
            catch (System.IO.IOException)
            {
                // when socket error occurs
                body = "";
                /*clientStream.ReadByte();
                clientStream.ReadByte();*/
                return MsgType.Error;
            }
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

        public virtual void Close(string logout_msg = "")
        {
            if (!Active) return;
            clientStream.Close();
            Client.Close();
            Active = false;
        }

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
                if (msg.Length > 65536 - 2) return false; // TODO: Throw
                clientStream.WriteByte((byte)msg_type);
                clientStream.WriteByte((byte)(msg.Length >> 8));
                clientStream.WriteByte((byte)(msg.Length));
                clientStream.Write(msg, 0, msg.Length);
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
            /*Int16 msg_bytelength = (Int16)(response.Length - 3);
            response[0] = (byte)msg_type;
            response[1] = (byte)(msg_bytelength >> 8);
            response[2] = (byte)(msg_bytelength);
            clientStream.Write(response, 0, response.Length);*/
            return SendMessage(msg_type, response);
        }
#endregion

    }
}