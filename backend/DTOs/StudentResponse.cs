namespace YouthGroupAttendance.Api.DTOs;

public class StudentResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public string? Gender { get; set; }
    public string? School { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalAttendances { get; set; }
}

public class StudentDetailResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public string? Gender { get; set; }
    public string? School { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AttendanceRecord> Attendances { get; set; } = new();
}

public class MergeStudentsRequest
{
    public int SourceStudentId { get; set; }
    public int DestinationStudentId { get; set; }
}

public class MergeStudentsResponse
{
    public StudentDetailResponse MergedStudent { get; set; } = null!;
    public int AttendancesMoved { get; set; }
    public int SourceStudentId { get; set; }
    public string SourceStudentName { get; set; } = string.Empty;
}

public class AttendanceRecord
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AttendanceTrendsResponse
{
    public int TotalStudents { get; set; }
    public int TotalAttendances { get; set; }
    public List<AttendanceByDate> AttendanceByDate { get; set; } = new();
    public List<GradeBreakdown> GradeBreakdown { get; set; } = new();
}

public class AttendanceByDate
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class GradeBreakdown
{
    public int GraduationYear { get; set; }
    public int StudentCount { get; set; }
    public int AttendanceCount { get; set; }
}
