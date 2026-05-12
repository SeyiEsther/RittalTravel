-- RittalTravelDB Setup Script
-- Run this in SSMS on csmsvr02 before first deployment
-- Ask Mark in IT to confirm the SQL Server instance name

USE master;
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RittalTravelDB')
BEGIN
    CREATE DATABASE RittalTravelDB;
END
GO

USE RittalTravelDB;
GO

-- EF Core migrations table (allows Migrate() to track applied migrations)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId]    nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END
GO

-- The application will create all tables automatically via EF Core Migrate() on first run.
-- No manual table creation required. Migrations are in the RittalTravel/Migrations folder.

PRINT 'RittalTravelDB setup complete. Start the application to apply EF Core migrations.';
GO
