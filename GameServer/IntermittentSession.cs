using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameServer
{
    class IntermittentSession : ReaGame.RemoteSession
    {
        /*
            соединение устанавливатся только перед передачей данных
            соединение инициализируется из ListenThread сообщением Login или Resume
            также способна устаревать 
            и не забыть начать реализацию игрового процесса!!!
         */

        /// <summary>
        /// Тайм-аут сессии (в секундах)
        /// </summary>
        public const int timeout = 5 * 60;

        static int sessions_total = 0;
        
        public int UserID { get; private set; }

        public IntermittentSession(ReaGame.RemoteClient rclient) : base(rclient)
        {
            MsgReaders = new Dictionary<MsgType, Action<string>>()
            {
                { MsgType.Error, MsgError },
                { MsgType.Test, MsgTest },
                { MsgType.Logout, MsgLogout },
                { MsgType.GetQuizList, MsgGetQuizList },
            };
        }

        /// <summary>
        /// Первичная аутентификация пользователя
        /// </summary>
        /// <returns>Статус аутентификации</returns>
        public bool Authenticate(string login_message)
        {
            int login_length = login_message[0]; string msg = login_message.Substring(1);
            // Формат сообщения логина: 
            // Первый символ интерпретируется (не парсится!!!) как число - длина логина в символах
            // Логин + пароль - строка в UTF-8, кодированная в Base64
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
                }
                else // TODO: <- TEMP!
                    UserID = user.UserId;
            }
            return true;
        }

        public void GetMessages()
        {
            string msgBody;
            MsgType msgType = ReadMessage(out msgBody);
            if (MsgReaders.ContainsKey(msgType)) MsgReaders[msgType](msgBody);
            else
            {
                Console.WriteLine("[{0}]: Unknown message, aborting the connection to prevent desync", ID);
                Close();
            }
        }


        #region Message handlers
        /* HANDLERS FOR CLIENT MESSAGES */
        private void MsgPause(string body)
        {
            // Последнее сообщение, передаваемое в конце пакета сообщений
            // Ставит приём сообщений сервером на паузу и отключает текущего клиента
            SendMessage(MsgType.PauseSession);
        }
        private void MsgError(string body)
        {
            Console.WriteLine("Socket error.");
            Pause();
        }

        private void MsgLogout(string body)
        {
            SendMessage(MsgType.Logout, "Goodbye");
            Close("Requested by user");
        }
        
        private void MsgTest(string body)
        {
            using (var db = new ReaGameContext())
            {
                SendMessage(MsgType.Str, String.Format("Hello, your session id is {0}.", ID));
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
    }
}
