using System.ComponentModel.DataAnnotations;

namespace RittalTravel.Models;

public class Trip
{
    public int Id { get; set; }
    public int OrganisationId { get; set; }

    [Required]
    [StringLength(100)]
    public string TravellerName { get; set; } = "";

    [Required]
    public string Origin { get; set; } = "";

    [Required]
    public string Destination { get; set; } = "";

    public string? Waypoints { get; set; }

    [Required]
    public string TransportMode { get; set; } = "";

    public string TravelClass { get; set; } = "Economy";

    [Range(1, 500)]
    public int Passengers { get; set; } = 1;

    public DateTime TripDate { get; set; } = DateTime.Today;

    public double DistanceKm { get; set; }
    public double EmissionFactor { get; set; }
    public double KgCO2e { get; set; }
    public string Formula { get; set; } = "";
    public string DistanceMethodology { get; set; } = "";
    public string DefraFactorYear { get; set; } = "DEFRA 2025";
    public string? Notes { get; set; }
    public string? Purpose { get; set; }
    public string LoggedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organisation Organisation { get; set; } = null!;
}
