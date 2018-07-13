using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameServer
{
    public partial class ReaGameContext : DbContext
    {
        public ReaGameContext()
        {
        }

        public ReaGameContext(DbContextOptions<ReaGameContext> options)
            : base(options)
        {
        }

        public virtual DbSet<QuestionAnswer> QuestionAnswers { get; set; }
        public virtual DbSet<QuestionCategory> QuestionCategories { get; set; }
        public virtual DbSet<Question> Questions { get; set; }
        public virtual DbSet<QuizResult> QuizResults { get; set; }
        public virtual DbSet<Quiz> Quizzes { get; set; }
        public virtual DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Server=rea-shed-00,64218;Database=ReaGame;Trusted_Connection=true;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuestionAnswer>(entity =>
            {
                entity.HasOne(d => d.Question)
                    .WithMany(p => p.QuestionAnswers)
                    .HasForeignKey(d => d.QuestionId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_QuestionAnswers_Questions");
            });

            modelBuilder.Entity<QuestionCategory>(entity =>
            {
                entity.HasOne(d => d.Quiz)
                    .WithMany(p => p.QuestionCategories)
                    .HasForeignKey(d => d.QuizId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_QuestionCategories_Quizzes");
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Questions)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Questions_QuestionCategories");
            });

            modelBuilder.Entity<QuizResult>(entity =>
            {
                entity.HasOne(d => d.Quiz)
                    .WithMany(p => p.QuizResults)
                    .HasForeignKey(d => d.QuizId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_QuizResults_Quizzes");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.QuizResults)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_QuizResults_Users");
            });
        }
    }
}
