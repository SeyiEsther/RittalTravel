using Microsoft.EntityFrameworkCore;
using RittalTravel.Models;

namespace RittalTravel.Data;

public class RittalTravelContext : DbContext
{
    public RittalTravelContext(DbContextOptions<RittalTravelContext> options)
        : base(options) { }

    public DbSet<Trip> Trips { get; set; } = null!;
    public DbSet<Organisation> Organisations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organisation>().HasData(
            new Organisation { Id = 1, Name = "Rittal UK", ContactEmail = "admin@rittal.co.uk" }
        );

        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Organisation)
            .WithMany()
            .HasForeignKey(t => t.OrganisationId);

        modelBuilder.Entity<Trip>().Property(t => t.TravellerName).HasMaxLength(100);
        modelBuilder.Entity<Trip>().Property(t => t.TransportMode).HasMaxLength(50);
        modelBuilder.Entity<Trip>().Property(t => t.TravelClass).HasMaxLength(20);
    }
}
