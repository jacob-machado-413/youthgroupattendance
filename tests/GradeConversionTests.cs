using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class GradeConversionTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public GradeConversionTests(CustomWebApplicationFactory factory)
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
    public async Task GradeConversion_10thGrade_GraduatesIn2029()
    {
        var request = new AttendanceRequest { FullName = "Sophomore", Grade = "10th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2029, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_12thGrade_GraduatesIn2027()
    {
        var request = new AttendanceRequest { FullName = "Senior", Grade = "12th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2027, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_9thGrade_GraduatesIn2030()
    {
        var request = new AttendanceRequest { FullName = "Freshman", Grade = "9" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2030, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_8thGrade_GraduatesIn2031()
    {
        var request = new AttendanceRequest { FullName = "EighthGrader", Grade = "8th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2031, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_NumericInput_WorksCorrectly()
    {
        var request = new AttendanceRequest { FullName = "NumericGrade", Grade = "11" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2028, result.GraduationYear);
    }
}
