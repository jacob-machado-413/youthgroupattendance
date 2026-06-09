using System.Net.Http.Json;
using YouthGroupAttendance.Frontend.Models;

namespace YouthGroupAttendance.Frontend.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;

    public ApiService(HttpClient http, AuthState auth)
    {
        _http = http;
        _auth = auth;
    }

    private void SetAuthHeader()
    {
        _http.DefaultRequestHeaders.Remove("X-API-Key");
        if (_auth.ApiKey != null)
            _http.DefaultRequestHeaders.Add("X-API-Key", _auth.ApiKey);
    }

    // ── Attendance ───────────────────────────────────────────────────────

    public async Task<AttendanceResponse?> RecordAttendanceAsync(AttendanceRequest request)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("/api/attendance", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttendanceResponse>();
    }

    public async Task<List<AttendanceResponse>> GetAttendanceByDateAsync(DateTime? date = null)
    {
        SetAuthHeader();
        var url = date.HasValue
            ? $"/api/attendance/by-date?date={date.Value:yyyy-MM-dd}"
            : "/api/attendance/by-date";
        return await _http.GetFromJsonAsync<List<AttendanceResponse>>(url) ?? new();
    }

    public async Task<List<StudentResponse>> GetStudentsByDateAsync(DateTime? date = null)
    {
        SetAuthHeader();
        var url = date.HasValue
            ? $"/api/attendance/students-by-date?date={date.Value:yyyy-MM-dd}"
            : "/api/attendance/students-by-date";
        return await _http.GetFromJsonAsync<List<StudentResponse>>(url) ?? new();
    }

    public async Task<AttendanceTrendsResponse?> GetTrendsAsync(DateTime? from = null, DateTime? to = null)
    {
        SetAuthHeader();
        var url = "/api/attendance/trends";
        var parts = new List<string>();
        if (from.HasValue) parts.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) parts.Add($"to={to.Value:yyyy-MM-dd}");
        if (parts.Count > 0) url += "?" + string.Join("&", parts);
        return await _http.GetFromJsonAsync<AttendanceTrendsResponse>(url);
    }

    // ── Students ─────────────────────────────────────────────────────────

    public async Task<List<StudentResponse>> GetAllStudentsAsync()
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<StudentResponse>>("/api/students") ?? new();
    }

    public async Task<StudentDetailResponse?> GetStudentAsync(int id)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{id}");
    }

    public async Task<List<StudentResponse>> SearchStudentsAsync(string name)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<StudentResponse>>($"/api/students/search?name={Uri.EscapeDataString(name)}") ?? new();
    }

    public async Task<MergeStudentsResponse?> MergeStudentsAsync(MergeStudentsRequest request)
    {
        SetAuthHeader();
        var response = await _http.PostAsJsonAsync("/api/students/merge", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MergeStudentsResponse>();
    }

    public async Task<List<InactiveStudentResponse>> GetInactiveStudentsAsync(int weeks = 3)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<List<InactiveStudentResponse>>($"/api/students/inactive?weeks={weeks}") ?? new();
    }

    // ── CSV Export ──────────────────────────────────────────────────────

    public async Task<string> ExportStudentsCsvAsync()
    {
        SetAuthHeader();
        var response = await _http.GetAsync("/api/students/export");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ExportAttendanceCsvAsync(DateTime? date = null)
    {
        SetAuthHeader();
        var url = date.HasValue ? $"/api/attendance/export?date={date.Value:yyyy-MM-dd}" : "/api/attendance/export";
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ── Auth check ───────────────────────────────────────────────────────

    public async Task<bool> ValidateApiKeyAsync()
    {
        SetAuthHeader();
        try
        {
            var response = await _http.GetAsync("/api/students");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
