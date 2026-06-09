namespace YouthGroupAttendance.Frontend.Helpers;

public static class GradeHelper
{
    /// <summary>
    /// Returns the cutover date: the end (Sunday) of the first full week of June.
    /// Before this date, the academic year is the current year.
    /// On or after this date, the academic year advances by one.
    /// </summary>
    public static DateTime GetGradeCutoverDate(int year)
    {
        var june1 = new DateTime(year, 6, 1);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)june1.DayOfWeek + 7) % 7;
        var firstMonday = june1.AddDays(daysUntilMonday);
        return firstMonday.AddDays(6);
    }

    /// <summary>
    /// Converts a GraduationYear to the current grade string (e.g. "10th").
    /// Academic year advances at the end of the first full week of June.
    /// </summary>
    public static string GraduationYearToGrade(int graduationYear)
    {
        var now = DateTime.UtcNow;
        var cutover = GetGradeCutoverDate(now.Year);
        var academicYear = now >= cutover ? now.Year + 1 : now.Year;
        var gradeLevel = 12 - (graduationYear - academicYear);

        return gradeLevel switch
        {
            6 => "6th",
            7 => "7th",
            8 => "8th",
            9 => "9th",
            10 => "10th",
            11 => "11th",
            12 => "12th",
            _ => $"{gradeLevel}th"
        };
    }

    /// <summary>
    /// Returns the list of grade options for dropdowns.
    /// </summary>
    public static string[] GradeOptions => new[]
    {
        "6th", "7th", "8th", "9th", "10th", "11th", "12th"
    };
}
