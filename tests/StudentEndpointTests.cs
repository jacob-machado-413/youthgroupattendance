using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class StudentEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public StudentEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key");
        _context = factory.Services.GetRequiredService<YouthGroupContext>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _context.Attendances.RemoveRange(_context.Attendances);
        _context.Students.RemoveRange(_context.Students);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllStudents_ReturnsAllStudentsWithCounts()
    {
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Student A",
            Grade = "10th"
        });
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Student B",
            Grade = "11th"
        });
        // Student A comes twice
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Student A",
            Grade = "10th"
        });

        var response = await _client.GetAsync("/api/students");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.Equal(2, students.Count);

        var studentA = students.First(s => s.FullName == "Student A");
        Assert.Equal(2, studentA.TotalAttendances);
        Assert.Equal(2029, studentA.GraduationYear);

        var studentB = students.First(s => s.FullName == "Student B");
        Assert.Equal(1, studentB.TotalAttendances);
    }

    [Fact]
    public async Task GetStudentsByDate_ReturnsOnlyStudentsWhoAttendedThatDate()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Two students attend today
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Today Student A",
            Grade = "10th",
            Date = today
        });
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Today Student B",
            Grade = "11th",
            Date = today
        });

        // One student attends yesterday
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Yesterday Student",
            Grade = "9th",
            Date = yesterday
        });

        // Query for today
        var response = await _client.GetAsync($"/api/attendance/students-by-date?date={today:yyyy-MM-dd}");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.Equal(2, students.Count);
        Assert.Contains(students, s => s.FullName == "Today Student A");
        Assert.Contains(students, s => s.FullName == "Today Student B");
        Assert.DoesNotContain(students, s => s.FullName == "Yesterday Student");
    }

    [Fact]
    public async Task GetStudentsByDate_ReturnsEachStudentOnlyOnce()
    {
        var today = DateTime.UtcNow.Date;

        // Same student attends twice on the same date (shouldn't happen normally, but guard against it)
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Double Attendance",
            Grade = "10th",
            Date = today
        });
        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Double Attendance",
            Grade = "10th",
            Date = today
        });

        var response = await _client.GetAsync($"/api/attendance/students-by-date?date={today:yyyy-MM-dd}");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.Single(students);
        Assert.Equal("Double Attendance", students[0].FullName);
    }

    [Fact]
    public async Task GetStudentsByDate_DefaultsToToday()
    {
        var today = DateTime.UtcNow.Date;

        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Default Date Student",
            Grade = "10th"
        });

        // No date parameter — should default to today
        var response = await _client.GetAsync("/api/attendance/students-by-date");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.Contains(students, s => s.FullName == "Default Date Student");
    }

    [Fact]
    public async Task GetStudentById_ReturnsStudentWithAttendanceHistory()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Detail Test", Grade = "12th" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/students/{created.StudentId}");
        var student = await response.Content.ReadFromJsonAsync<StudentDetailResponse>();

        Assert.NotNull(student);
        Assert.Equal("Detail Test", student.FullName);
        Assert.Equal(2027, student.GraduationYear);
        Assert.Single(student.Attendances);
    }

    [Fact]
    public async Task GetStudentById_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync("/api/students/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchStudents_ReturnsMatchingResults()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Alice Johnson", Grade = "10th" });
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Bob Johnson", Grade = "11th" });
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Charlie Smith", Grade = "9th" });

        var response = await _client.GetAsync("/api/students/search?name=Johnson");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.Equal(2, students.Count);
        Assert.Contains(students, s => s.FullName == "Alice Johnson");
        Assert.Contains(students, s => s.FullName == "Bob Johnson");
    }

    [Fact]
    public async Task GetTrends_ReturnsAggregatedData()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Senior A", Grade = "12th" });
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Senior B", Grade = "12th" });
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Junior A", Grade = "11th" });

        var response = await _client.GetAsync("/api/attendance/trends");
        var trends = await response.Content.ReadFromJsonAsync<AttendanceTrendsResponse>();

        Assert.NotNull(trends);
        Assert.Equal(3, trends.TotalStudents);
        Assert.Equal(3, trends.TotalAttendances);

        Assert.Contains(trends.GradeBreakdown, g => g.GraduationYear == 2027); // 12th
        Assert.Contains(trends.GradeBreakdown, g => g.GraduationYear == 2028); // 11th
    }

    [Fact]
    public async Task RecordAttendance_FindsExistingStudent_CaseInsensitive()
    {
        // Create with capitalized name
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Jane Doe", Grade = "10th" });

        // Lookup with different casing
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "jane doe", Grade = "10th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.False(result.IsNewStudent);
        Assert.Equal("Jane Doe", result.FullName);
    }

    [Fact]
    public async Task SearchStudents_FindsMatches_CaseInsensitive()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Alice Johnson", Grade = "10th" });

        var response = await _client.GetAsync("/api/students/search?name=johnson");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.NotEmpty(students);
        Assert.Contains(students, s => s.FullName == "Alice Johnson");
    }

    [Fact]
    public async Task SearchStudents_FindsMatches_WithMixedCase()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Bob Smith", Grade = "10th" });

        var response = await _client.GetAsync("/api/students/search?name=BOB");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        Assert.NotEmpty(students);
        Assert.Contains(students, s => s.FullName == "Bob Smith");
    }
}
