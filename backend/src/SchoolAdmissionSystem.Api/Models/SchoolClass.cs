namespace SchoolAdmissionSystem.Api.Models;

public class SchoolClass
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SeatHold> Holds { get; set; } = new List<SeatHold>();
}

