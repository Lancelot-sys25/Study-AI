using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerScopedLearningData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subjects",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Subjects",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "StudySessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "RoadmapItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "QuizAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "LearningDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Flashcards",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AiMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_OwnerId_Name",
                table: "Subjects",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_OwnerId",
                table: "StudySessions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapItems_OwnerId",
                table: "RoadmapItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_OwnerId",
                table: "Quizzes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_OwnerId",
                table: "QuizAttempts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningDocuments_OwnerId",
                table: "LearningDocuments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_OwnerId",
                table: "Flashcards",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_UserId_ConversationId",
                table: "AiMessages",
                columns: new[] { "UserId", "ConversationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_OwnerId_Name",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_OwnerId",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_RoadmapItems_OwnerId",
                table: "RoadmapItems");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_OwnerId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_OwnerId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_LearningDocuments_OwnerId",
                table: "LearningDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_OwnerId",
                table: "Flashcards");

            migrationBuilder.DropIndex(
                name: "IX_AiMessages_UserId_ConversationId",
                table: "AiMessages");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "RoadmapItems");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "LearningDocuments");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AiMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subjects",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
