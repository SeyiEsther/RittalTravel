using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RittalTravel.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("AspNetRoles", t => new { Id=t.Column<string>("nvarchar(450)",nullable:false), Name=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), NormalizedName=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), ConcurrencyStamp=t.Column<string>("nvarchar(max)",nullable:true) }, c => c.PrimaryKey("PK_AspNetRoles",x=>x.Id));
            migrationBuilder.CreateTable("AspNetUsers", t => new { Id=t.Column<string>("nvarchar(450)",nullable:false), UserName=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), NormalizedUserName=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), Email=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), NormalizedEmail=t.Column<string>("nvarchar(256)",maxLength:256,nullable:true), EmailConfirmed=t.Column<bool>("bit",nullable:false), PasswordHash=t.Column<string>("nvarchar(max)",nullable:true), SecurityStamp=t.Column<string>("nvarchar(max)",nullable:true), ConcurrencyStamp=t.Column<string>("nvarchar(max)",nullable:true), PhoneNumber=t.Column<string>("nvarchar(max)",nullable:true), PhoneNumberConfirmed=t.Column<bool>("bit",nullable:false), TwoFactorEnabled=t.Column<bool>("bit",nullable:false), LockoutEnd=t.Column<DateTimeOffset?>("datetimeoffset",nullable:true), LockoutEnabled=t.Column<bool>("bit",nullable:false), AccessFailedCount=t.Column<int>("int",nullable:false) }, c => c.PrimaryKey("PK_AspNetUsers",x=>x.Id));
            migrationBuilder.CreateTable("Organisations", t => new { Id=t.Column<int>("int",nullable:false).Annotation("SqlServer:Identity","1, 1"), Name=t.Column<string>("nvarchar(max)",nullable:false), ContactEmail=t.Column<string>("nvarchar(max)",nullable:true) }, c => c.PrimaryKey("PK_Organisations",x=>x.Id));
            migrationBuilder.CreateTable("Trips", t => new { Id=t.Column<int>("int",nullable:false).Annotation("SqlServer:Identity","1, 1"), OrganisationId=t.Column<int>("int",nullable:false), TravellerName=t.Column<string>("nvarchar(100)",maxLength:100,nullable:false), Origin=t.Column<string>("nvarchar(max)",nullable:false), Destination=t.Column<string>("nvarchar(max)",nullable:false), Waypoints=t.Column<string>("nvarchar(max)",nullable:true), TransportMode=t.Column<string>("nvarchar(50)",maxLength:50,nullable:false), TravelClass=t.Column<string>("nvarchar(20)",maxLength:20,nullable:false), Passengers=t.Column<int>("int",nullable:false), TripDate=t.Column<DateTime>("datetime2",nullable:false), DistanceKm=t.Column<double>("float",nullable:false), EmissionFactor=t.Column<double>("float",nullable:false), KgCO2e=t.Column<double>("float",nullable:false), Formula=t.Column<string>("nvarchar(max)",nullable:false), DistanceMethodology=t.Column<string>("nvarchar(max)",nullable:false), DefraFactorYear=t.Column<string>("nvarchar(max)",nullable:false), Notes=t.Column<string>("nvarchar(max)",nullable:true), Purpose=t.Column<string>("nvarchar(max)",nullable:true), LoggedBy=t.Column<string>("nvarchar(max)",nullable:false), CreatedAt=t.Column<DateTime>("datetime2",nullable:false) }, c => { c.PrimaryKey("PK_Trips",x=>x.Id); c.ForeignKey("FK_Trips_Organisations_OrganisationId",x=>x.OrganisationId,"Organisations","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.CreateTable("AspNetRoleClaims", t => new { Id=t.Column<int>("int",nullable:false).Annotation("SqlServer:Identity","1, 1"), RoleId=t.Column<string>("nvarchar(450)",nullable:false), ClaimType=t.Column<string>("nvarchar(max)",nullable:true), ClaimValue=t.Column<string>("nvarchar(max)",nullable:true) }, c => { c.PrimaryKey("PK_AspNetRoleClaims",x=>x.Id); c.ForeignKey("FK_AspNetRoleClaims_AspNetRoles_RoleId",x=>x.RoleId,"AspNetRoles","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.CreateTable("AspNetUserClaims", t => new { Id=t.Column<int>("int",nullable:false).Annotation("SqlServer:Identity","1, 1"), UserId=t.Column<string>("nvarchar(450)",nullable:false), ClaimType=t.Column<string>("nvarchar(max)",nullable:true), ClaimValue=t.Column<string>("nvarchar(max)",nullable:true) }, c => { c.PrimaryKey("PK_AspNetUserClaims",x=>x.Id); c.ForeignKey("FK_AspNetUserClaims_AspNetUsers_UserId",x=>x.UserId,"AspNetUsers","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.CreateTable("AspNetUserLogins", t => new { LoginProvider=t.Column<string>("nvarchar(450)",nullable:false), ProviderKey=t.Column<string>("nvarchar(450)",nullable:false), ProviderDisplayName=t.Column<string>("nvarchar(max)",nullable:true), UserId=t.Column<string>("nvarchar(450)",nullable:false) }, c => { c.PrimaryKey("PK_AspNetUserLogins",x=>new{x.LoginProvider,x.ProviderKey}); c.ForeignKey("FK_AspNetUserLogins_AspNetUsers_UserId",x=>x.UserId,"AspNetUsers","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.CreateTable("AspNetUserRoles", t => new { UserId=t.Column<string>("nvarchar(450)",nullable:false), RoleId=t.Column<string>("nvarchar(450)",nullable:false) }, c => { c.PrimaryKey("PK_AspNetUserRoles",x=>new{x.UserId,x.RoleId}); c.ForeignKey("FK_AspNetUserRoles_AspNetRoles_RoleId",x=>x.RoleId,"AspNetRoles","Id",onDelete:ReferentialAction.Cascade); c.ForeignKey("FK_AspNetUserRoles_AspNetUsers_UserId",x=>x.UserId,"AspNetUsers","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.CreateTable("AspNetUserTokens", t => new { UserId=t.Column<string>("nvarchar(450)",nullable:false), LoginProvider=t.Column<string>("nvarchar(450)",nullable:false), Name=t.Column<string>("nvarchar(450)",nullable:false), Value=t.Column<string>("nvarchar(max)",nullable:true) }, c => { c.PrimaryKey("PK_AspNetUserTokens",x=>new{x.UserId,x.LoginProvider,x.Name}); c.ForeignKey("FK_AspNetUserTokens_AspNetUsers_UserId",x=>x.UserId,"AspNetUsers","Id",onDelete:ReferentialAction.Cascade); });
            migrationBuilder.InsertData("Organisations",new[]{"Id","ContactEmail","Name"},new object[]{1,"admin@rittal.co.uk","Rittal UK"});
            migrationBuilder.CreateIndex("IX_AspNetRoleClaims_RoleId","AspNetRoleClaims","RoleId");
            migrationBuilder.CreateIndex("RoleNameIndex","AspNetRoles","NormalizedName",unique:true,filter:"[NormalizedName] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_AspNetUserClaims_UserId","AspNetUserClaims","UserId");
            migrationBuilder.CreateIndex("IX_AspNetUserLogins_UserId","AspNetUserLogins","UserId");
            migrationBuilder.CreateIndex("IX_AspNetUserRoles_RoleId","AspNetUserRoles","RoleId");
            migrationBuilder.CreateIndex("EmailIndex","AspNetUsers","NormalizedEmail");
            migrationBuilder.CreateIndex("UserNameIndex","AspNetUsers","NormalizedUserName",unique:true,filter:"[NormalizedUserName] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_Trips_OrganisationId","Trips","OrganisationId");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("AspNetRoleClaims"); migrationBuilder.DropTable("AspNetUserClaims");
            migrationBuilder.DropTable("AspNetUserLogins"); migrationBuilder.DropTable("AspNetUserRoles");
            migrationBuilder.DropTable("AspNetUserTokens"); migrationBuilder.DropTable("AspNetRoles");
            migrationBuilder.DropTable("AspNetUsers"); migrationBuilder.DropTable("Trips");
            migrationBuilder.DropTable("Organisations");
        }
    }
}
