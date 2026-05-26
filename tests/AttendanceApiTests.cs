using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YouthGroupAttendance.Api.Data;
using YouthGroupAttendance.Api.DTOs;

namespace YouthGroupAttendance.Api.Tests;

public class AttendanceApiTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly YouthGroupContext _context;

    public AttendanceApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _context = factory.Services.GetRequiredService<YouthGroupContext>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clear all data between tests so each test starts with a clean DB
        _context.Attendances.RemoveRange(_context.Attendances);
        _context.Students.RemoveRange(_context.Students);
        await _context.SaveChangesAsync();
    }

    // ── Attendance Recording ──────────────────────────────────────────────

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

    // ── Grade to Graduation Year Conversion ───────────────────────────────

    [Fact]
    public async Task GradeConversion_10thGrade_GraduatesIn2028()
    {
        var request = new AttendanceRequest { FullName = "Sophomore", Grade = "10th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2028, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_12thGrade_GraduatesIn2026()
    {
        var request = new AttendanceRequest { FullName = "Senior", Grade = "12th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2026, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_9thGrade_GraduatesIn2029()
    {
        var request = new AttendanceRequest { FullName = "Freshman", Grade = "9" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2029, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_8thGrade_GraduatesIn2030()
    {
        var request = new AttendanceRequest { FullName = "EighthGrader", Grade = "8th" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2030, result.GraduationYear);
    }

    [Fact]
    public async Task GradeConversion_NumericInput_WorksCorrectly()
    {
        var request = new AttendanceRequest { FullName = "NumericGrade", Grade = "11" };
        var response = await _client.PostAsJsonAsync("/api/attendance", request);
        var result = await response.Content.ReadFromJsonAsync<AttendanceResponse>();

        Assert.NotNull(result);
        Assert.Equal(2027, result.GraduationYear);
    }

    // ── Get Attendance by Date ────────────────────────────────────────────

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

    // ── Students Endpoints ────────────────────────────────────────────────

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
        Assert.Equal(2028, studentA.GraduationYear);

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
        Assert.Equal(2026, student.GraduationYear);
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

    // ── Trends ────────────────────────────────────────────────────────────

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

        Assert.Contains(trends.GradeBreakdown, g => g.GraduationYear == 2026); // 12th
        Assert.Contains(trends.GradeBreakdown, g => g.GraduationYear == 2027); // 11th
    }

    // ── Attendance by Student ID ──────────────────────────────────────────

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

    // ── Validation ────────────────────────────────────────────────────────

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

    // ── Edge Cases ────────────────────────────────────────────────────────

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
        Assert.Equal(2028, result.GraduationYear);
    }

    // ── Event Type ───────────────────────────────────────────────────────────

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

    // ── Gender ───────────────────────────────────────────────────────────────

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

    // ── Notes ────────────────────────────────────────────────────────────────

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
}
