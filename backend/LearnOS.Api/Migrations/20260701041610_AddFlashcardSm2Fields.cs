using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashcardSm2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EaseFactor",
                table: "Flashcards",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ForgetCount",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntervalDays",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedAt",
                table: "Flashcards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repetition",
                table: "Flashcards",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EaseFactor",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "ForgetCount",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "IntervalDays",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "LastReviewedAt",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "Repetition",
                table: "Flashcards");
        }
    }
}
