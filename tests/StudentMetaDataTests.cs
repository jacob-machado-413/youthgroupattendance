using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class StudentMetaDataTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public StudentMetaDataTests(CustomWebApplicationFactory factory)
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
    public async Task RecordAttendance_DefaultsToRegularYouthGroup_WhenEventTypeNotProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Default Event", Grade = "10th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("RegularYouthGroup", result.EventType);
    }

    [Fact]
    public async Task RecordAttendance_StoresSocialGameNight_WhenSpecified()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Game Night Kid", Grade = "10th", EventType = "SocialGameNight" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("SocialGameNight", result.EventType);
    }

    [Fact]
    public async Task RecordAttendance_StoresEventType_ForAllOptions()
    {
        var types = new[] { "RegularYouthGroup", "SocialGameNight", "ServiceProject", "RetreatCamp" };
        foreach (var eventType in types)
        {
            var response = await _client.PostAsJsonAsync("/api/attendance",
                new AttendanceRequest
                {
                    FullName = $"Event Type {eventType}",
                    Grade = "10th",
                    EventType = eventType
                });
            var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();
            Assert.NotNull(result);
            Assert.Equal(eventType, result.EventType);
        }
    }

    [Fact]
    public async Task GetAttendanceByDate_IncludesEventType()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Event Type Test", Grade = "10th", EventType = "ServiceProject" });

        var response = await _client.GetAsync("/api/attendance/by-date");
        var records = await response.Content.ReadFromJsonAsync<List<AttendanceResponse>>();

        Assert.NotNull(records);
        var record = Assert.Single(records);
        Assert.Equal("ServiceProject", record.EventType);
    }

    [Fact]
    public async Task StudentDetail_IncludesEventTypeInAttendanceHistory()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Detail Event", Grade = "10th", EventType = "SocialGameNight" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/students/{created.StudentId}");
        var student = await response.Content.ReadFromJsonAsync<StudentDetailResponse>();

        Assert.NotNull(student);
        var attendance = Assert.Single(student.Attendances);
        Assert.Equal("SocialGameNight", attendance.EventType);
    }

    [Fact]
    public async Task RecordAttendance_EventTypeIsCaseInsensitive()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Case Test", Grade = "10th", EventType = "socialgamenight" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("SocialGameNight", result.EventType);
    }

    [Fact]
    public async Task RecordAttendance_InvalidEventType_FallsBackToRegular()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Bad Event", Grade = "10th", EventType = "NotARealType" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("RegularYouthGroup", result.EventType);
    }

    [Fact]
    public async Task RecordAttendance_GenderIsNull_WhenNotProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "No Gender", Grade = "10th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Null(result.Gender);
    }

    [Fact]
    public async Task RecordAttendance_StoresGender_WhenProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Male Student", Grade = "10th", Gender = "Male" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("Male", result.Gender);
    }

    [Fact]
    public async Task RecordAttendance_StoresGender_ForFemale()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Female Student", Grade = "10th", Gender = "Female" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("Female", result.Gender);
    }

    [Fact]
    public async Task StudentList_IncludesGender()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Gendered Student", Grade = "10th", Gender = "Male" });

        var response = await _client.GetAsync("/api/students");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        var student = Assert.Single(students);
        Assert.Equal("Male", student.Gender);
    }

    [Fact]
    public async Task StudentDetail_IncludesGender()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Detail Gender", Grade = "10th", Gender = "Female" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/students/{created.StudentId}");
        var student = await response.Content.ReadFromJsonAsync<StudentDetailResponse>();

        Assert.NotNull(student);
        Assert.Equal("Female", student.Gender);
    }

    [Fact]
    public async Task RecordAttendance_NotesIsNull_WhenNotProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "No Notes", Grade = "10th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Null(result.Notes);
    }

    [Fact]
    public async Task RecordAttendance_StoresNotes_WhenProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest
            {
                FullName = "Notes Test",
                Grade = "10th",
                Notes = "Spring break, many students missing"
            });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("Spring break, many students missing", result.Notes);
    }

    [Fact]
    public async Task GetAttendanceByDate_IncludesNotes()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Notes By Date", Grade = "10th", Notes = "Game night was fun" });

        var response = await _client.GetAsync("/api/attendance/by-date");
        var records = await response.Content.ReadFromJsonAsync<List<AttendanceResponse>>();

        Assert.NotNull(records);
        var record = Assert.Single(records);
        Assert.Equal("Game night was fun", record.Notes);
    }

    [Fact]
    public async Task StudentDetail_IncludesNotesInAttendanceHistory()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "Notes Detail", Grade = "10th", Notes = "Camp was great" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/students/{created.StudentId}");
        var student = await response.Content.ReadFromJsonAsync<StudentDetailResponse>();

        Assert.NotNull(student);
        var attendance = Assert.Single(student.Attendances);
        Assert.Equal("Camp was great", attendance.Notes);
    }

    [Fact]
    public async Task RecordAttendance_SchoolIsNull_WhenNotProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "No School", Grade = "10th" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Null(result.School);
    }

    [Fact]
    public async Task RecordAttendance_StoresSchool_WhenProvided()
    {
        var response = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "School Test", Grade = "10th", School = "St. Mary's High School" });
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal("St. Mary's High School", result.School);
    }

    [Fact]
    public async Task StudentList_IncludesSchool()
    {
        await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "School List", Grade = "10th", School = "Central High" });

        var response = await _client.GetAsync("/api/students");
        var students = await response.Content.ReadFromJsonAsync<List<StudentResponse>>();

        Assert.NotNull(students);
        var student = Assert.Single(students);
        Assert.Equal("Central High", student.School);
    }

    [Fact]
    public async Task StudentDetail_IncludesSchool()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/attendance",
            new AttendanceRequest { FullName = "School Detail", Grade = "10th", School = "Westside Academy" });
        var created = await createResponse.Content.ReadFromJsonAsync<AttendanceResponse>();
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/students/{created.StudentId}");
        var student = await response.Content.ReadFromJsonAsync<StudentDetailResponse>();

        Assert.NotNull(student);
        Assert.Equal("Westside Academy", student.School);
    }
}
