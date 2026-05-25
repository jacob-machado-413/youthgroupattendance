using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

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

    /// <summary>
    /// Get all students with their total attendance count.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StudentResponse>>> GetAllStudents()
    {
        var students = await _context.Students
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                FullName = s.FullName,
                GraduationYear = s.GraduationYear,
                CreatedAt = s.CreatedAt,
                TotalAttendances = s.Attendances.Count
            })
            .OrderBy(s => s.FullName)
            .ToListAsync();

        return Ok(students);
    }

    /// <summary>
    /// Get a specific student with their full attendance history.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentDetailResponse>> GetStudent(int id)
    {
        var student = await _context.Students
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound($"Student with ID {id} not found.");
        }

        return Ok(new StudentDetailResponse
        {
            Id = student.Id,
            FullName = student.FullName,
            GraduationYear = student.GraduationYear,
            CreatedAt = student.CreatedAt,
            Attendances = student.Attendances
                .OrderByDescending(a => a.Date)
                .Select(a => new AttendanceRecord
                {
                    Id = a.Id,
                    Date = a.Date,
                    CreatedAt = a.CreatedAt
                })
                .ToList()
        });
    }

    /// <summary>
    /// Search students by full name (case-insensitive).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<StudentResponse>>> SearchStudents([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return await GetAllStudents();
        }

        var students = await _context.Students
            .Where(s => s.FullName.Contains(name))
            .Select(s => new StudentResponse
            {
                Id = s.Id,
                FullName = s.FullName,
                GraduationYear = s.GraduationYear,
                CreatedAt = s.CreatedAt,
                TotalAttendances = s.Attendances.Count
            })
            .OrderBy(s => s.FullName)
            .ToListAsync();

        return Ok(students);
    }
}
