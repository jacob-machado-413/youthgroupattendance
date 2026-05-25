using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;
using YouthGroupAttendance.Api.Models;

namespace YouthGroupAttendance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly YouthGroupContext _context;

    public AttendanceController(YouthGroupContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Convert a grade string (e.g. "10th", "10", "Grade 10") to a graduation year.
    /// Graduation year = current year + (12 - grade level).
    /// </summary>
    private static int GradeToGraduationYear(string grade)
    {
        // Extract the numeric part from the grade string
        var digits = new string(grade.Where(char.IsDigit).ToArray());

        if (int.TryParse(digits, out var gradeLevel) && gradeLevel >= 1 && gradeLevel <= 12)
        {
            var currentYear = DateTime.UtcNow.Month >= 9
                ? DateTime.UtcNow.Year + 1  // After September, we're in the next academic year
                : DateTime.UtcNow.Year;

            return currentYear + (12 - gradeLevel);
        }

        // Fallback: assume current year + 1 (default to freshman)
        return DateTime.UtcNow.Year + 1;
    }

    /// <summary>
    /// Record attendance for a student.
    /// If a student with the given FullName exists, a new attendance record is added for them.
    /// If not, a new student is created first, then attendance is recorded.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AttendanceResponse>> RecordAttendance([FromBody] AttendanceRequest request)
    {
        var attendanceDate = request.Date?.Date ?? DateTime.UtcNow.Date;

        // Look up existing student by full name (case-insensitive)
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.FullName == request.FullName);

        var isNewStudent = false;

        if (student == null)
        {
            // Create new student
            student = new Student
            {
                FullName = request.FullName,
                GraduationYear = GradeToGraduationYear(request.Grade),
                CreatedAt = DateTime.UtcNow
            };

            _context.Students.Add(student);

            // Save the student first so EF generates the Id
            await _context.SaveChangesAsync();

            isNewStudent = true;
        }

        // Record attendance
        var attendance = new Attendance
        {
            StudentId = student.Id,
            Date = attendanceDate,
            CreatedAt = DateTime.UtcNow
        };

        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        return Ok(new AttendanceResponse
        {
            AttendanceId = attendance.Id,
            StudentId = student.Id,
            FullName = student.FullName,
            GraduationYear = student.GraduationYear,
            Date = attendanceDate,
            IsNewStudent = isNewStudent
        });
    }

    /// <summary>
    /// Record attendance for an existing student by their ID.
    /// </summary>
    [HttpPost("by-student-id")]
    public async Task<ActionResult<AttendanceResponse>> RecordAttendanceByStudentId(
        [FromBody] AttendanceByStudentIdRequest request)
    {
        var student = await _context.Students.FindAsync(request.StudentId);

        if (student == null)
        {
            return NotFound($"Student with ID {request.StudentId} not found.");
        }

        var attendanceDate = request.Date?.Date ?? DateTime.UtcNow.Date;

        var attendance = new Attendance
        {
            StudentId = student.Id,
            Date = attendanceDate,
            CreatedAt = DateTime.UtcNow
        };

        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        return Ok(new AttendanceResponse
        {
            AttendanceId = attendance.Id,
            StudentId = student.Id,
            FullName = student.FullName,
            GraduationYear = student.GraduationYear,
            Date = attendanceDate,
            IsNewStudent = false
        });
    }

    /// <summary>
    /// Get attendance records for a specific date.
    /// </summary>
    [HttpGet("by-date")]
    public async Task<ActionResult<List<AttendanceResponse>>> GetAttendanceByDate([FromQuery] DateTime? date)
    {
        var queryDate = date?.Date ?? DateTime.UtcNow.Date;

        var records = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.Date == queryDate)
            .OrderBy(a => a.Student.FullName)
            .Select(a => new AttendanceResponse
            {
                AttendanceId = a.Id,
                StudentId = a.StudentId,
                FullName = a.Student.FullName,
                GraduationYear = a.Student.GraduationYear,
                Date = a.Date,
                IsNewStudent = false
            })
            .ToListAsync();

        return Ok(records);
    }

    /// <summary>
    /// Get attendance trends data.
    /// </summary>
    [HttpGet("trends")]
    public async Task<ActionResult<AttendanceTrendsResponse>> GetTrends(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.MinValue;
        var toDate = to ?? DateTime.MaxValue;

        var totalStudents = await _context.Students.CountAsync();

        var query = _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.Date >= fromDate && a.Date <= toDate);

        var totalAttendances = await query.CountAsync();

        var attendanceByDate = await query
            .GroupBy(a => a.Date)
            .Select(g => new AttendanceByDate
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var gradeBreakdown = await query
            .GroupBy(a => a.Student.GraduationYear)
            .Select(g => new GradeBreakdown
            {
                GraduationYear = g.Key,
                StudentCount = g.Select(a => a.StudentId).Distinct().Count(),
                AttendanceCount = g.Count()
            })
            .OrderBy(x => x.GraduationYear)
            .ToListAsync();

        return Ok(new AttendanceTrendsResponse
        {
            TotalStudents = totalStudents,
            TotalAttendances = totalAttendances,
            AttendanceByDate = attendanceByDate,
            GradeBreakdown = gradeBreakdown
        });
    }
}
