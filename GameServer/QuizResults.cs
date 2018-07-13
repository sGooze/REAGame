using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer
{
    public partial class QuizResult
    {
        [Key]
        [Column("ResultID")]
        public int ResultId { get; set; }
        [Column("QuizID")]
        public int QuizId { get; set; }
        [Column("UserID")]
        public int UserId { get; set; }
        public int Score { get; set; }

        [ForeignKey("QuizId")]
        [InverseProperty("QuizResults")]
        public Quiz Quiz { get; set; }
        [ForeignKey("UserId")]
        [InverseProperty("QuizResults")]
        public User User { get; set; }
    }
}
