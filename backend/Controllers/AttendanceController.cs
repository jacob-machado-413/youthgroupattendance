using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;
using YouthGroupAttendance.Api.Models;

namespace YouthGroupAttendance.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly YouthGroupContext _context;

    public AttendanceController(YouthGroupContext context)
    {
        _context = context;
    }

    private static int GradeToGraduationYear(string grade)
    {
        var digits = new string(grade.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var gradeLevel) && gradeLevel >= 1 && gradeLevel <= 12)
        {
            var currentYear = DateTime.UtcNow.Month >= 9
                ? DateTime.UtcNow.Year + 1
                : DateTime.UtcNow.Year;
            return currentYear + (12 - gradeLevel);
        }
        return DateTime.UtcNow.Year + 1;
    }

    private static Models.EventType ParseEventType(string? eventType)
    {
        if (Enum.TryParse<Models.EventType>(eventType, ignoreCase: true, out var result))
            return result;
        return Models.EventType.RegularYouthGroup;
    }

    private static Gender? ParseGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
            return null;
        if (Enum.TryParse<Gender>(gender, ignoreCase: true, out var result))
            return result;
        return null;
    }

    private static string? GenderToString(Gender? gender) =>
        gender?.ToString();

    [HttpPost]
    public async Task<ActionResult<AttendanceResponse>> RecordAttendance([FromBody] AttendanceRequest request)
    {
        var attendanceDate = request.Date?.Date ?? DateTime.UtcNow.Date;
        var eventType = ParseEventType(request.EventType);

        var student = await _context.Students
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.FullName, request.FullName));

        var isNewStudent = false;

        if (student == null)
        {
            student = new Student
            {
                FullName = request.FullName,
                GraduationYear = GradeToGraduationYear(request.Grade),
                Gender = ParseGender(request.Gender),
                School = string.IsNullOrWhiteSpace(request.School) ? null : request.School,
                CreatedAt = DateTime.UtcNow
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();
            isNewStudent = true;
        }

        var attendance = new Attendance
        {
            StudentId = student.Id,
            Date = attendanceDate,
            EventType = eventType,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
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
            Gender = GenderToString(student.Gender),
            School = student.School,
            Date = attendanceDate,
            EventType = eventType.ToString(),
            Notes = attendance.Notes,
            IsNewStudent = isNewStudent
        });
    }

    [HttpPost("by-student-id")]
    public async Task<ActionResult<AttendanceResponse>> RecordAttendanceByStudentId(
        [FromBody] AttendanceByStudentIdRequest request)
    {
        var student = await _context.Students.FindAsync(request.StudentId);
        if (student == null)
            return NotFound($"Student with ID {request.StudentId} not found.");

        var attendanceDate = request.Date?.Date ?? DateTime.UtcNow.Date;
        var eventType = ParseEventType(request.EventType);

        var attendance = new Attendance
        {
            StudentId = student.Id,
            Date = attendanceDate,
            EventType = eventType,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
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
            Gender = GenderToString(student.Gender),
            School = student.School,
            Date = attendanceDate,
            EventType = eventType.ToString(),
            Notes = attendance.Notes,
            IsNewStudent = false
        });
    }

    [HttpGet("by-date")]
    public async Task<ActionResult<List<AttendanceResponse>>> GetAttendanceByDate([FromQuery] DateTime? date)
    {
        var queryDate = date?.Date ?? DateTime.UtcNow.Date;

        var records = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.Date == queryDate)
            .OrderBy(a => a.Student.FullName)
            .ToListAsync();

        var result = records.Select(a => new AttendanceResponse
        {
            AttendanceId = a.Id,
            StudentId = a.StudentId,
            FullName = a.Student.FullName,
            GraduationYear = a.Student.GraduationYear,
            Gender = GenderToString(a.Student.Gender),
            School = a.Student.School,
            Date = a.Date,
            EventType = a.EventType.ToString(),
            Notes = a.Notes,
            IsNewStudent = false
        }).ToList();

        return Ok(result);
    }

    [HttpGet("students-by-date")]
    public async Task<ActionResult<List<StudentResponse>>> GetStudentsByDate([FromQuery] DateTime? date)
    {
        var queryDate = date?.Date ?? DateTime.UtcNow.Date;

        var studentIds = await _context.Attendances
            .Where(a => a.Date == queryDate)
            .Select(a => a.StudentId)
            .Distinct()
            .ToListAsync();

        var students = await _context.Students
            .Include(s => s.Attendances)
            .Where(s => studentIds.Contains(s.Id))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var result = students.Select(s => new StudentResponse
        {
            Id = s.Id,
            FullName = s.FullName,
            GraduationYear = s.GraduationYear,
            Gender = GenderToString(s.Gender),
            School = s.School,
            CreatedAt = s.CreatedAt,
            TotalAttendances = s.Attendances.Count
        }).OrderBy(s => s.FullName).ToList();

        return Ok(result);
    }

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
