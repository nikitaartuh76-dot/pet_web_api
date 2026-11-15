using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ASP_FINAL_PROJECT.Migrations
{
    public partial class LessonOwnership : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Lessons");
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HostId = table.Column<string>(type: "text", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("lesson_pk", x => x.Id);
                    table.ForeignKey("FK_Lessons_AspNetUsers_CreatorId", x => x.CreatorId, "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Lessons_AspNetUsers_HostId", x => x.HostId, "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_Lessons_CreatorId", "Lessons", "CreatorId");
            migrationBuilder.CreateIndex("IX_Lessons_HostId", "Lessons", "HostId");
            migrationBuilder.CreateIndex("IX_Lessons_Status_StartDate", "Lessons", new[] { "Status", "StartDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("This migration recreates Lessons and cannot restore the deleted lesson data.");
        }
    }
}
