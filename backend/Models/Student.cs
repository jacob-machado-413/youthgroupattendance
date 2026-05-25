using System.ComponentModel.DataAnnotations;

namespace YouthGroupAttendance.Api.Models;

public class Student
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public int GraduationYear { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
