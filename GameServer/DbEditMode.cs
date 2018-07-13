using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

// мне очень стыдно за это

namespace GameServer
{
    partial class GameServer
    {
        static void QuizInfo(Quiz quiz, ReaGameContext db)
        {
            if (quiz == null) return;
            Console.WriteLine("\n[{0}] {1}\nTime: {2}\n[{3}|{4}|{5}]", quiz.QuizId, quiz.Name, quiz.TimeToSolve, quiz.ScoreGreat, quiz.ScoreGood, quiz.ScoreMediocre);
            foreach(var cat in quiz.QuestionCategories)
            {
                Console.WriteLine("\t{0}: {1}", cat.CategoryId, cat.Name);
                foreach (var quest in db.Questions.Where(x => x.CategoryId == cat.CategoryId))
                {
                    Console.WriteLine("\t\t{0}: [{1}][{2} pts.] {3}", quest.QuestionId, quest.Type, quest.Score, quest.Text);
                    foreach (var answ in quest.QuestionAnswers)
                    {
                        Console.WriteLine("\t\t\t{0} {1}", answ.IsCorrect ? "+" : "-", answ.AnswerId, answ.Text);
                    }
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Редактор содержимого базы данных
        /// </summary>
        /// <param name="args">Аргументы программы</param>
        /// <returns>True для продолжения работы сервера в обычном режиме; False для закрытия</returns>
        static bool DbEditor(string[] args)
        {
            Console.WriteLine("Server started in Database Editor mode");
            using (ReaGameContext db = new ReaGameContext())
            {
                Quiz quiz = null;
                QuestionCategory cat = null;
                Question que = null;
                QuestionAnswer ans = null;
                foreach (var q in db.Quizzes)
                    Console.WriteLine(" [{0}]: {1}", q.QuizId, q.Name);
                while (true)
                {
                    if (quiz != null) Console.Write("[{0}]", quiz.Name);
                    Console.Write(":> ");
                    var cargs = Console.ReadLine().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cargs.Length == 0) continue;
                    try
                    {
                        switch (cargs[0])
                        {
                            case "quit": return false;
                            case "start":return true;
                            case "list":
                                if (cargs.Length < 2)
                                    foreach (var q in db.Quizzes)
                                        Console.WriteLine(" [{0}]: {1}", q.QuizId, q.Name);
                                else
                                    foreach (var q in db.Quizzes.Where(x => x.Name.Contains(cargs[1])))
                                        Console.WriteLine(" [{0}]: {1}", q.QuizId, q.Name);
                                break;
                            case "info":
                                if (cargs.Length > 1){
                                    var a = db.Quizzes.FirstOrDefault(x => x.QuizId == int.Parse(cargs[1]));
                                    if (a != null) QuizInfo(a, db);
                                }
                                else if (quiz != null)
                                    QuizInfo(quiz, db);
                                break;
                            case "new":
                                if ((cargs.Length < 2) || (quiz != null)) continue;
                                quiz = new Quiz() { Name = cargs[1] };
                                db.Quizzes.Add(quiz);
                                //db.Entry(quiz).State = Microsoft.EntityFrameworkCore.EntityState.Added;
                                break;
                            case "edit":
                                if ((cargs.Length < 2)) continue;
                                quiz = db.Quizzes.FirstOrDefault(x => x.Name == cargs[1]);
                                if (quiz == null) Console.WriteLine("Invalid quiz: {0}", cargs[1]);
                                break;
                            case "time":
                                if ((cargs.Length < 2) || (quiz == null)) continue;
                                quiz.TimeToSolve = int.Parse(cargs[1]);
                                break;
                            case "score":
                                if ((cargs.Length < 4) || (quiz == null)) continue;
                                quiz.ScoreGreat = int.Parse(cargs[1]);
                                quiz.ScoreGood = int.Parse(cargs[2]);
                                quiz.ScoreMediocre = int.Parse(cargs[3]);
                                break;
                            case "save":
                                db.SaveChanges();
                                break;

                            // Редактирование категорий
                            case "catadd":
                                if ((cargs.Length < 2) || (quiz == null)) continue;
                                if (db.QuestionCategories.Where(x => x.QuizId == quiz.QuizId && x.Name == cargs[1]).Count() > 0)
                                { Console.WriteLine("Unoriginal category name!"); break; }
                                cat = new QuestionCategory() { Name = cargs[1], QuizId = quiz.QuizId };
                                db.QuestionCategories.Add(cat);
                                break;
                            case "catedit":
                                if ((cargs.Length < 2) || (quiz == null)) continue;
                                cat = db.QuestionCategories.FirstOrDefault(x => x.Name == cargs[1]);
                                if (cat == null) Console.WriteLine("Invalid category: {0}", cargs[1]);
                                break;

                                // Добавление 
                        }
                    }
                    catch (FormatException) { Console.WriteLine("Invalid input!"); }
                }
            }
        }
    }
}
