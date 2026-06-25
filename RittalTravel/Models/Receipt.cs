namespace RittalTravel.Models;

public class Receipt
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int OrganisationId { get; set; }

    public string? TravellerName { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TripDate { get; set; }
    public string? TransportMode { get; set; }
    public string? Notes { get; set; }
    public string? DocumentType { get; set; }
}
