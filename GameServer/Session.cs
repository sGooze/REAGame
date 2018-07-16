using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
/*
namespace GameServer
{
    /// <summary>
    /// Класс клиентской сессии, отвечает за пересылку информации между клиентом и сервером.
    /// </summary>
    public class Session : ReaGame.RemoteSession
    {
        /// <summary>
        /// Тайм-аут сессии (в секундах)
        /// </summary>
        public const int timeout = 5 * 60; 
        
        static int sessions_total = 0;
        public DateTime LastActivity { get; private set; }
        public int UserID { get; private set; }

        public Session(TcpClient client)
        {
            ID = ++sessions_total;
            Client = client;
            clientStream = Client.GetStream();
            clientStream.ReadTimeout = timeout * 1000;

            MsgReaders = new Dictionary<MsgType, Action<string>>()
            {
                { MsgType.Error, MsgError },
                { MsgType.Test, MsgTest },
                { MsgType.Logout, MsgLogout },
                { MsgType.GetQuizList, MsgGetQuizList },
            };
            
            LastActivity = DateTime.Now;
            Active = true;
        }

        #region Message handlers

        private void MsgError(string body)
        {
            Console.WriteLine("Socket error.");
            Close();
        }

        private void MsgLogout(string body)
        {
            SendMessage(MsgType.Logout, "Goodbye");
            Close("Requested by user");
        }

        private Random random = new Random();
        private void MsgTest(string body)
        {
            using (var db = new ReaGameContext())
            {
                SendMessage(MsgType.Str, String.Format("Hello, your session id is {0}. Here's a random number: {1}", ID, random.Next(2048)));
            }
            
        }

        private void MsgGetQuizList(string body)
        {
            // TODO: append hi-score
            using (var db = new ReaGameContext())
            {
                var obj = new JObject(
                    new JProperty("quiz_list",
                    new JArray(
                        from quiz in db.Quizzes
                        select JObject.FromObject(quiz)
                        )
                    )
                );
                SendMessage(MsgType.Json, obj.ToString());
            }
        }

        private void MsgGetQuizInfo(string body)
        {
            clientStream.ReadByte();
            clientStream.ReadByte();
            using (var db = new ReaGameContext())
            {
                var obj = JObject.FromObject(db.Quizzes.Select(x => x.QuizId == int.Parse(body)));
                obj.Add(new JProperty("categories", new JArray(
                    from cat in db.QuestionCategories
                    select new JObject(
                        new JProperty("name", cat.Name),
                        // TODO: только список ценовых категорий, в которых есть вопросы - конкретные вопросы будут выбираться случайным образом во время игры!
                        new JProperty("questions", new JArray(
                            from quest in cat.Questions
                            orderby quest.Score
                            select new JObject(
                                    new JProperty("id", quest.QuestionId),
                                    new JProperty("text", quest.Text),
                                    new JProperty("score", quest.Score)
                                )
                            )
                        )
                    )
                )));
                SendMessage(MsgType.Json, obj.ToString());
            }
        }

        #endregion

        /// <summary>
        /// Первичная аутентификация пользователя
        /// </summary>
        /// <returns>Статус аутентификации</returns>
        public bool Authenticate()
        {
            // Формат сообщения логина: 
            //[1 byte - Длина сообщения][1 byte - длина логина (в расшифрованной строке)][Логин + пароль (без разделителей, Base64)...]
            // Логин + пароль - строка в UTF-8, кодированная в Base64
            clientStream.ReadTimeout = 25000;
            try
            {
                // TODO: Новый формат сообщения логина!
                //      [Msg.Login][..][bLoginChars][base64(login+pass)]
                //  Также добавить обработку Msg.RestoreConnection
                // todo: убрать сообщения для мисши
                int msg_length = clientStream.ReadByte();
                 Console.WriteLine("[{0}]: Auth message, recieved length = {1} bytes.", this.ID, msg_length);
                int login_length = clientStream.ReadByte();
                 Console.WriteLine("Login length = {0} symbols.", login_length);
                if (msg_length <= 0) return false;
                byte[] login_bytes = new byte[msg_length];
                clientStream.Read(login_bytes, 0, msg_length);
                 Console.Write("{0} bytes recieved: ", msg_length);
                 foreach (var b in login_bytes) Console.Write("[{0}]", b);
                 Console.WriteLine();

                clientStream.ReadTimeout = System.Threading.Timeout.Infinite;
                string msg = Encoding.UTF8.GetString(login_bytes);
                msg = Encoding.UTF8.GetString(Convert.FromBase64String(msg));
                string login = msg.Substring(0, login_length), password = msg.Substring(login_length);
                 Console.WriteLine("Login = {0}, password = {1}", login, password);

                // TODO: Replace with 1C authorization
                if (!(Constant.UserList.ContainsKey(login) && Constant.UserList[login] == password)) return false;

                using (var db = new ReaGameContext())
                {
                    var user = db.Users.FirstOrDefault(x => x.UserName == login);
                    
                    if (user == null)
                    {
                        // TODO: Update db with new user info
                        Console.WriteLine("[{0}]: new user {1}", ID, login);
                        UserID = random.Next(65538);
                    }
                    else // TODO: <- TEMP!
                    UserID = user.UserId;
                }

                return true;
            }
            catch (System.IO.IOException)
            {
                Console.WriteLine("An error has occured during stream reading (possible timeout)");
                return false;
            }
        }
        /// <summary>
        /// Закрытие соединения с клиентом и завершение сессии
        /// </summary>
        public override void Close(string logout_msg = "")
        {
            // TODO: Farewell message
            if (!Active) return;
            //SendMessage(MsgType.Logout, logout_msg);
            //clientStream.Close();
            Client.Close();
            Active = false;
            Console.WriteLine("[{0}]: disconnected - {1}.", ID, logout_msg);
        }

        public void Listen()
        {
            string msgBody;
            MsgType msgType = ReadMessage(out msgBody);
            LastActivity = DateTime.Now;
            if (MsgReaders.ContainsKey(msgType)) MsgReaders[msgType](msgBody);
            else
            {
                Console.WriteLine("[{0}]: Unknown message, aborting the connection to prevent desync", ID);
                Close();
            }
        }
    }
}
*/