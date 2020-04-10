using Microsoft.EntityFrameworkCore.Migrations;

namespace sap_profile_ms.Migrations
{
    public partial class Initial3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                nullable: true);

           
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AspNetUsers",
                nullable: true);

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");

            
            migrationBuilder.DropColumn(
                name: "Name",
                table: "AspNetUsers");

           
        }
    }
}
