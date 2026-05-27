using System.ComponentModel.DataAnnotations;

namespace YouthGroupAttendance.Api.DTOs;

public class AttendanceRequest
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Grade { get; set; } = string.Empty;

    public string? EventType { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    /// <summary>
    /// The school the student attends. Only used when creating a new student.
    /// </summary>
    [MaxLength(100)]
    public string? School { get; set; }

    public DateTime? Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class AttendanceByStudentIdRequest
{
    [Required]
    public int StudentId { get; set; }

    public string? EventType { get; set; }

    public DateTime? Date { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class AttendanceResponse
{
    public int AttendanceId { get; set; }
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public string? Gender { get; set; }
    public string? School { get; set; }
    public DateTime Date { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsNewStudent { get; set; }
}
