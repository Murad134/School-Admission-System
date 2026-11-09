using System.Text.RegularExpressions;

namespace SchoolAdmissionSystem.Api.Common;

public static class StudentIdValidator
{
    private static readonly Regex Regex = new(@"^[0-9]{17}$", RegexOptions.Compiled);

    public static bool IsValid(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return false;

        return Regex.IsMatch(studentId.Trim());
    }
}
