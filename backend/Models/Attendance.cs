using System.ComponentModel.DataAnnotations;

namespace YouthGroupAttendance.Api.Models;

public class Attendance
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public DateTime Date { get; set; }

    [Required]
    public EventType EventType { get; set; }

    /// <summary>
    /// Optional notes about the event (e.g. "Spring break, many students missing").
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Student Student { get; set; } = null!;
}
