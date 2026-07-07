# Rittal Travel — Internal Business Travel Carbon Reporting

## Prerequisites
- .NET 8 SDK
- SQL Server (ask Mark in IT for instance name)
- Google Maps API key (ask Esther)

## Setup
1. Run RittalTravelDB.sql in SSMS
2. Configure secrets (do not commit real credentials to source control):
   - **Development:** set values in `appsettings.Development.json`, or use .NET User Secrets:
     ```bash
     dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=YOUR_SERVER;Initial Catalog=RittalTravelDB;..."
     dotnet user-secrets set "GoogleMaps:ApiKey" "YOUR_GOOGLE_MAPS_KEY"
     ```
   - **Production (IIS):** set environment variables on the app pool or site:
     - `ConnectionStrings__DefaultConnection`
     - `GoogleMaps__ApiKey`
3. Open in Visual Studio and press F5 to run locally

## Deploy to IIS
1. Right-click project in Visual Studio -- Publish
2. Choose Folder, publish to C:\inetpub\RittalTravel
3. In IIS Manager create new site pointing to that folder
4. Set application pool to No Managed Code
5. Ensure SQL Server allows connections from the IIS server
6. Set `ConnectionStrings__DefaultConnection` and `GoogleMaps__ApiKey` as environment variables on the site

## Reset Seed Data
Delete all rows from Trips table in SSMS and restart the app.

## Support
Contact Esther Ogunbayo -- seyi@usecarbontrack.co.uk
