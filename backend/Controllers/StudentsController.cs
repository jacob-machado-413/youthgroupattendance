using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;
using YouthGroupAttendance.Api.Models;

namespace YouthGroupAttendance.Api.Controllers;

[ApiController]
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
            .Where(s => s.FullName.Contains(name))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var result = students.Select(s => new StudentResponse
        {
            Id = s.Id,
            FullName = s.FullName,
            GraduationYear = s.GraduationYear,
            Gender = GenderToString(s.Gender),
            CreatedAt = s.CreatedAt,
            TotalAttendances = s.Attendances.Count
        }).ToList();

        return Ok(result);
    }
}
