using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class AdminMergeTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public AdminMergeTests(CustomWebApplicationFactory factory)
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
    public async Task MergeStudents_MovesAttendancesAndDeletesSource()
    {
        // Create two students
        var res1 = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Jane Doe", Grade = "10th" });
        var student1 = await res1.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(student1);

        var res2 = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Jane M. Doe", Grade = "10th" });
        var student2 = await res2.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(student2);

        // Jane Doe attends twice, Jane M. Doe attends once
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Jane Doe", Grade = "10th" });

        // Merge Jane M. Doe into Jane Doe
        var mergeResponse = await _client.PostAsJsonAsync("/api/students/merge",
            new MergeStudentsRequest
            {
                SourceStudentId = student2.StudentId,
                DestinationStudentId = student1.StudentId
            });
        var mergeResult = await mergeResponse.Content.ReadFromJsonAsync<MergeStudentsResponse>();

        Assert.Equal(HttpStatusCode.OK, mergeResponse.StatusCode);
        Assert.NotNull(mergeResult);
        Assert.Equal(1, mergeResult.AttendancesMoved);
        Assert.Equal(student2.StudentId, mergeResult.SourceStudentId);
        Assert.Equal("Jane M. Doe", mergeResult.SourceStudentName);

        // Destination should now have 3 attendances (2 original + 1 moved)
        Assert.Equal(3, mergeResult.MergedStudent.Attendances.Count);
        Assert.Equal("Jane Doe", mergeResult.MergedStudent.FullName);

        // Source student should be gone
        var getSource = await _client.GetAsync($"/api/students/{student2.StudentId}");
        Assert.Equal(HttpStatusCode.NotFound, getSource.StatusCode);
    }

    [Fact]
    public async Task MergeStudents_ReturnsBadRequest_WhenIdsAreSame()
    {
        var response = await _client.PostAsJsonAsync("/api/students/merge",
            new MergeStudentsRequest { SourceStudentId = 1, DestinationStudentId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MergeStudents_Returns404_WhenSourceNotFound()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Real Student", Grade = "10th" });

        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Keep Me", Grade = "10th" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.PostAsJsonAsync("/api/students/merge",
            new MergeStudentsRequest { SourceStudentId = 9999, DestinationStudentId = created.StudentId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MergeStudents_Returns404_WhenDestinationNotFound()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Merge Source", Grade = "10th" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.PostAsJsonAsync("/api/students/merge",
            new MergeStudentsRequest { SourceStudentId = created.StudentId, DestinationStudentId = 9999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
