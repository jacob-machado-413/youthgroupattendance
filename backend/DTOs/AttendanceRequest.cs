using System.ComponentModel.DataAnnotations;

namespace YouthGroupAttendance.Api.DTOs;

/// <summary>
/// Request to record an attendance entry.
/// If a student with the given FullName exists, a new attendance record is added for them.
/// If not, a new student is created first, then attendance is recorded.
/// </summary>
public class AttendanceRequest
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Grade { get; set; } = string.Empty;

    /// <summary>
    /// Optional: the date of the attendance. Defaults to today's date (UTC) if not provided.
    /// </summary>
    public DateTime? Date { get; set; }
}

/// <summary>
/// Request to record attendance for an existing student by their ID.
/// </summary>
public class AttendanceByStudentIdRequest
{
    [Required]
    public int StudentId { get; set; }

    /// <summary>
    /// Optional: the date of the attendance. Defaults to today's date (UTC) if not provided.
    /// </summary>
    public DateTime? Date { get; set; }
}

/// <summary>
/// Response returned after recording attendance.
/// </summary>
public class AttendanceResponse
{
    public int AttendanceId { get; set; }
    public int StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public DateTime Date { get; set; }
    public bool IsNewStudent { get; set; }
}
