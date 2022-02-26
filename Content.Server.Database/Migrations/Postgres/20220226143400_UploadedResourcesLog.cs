﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    public partial class UploadedResourcesLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_server_role_ban_server_role_unban__unban_id",
                table: "server_role_ban");

            migrationBuilder.DropForeignKey(
                name: "FK_server_role_unban_server_ban_ban_id",
                table: "server_role_unban");

            migrationBuilder.DropIndex(
                name: "IX_server_role_unban_ban_id",
                table: "server_role_unban");

            migrationBuilder.DropIndex(
                name: "IX_server_role_ban__unban_id",
                table: "server_role_ban");

            migrationBuilder.DropColumn(
                name: "unban_id",
                table: "server_role_ban");

            migrationBuilder.CreateTable(
                name: "uploaded_resource_log",
                columns: table => new
                {
                    uploaded_resource_log_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_resource_log", x => x.uploaded_resource_log_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_server_role_unban_ban_id",
                table: "server_role_unban",
                column: "ban_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_role_ban_address",
                table: "server_role_ban",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "IX_server_role_ban_user_id",
                table: "server_role_ban",
                column: "user_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_server_role_ban_HaveEitherAddressOrUserIdOrHWId",
                table: "server_role_ban",
                sql: "address IS NOT NULL OR user_id IS NOT NULL OR hwid IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_server_role_unban_server_role_ban_ban_id",
                table: "server_role_unban",
                column: "ban_id",
                principalTable: "server_role_ban",
                principalColumn: "server_role_ban_id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_server_role_unban_server_role_ban_ban_id",
                table: "server_role_unban");

            migrationBuilder.DropTable(
                name: "uploaded_resource_log");

            migrationBuilder.DropIndex(
                name: "IX_server_role_unban_ban_id",
                table: "server_role_unban");

            migrationBuilder.DropIndex(
                name: "IX_server_role_ban_address",
                table: "server_role_ban");

            migrationBuilder.DropIndex(
                name: "IX_server_role_ban_user_id",
                table: "server_role_ban");

            migrationBuilder.DropCheckConstraint(
                name: "CK_server_role_ban_HaveEitherAddressOrUserIdOrHWId",
                table: "server_role_ban");

            migrationBuilder.AddColumn<int>(
                name: "unban_id",
                table: "server_role_ban",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_role_unban_ban_id",
                table: "server_role_unban",
                column: "ban_id");

            migrationBuilder.CreateIndex(
                name: "IX_server_role_ban__unban_id",
                table: "server_role_ban",
                column: "unban_id");

            migrationBuilder.AddForeignKey(
                name: "FK_server_role_ban_server_role_unban__unban_id",
                table: "server_role_ban",
                column: "unban_id",
                principalTable: "server_role_unban",
                principalColumn: "role_unban_id");

            migrationBuilder.AddForeignKey(
                name: "FK_server_role_unban_server_ban_ban_id",
                table: "server_role_unban",
                column: "ban_id",
                principalTable: "server_ban",
                principalColumn: "server_ban_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
