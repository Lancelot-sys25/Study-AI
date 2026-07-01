using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class SetFlashcardEaseFactorDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "EaseFactor",
                table: "Flashcards",
                type: "float",
                nullable: false,
                defaultValue: 2.5,
                oldClrType: typeof(double),
                oldType: "float");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "EaseFactor",
                table: "Flashcards",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 2.5);
        }
    }
}
