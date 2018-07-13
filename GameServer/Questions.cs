using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer
{
    public partial class Question
    {
        public Question()
        {
            QuestionAnswers = new HashSet<QuestionAnswer>();
        }

        [Key]
        [Column("QuestionID")]
        public int QuestionId { get; set; }
        [Column("CategoryID")]
        public int CategoryId { get; set; }
        public byte Type { get; set; }
        [StringLength(512)]
        public string Text { get; set; }
        public int Score { get; set; }

        [ForeignKey("CategoryId")]
        [InverseProperty("Questions")]
        public QuestionCategory Category { get; set; }
        [InverseProperty("Question")]
        public ICollection<QuestionAnswer> QuestionAnswers { get; set; }
    }
}
