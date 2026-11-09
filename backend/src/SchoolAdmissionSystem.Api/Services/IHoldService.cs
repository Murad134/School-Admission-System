using SchoolAdmissionSystem.Api.DTOs;

namespace SchoolAdmissionSystem.Api.Services;

public interface IHoldService
{
    Task<HoldResponse> CreateHoldAsync(string classId, string studentId);
    Task<HoldDetailResponse> GetHoldByIdAsync(string holdId);
    Task<ConfirmHoldResponse> ConfirmHoldAsync(string holdId, string studentId);
    Task CancelHoldAsync(string holdId, string studentId);
    Task<List<StudentHoldResponse>> GetStudentHoldsAsync(string studentId);
}
