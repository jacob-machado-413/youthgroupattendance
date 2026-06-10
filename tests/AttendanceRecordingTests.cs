using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class AttendanceRecordingTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public AttendanceRecordingTests(CustomWebApplicationFactory factory)
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
    public async Task RecordAttendance_CreatesNewStudent_WhenStudentDoesNotExist()
    {
        var request = new AttendanceRequest
        {
            FullName = "Test New Student",
            Grade = "10th"
        };

        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Test New Student", result.FullName);
        Assert.True(result.IsNewStudent);
    }

    [Fact]
    public async Task RecordAttendance_ReturnsExistingStudent_WhenStudentAlreadyExists()
    {
        // First attendance
        var request1 = new AttendanceRequest { FullName = "Returning Student", Grade = "11th" };
        await _client.PostAsJsonAsync("/api/attendance", request1);

        // Second attendance
        var request2 = new AttendanceRequest { FullName = "Returning Student", Grade = "11th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request2);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Returning Student", result.FullName);
        Assert.False(result.IsNewStudent);
    }

    [Fact]
    public async Task RecordAttendance_ReturnsSameStudentId_ForReturningStudent()
    {
        var request = new AttendanceRequest { FullName = "Same ID Test", Grade = "9th" };

        var response1 = await _client.PostAsJsonAsync("/api/attendance", request);
        var result1 = await response1.Content.ReadFromJsonAsync<AttendanceResponse>();

        var response2 = await _client.PostAsJsonAsync("/api/attendance", request);
        var result2 = await response2.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.StudentId, result2.StudentId);
    }

    [Fact]
    public async Task RecordAttendance_WithCustomDate_UsesThatDate()
    {
        var customDate = new DateTime(2025, 12, 25);
        var request = new AttendanceRequest
        {
            FullName = "Christmas Student",
            Grade = "12th",
            Date = customDate
        };

        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(customDate, result.Date);
    }

    [Fact]
    public async Task GetAttendanceByDate_ReturnsOnlyThatDatesRecords()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;

        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Yesterday Student",
            Grade = "10th",
            Date = yesterday
        });

        await _client.PostAsJsonAsync("/api/attendance", new AttendanceRequest
        {
            FullName = "Today Student",
            Grade = "11th",
            Date = today
        });

        var response = await _client.GetAsync($"/api/attendance/by-date?date={today:yyyy-MM-dd}");
        var records = await response.Content.ReadFromJsonAsync<List<AttendanceResponse>>();

        Assert.NotNull(records);
        Assert.Single(records);
        Assert.Equal("Today Student", records[0].FullName);
    }

    [Fact]
    public async Task RecordAttendanceByStudentId_WorksForExistingStudent()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "By ID Test", Grade = "10th" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var byIdResponse = await _client.PostAsJsonAsync("/api/attendance/by-student-id",
            new AttendanceByStudentIdRequest { StudentId = created.StudentId });
        var result = await byIdResponse.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(created.StudentId, result.StudentId);
        Assert.False(result.IsNewStudent);
    }

    [Fact]
    public async Task RecordAttendanceByStudentId_Returns404_ForNonexistentStudent()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance/by-student-id",
            new AttendanceByStudentIdRequest { StudentId = 9999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordAttendance_MissingFullName_ReturnsBadRequest()
    {
        var request = new { FullName = "", Grade = "10th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordAttendance_MissingGrade_ReturnsBadRequest()
    {
        var request = new { FullName = "No Grade", Grade = "" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SameNameDifferentGrade_ReturnsExistingStudent()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Name Edge Case", Grade = "10th" });

        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Name Edge Case", Grade = "11th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.False(result.IsNewStudent);
        // Graduation year should remain the original
        Assert.Equal(2029, result.GraduationYear);
    }
}
