using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace GameServer
{
    [JsonObject(MemberSerialization.OptOut)]
    public partial class Quiz
    {
        public Quiz()
        {
            QuestionCategories = new HashSet<QuestionCategory>();
            QuizResults = new HashSet<QuizResult>();
        }

        [Key]
        [Column("QuizID")]
        public int QuizId { get; set; }
        [Required]
        [StringLength(128)]
        public string Name { get; set; }
        public int TimeToSolve { get; set; }
        public int ScoreGreat { get; set; }
        public int ScoreGood { get; set; }
        public int ScoreMediocre { get; set; }

        [JsonIgnore]
        [InverseProperty("Quiz")]
        public ICollection<QuestionCategory> QuestionCategories { get; set; }
        [JsonIgnore]
        [InverseProperty("Quiz")]
        public ICollection<QuizResult> QuizResults { get; set; }
    }
}
