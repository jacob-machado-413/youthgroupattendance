using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;
using YouthGroupAttendance.Api.Models;
using System.Text;

namespace YouthGroupAttendance.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly YouthGroupContext _context;

    public StudentsController(YouthGroupContext context)
    {
        _context = context;
    }

    private static string? GenderToString(Gender? gender) =>
        gender?.ToString();

    [HttpGet]
    public async Task<ActionResult<List<StudentResponse>>> GetAllStudents()
    {
        var students = await _context.Students
            .Include(s => s.Attendances)
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
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentDetailResponse>> GetStudent(int id)
    {
        var student = await _context.Students
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound($"Student with ID {id} not found.");

        return Ok(new StudentDetailResponse
        {
            Id = student.Id,
            FullName = student.FullName,
            GraduationYear = student.GraduationYear,
            Gender = GenderToString(student.Gender),
            School = student.School,
            CreatedAt = student.CreatedAt,
            Attendances = student.Attendances
                .OrderByDescending(a => a.Date)
                .Select(a => new AttendanceRecord
                {
                    Id = a.Id,
                    Date = a.Date,
                    EventType = a.EventType.ToString(),
                    Notes = a.Notes,
                    CreatedAt = a.CreatedAt
                })
                .ToList()
        });
    }

    [HttpGet("inactive")]
    public async Task<ActionResult<List<InactiveStudentResponse>>> GetInactiveStudents([FromQuery] int weeks = 3)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-(weeks * 7));

        var students = await _context.Students
            .Include(s => s.Attendances)
            .Where(s => !s.Attendances.Any() || s.Attendances.All(a => a.Date < cutoff))
            .ToListAsync();

        var result = students.Select(s =>
        {
            var lastDate = s.Attendances.Count > 0
                ? s.Attendances.Max(a => a.Date)
                : (DateTime?)null;

            return new InactiveStudentResponse
            {
                Id = s.Id,
                FullName = s.FullName,
                GraduationYear = s.GraduationYear,
                Gender = GenderToString(s.Gender),
                School = s.School,
                TotalAttendances = s.Attendances.Count,
                LastAttendanceDate = lastDate,
                DaysSinceLastAttendance = lastDate.HasValue
                    ? (int)(DateTime.UtcNow.Date - lastDate.Value).TotalDays
                    : -1,
                NeverAttended = s.Attendances.Count == 0
            };
        })
        .OrderByDescending(s => s.DaysSinceLastAttendance)
        .ToList();

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<StudentResponse>>> SearchStudents([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return await GetAllStudents();

        var students = await _context.Students
            .Include(s => s.Attendances)
            .Where(s => EF.Functions.Like(s.FullName, $"%{name}%"))
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
        }).ToList();

        return Ok(result);
    }

    [HttpPost("merge")]
    public async Task<ActionResult<MergeStudentsResponse>> MergeStudents([FromBody] MergeStudentsRequest request)
    {
        if (request.SourceStudentId == request.DestinationStudentId)
            return BadRequest("Cannot merge a student into themselves.");

        var source = await _context.Students
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.Id == request.SourceStudentId);

        if (source == null)
            return NotFound($"Source student with ID {request.SourceStudentId} not found.");

        var destination = await _context.Students
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.Id == request.DestinationStudentId);

        if (destination == null)
            return NotFound($"Destination student with ID {request.DestinationStudentId} not found.");

        var attendancesMoved = source.Attendances.Count;

        // Re-assign all attendance records from source to destination
        foreach (var attendance in source.Attendances)
        {
            attendance.StudentId = destination.Id;
        }

        _context.Students.Remove(source);
        await _context.SaveChangesAsync();

        // Reload destination with its merged attendances
        var merged = await _context.Students
            .Include(s => s.Attendances)
            .FirstAsync(s => s.Id == destination.Id);

        return Ok(new MergeStudentsResponse
        {
            MergedStudent = new StudentDetailResponse
            {
                Id = merged.Id,
                FullName = merged.FullName,
                GraduationYear = merged.GraduationYear,
                Gender = GenderToString(merged.Gender),
                School = merged.School,
                CreatedAt = merged.CreatedAt,
                Attendances = merged.Attendances
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecord
                    {
                        Id = a.Id,
                        Date = a.Date,
                        EventType = a.EventType.ToString(),
                        Notes = a.Notes,
                        CreatedAt = a.CreatedAt
                    })
                    .ToList()
            },
            AttendancesMoved = attendancesMoved,
            SourceStudentId = source.Id,
            SourceStudentName = source.FullName
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportStudents()
    {
        var students = await _context.Students
            .Include(s => s.Attendances)
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Id,FullName,GraduationYear,Grade,Gender,School,TotalAttendances,CreatedAt");

        foreach (var s in students)
        {
            var grade = GraduationYearToGrade(s.GraduationYear);
            var gender = GenderToString(s.Gender) ?? "";
            var school = s.School ?? "";
            csv.AppendLine($"{s.Id},\"{s.FullName}\",{s.GraduationYear},{grade},{gender},\"{school}\",{s.Attendances.Count},{s.CreatedAt:yyyy-MM-dd}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "students.csv");
    }

    private static string GraduationYearToGrade(int graduationYear)
    {
        var now = DateTime.UtcNow;
        var academicYear = now.Month >= 9 ? now.Year + 1 : now.Year;
        var gradeLevel = 12 - (graduationYear - academicYear);
        return gradeLevel switch
        {
            6 => "6th",
            7 => "7th",
            8 => "8th",
            9 => "9th",
            10 => "10th",
            11 => "11th",
            12 => "12th",
            _ => $"{gradeLevel}th"
        };
    }
}
