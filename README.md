# Rittal Travel — Internal Business Travel Carbon Reporting

## Prerequisites
- .NET 8 SDK
- SQL Server (ask Mark in IT for instance name)
- Google Maps API key (ask Esther)

## Setup
1. Run RittalTravelDB.sql in SSMS
2. Update Server= in appsettings.json to match your SQL Server instance
3. Add Google Maps API key to appsettings.json under GoogleMaps:ApiKey
4. Open in Visual Studio and press F5 to run locally

## Deploy to IIS
1. Right-click project in Visual Studio -- Publish
2. Choose Folder, publish to C:\inetpub\RittalTravel
3. In IIS Manager create new site pointing to that folder
4. Set application pool to No Managed Code
5. Ensure SQL Server allows connections from the IIS server

## Reset Seed Data
Delete all rows from Trips table in SSMS and restart the app.

## Support
Contact Esther Ogunbayo -- seyi@usecarbontrack.co.uk
