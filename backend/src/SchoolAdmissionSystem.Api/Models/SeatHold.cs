namespace SchoolAdmissionSystem.Api.Models;

public class SeatHold
{
    public string Id { get; set; } = null!;
    public string ClassId { get; set; } = null!;
    public string StudentId { get; set; } = null!;
    public string Status { get; set; } = HoldStatus.Held;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SchoolClass? Class { get; set; }
}

