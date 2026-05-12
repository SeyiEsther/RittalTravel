-- RittalTravelDB Setup Script
-- Run this in SSMS on csmsvr02 before first deployment
-- Ask Mark in IT to confirm the SQL Server instance name

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RittalTravelDB')
BEGIN
    CREATE DATABASE RittalTravelDB;
END
GO

USE RittalTravelDB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId]    nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END
GO

PRINT 'RittalTravelDB setup complete. Start the application to apply EF Core migrations.';
GO
