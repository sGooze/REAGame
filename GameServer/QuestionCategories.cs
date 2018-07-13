using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer
{
    public partial class QuestionCategory
    {
        public QuestionCategory()
        {
            Questions = new HashSet<Question>();
        }

        [Key]
        [Column("CategoryID")]
        public int CategoryId { get; set; }
        [Column("QuizID")]
        public int QuizId { get; set; }
        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        [ForeignKey("QuizId")]
        [InverseProperty("QuestionCategories")]
        public Quiz Quiz { get; set; }
        [InverseProperty("Category")]
        public ICollection<Question> Questions { get; set; }
    }
}
