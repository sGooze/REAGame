using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer
{
    public partial class User
    {
        public User()
        {
            QuizResults = new HashSet<QuizResult>();
        }

        [Key]
        [Column("UserID")]
        public int UserId { get; set; }
        [Required]
        [StringLength(128)]
        public string UserName { get; set; }
        [StringLength(512)]
        public string UserInfo { get; set; }

        [InverseProperty("User")]
        public ICollection<QuizResult> QuizResults { get; set; }
    }
}
