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
}
