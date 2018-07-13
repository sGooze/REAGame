using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer
{
    public partial class QuestionAnswer
    {
        [Key]
        [Column("AnswerID")]
        public int AnswerId { get; set; }
        [Column("QuestionID")]
        public int QuestionId { get; set; }
        [Required]
        [StringLength(128)]
        public string Text { get; set; }
        public bool IsCorrect { get; set; }

        [ForeignKey("QuestionId")]
        [InverseProperty("QuestionAnswers")]
        public Question Question { get; set; }
    }
}
