using SchoolAdmissionSystem.Api.DTOs;

namespace SchoolAdmissionSystem.Api.Services;

public interface IClassService
{
    Task<ClassResponse> CreateClassAsync(CreateClassRequest request);
    Task<List<ClassDetailResponse>> GetAllClassesAsync();
    Task<ClassDetailResponse> GetClassByIdAsync(string classId);
}

