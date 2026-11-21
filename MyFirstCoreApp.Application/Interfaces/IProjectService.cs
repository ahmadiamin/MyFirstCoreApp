using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IProjectService
{
    // ============================================
    // Projects
    // ============================================

    Task<List<ProjectListItemDto>> GetListAsync(
        int tenantId,
        ProjectStatus? status = null,
        string? search = null);

    Task<ProjectDetailsDto?> GetByIdAsync(
        int id,
        int tenantId);

    Task<int> CreateAsync(
        ProjectRequest request,
        int tenantId);

    Task UpdateAsync(
        ProjectRequest request,
        int tenantId);

    Task DeleteAsync(
        int id,
        int tenantId);

    Task ChangeStatusAsync(
        ProjectStatusChangeRequest request,
        int tenantId);

    // ============================================
    // Project Members
    // ============================================

    Task<int> AddMemberAsync(
        ProjectMemberRequest request,
        int tenantId);

    Task UpdateMemberAsync(
        int memberId,
        string? roleTitle,
        string? notes,
        int tenantId);

    Task RemoveMemberAsync(
        int memberId,
        int tenantId);

    // ============================================
    // Project Stages
    // ============================================

    Task<int> AddStageAsync(
        ProjectStageRequest request,
        int tenantId);

    Task UpdateStageAsync(
        ProjectStageRequest request,
        int tenantId);

    Task DeleteStageAsync(
        int stageId,
        int tenantId);

    Task ChangeStageStatusAsync(
        ProjectStageStatusChangeRequest request,
        int tenantId);
}
