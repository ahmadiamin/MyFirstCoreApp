using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public partial class ProjectService : IProjectService
{
	private readonly ApplicationDbContext _context;
	private readonly ICurrentUser _currentUser;

	public ProjectService(
		ApplicationDbContext context,
		ICurrentUser currentUser)
	{
		_context = context;
		_currentUser = currentUser;
	}

	// ============================================
	// Project List / Details
	// ============================================

	public async Task<List<ProjectListItemDto>> GetListAsync(
		int tenantId,
		ProjectStatus? status = null,
		string? search = null)
	{
		var query = _context.Set<Project>()
			.AsNoTracking()
			.Where(x => x.TenantId == tenantId);

		if (status.HasValue)
			query = query.Where(x => x.Status == status.Value);

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim();

			query = query.Where(x =>
				x.Title.Contains(term) ||
				(x.Code != null && x.Code.Contains(term)) ||
				(x.Contact != null && x.Contact.Name.Contains(term)));
		}

		var projects = await query
			.OrderByDescending(x => x.CreatedAt)
			.Select(x => new ProjectListItemDto
			{
				Id = x.Id,
				Title = x.Title,
				Code = x.Code,
				ContactName = x.Contact != null ? x.Contact.Name : null,
				ProjectManagerName = null,
				Status = x.Status,
				StartDate = x.StartDate,
				PlannedEndDate = x.PlannedEndDate,
				ActualEndDate = x.ActualEndDate,
				StageCount = x.Stages.Count,
				CompletedStageCount = x.Stages.Count(s =>
					s.Status == ProjectStageStatus.Completed ||
					s.Status == ProjectStageStatus.Skipped)
			})
			.ToListAsync();

		var managerIds = await query
			.Where(x => x.ProjectManagerUserTenantId.HasValue)
			.Select(x => x.ProjectManagerUserTenantId!.Value)
			.Distinct()
			.ToListAsync();

		var names = await GetUserTenantNamesAsync(managerIds, tenantId);

		// برای جلوگیری از کوئری جداگانه به ازای هر پروژه
		var managerByProject = await query
			.Select(x => new
			{
				x.Id,
				x.ProjectManagerUserTenantId
			})
			.ToDictionaryAsync(x => x.Id, x => x.ProjectManagerUserTenantId);

		foreach (var item in projects)
		{
			if (managerByProject.TryGetValue(item.Id, out var managerId) &&
				managerId.HasValue &&
				names.TryGetValue(managerId.Value, out var name))
			{
				item.ProjectManagerName = name;
			}
		}

		return projects;
	}

	public async Task<ProjectDetailsDto?> GetByIdAsync(
		int id,
		int tenantId)
	{
		var project = await _context.Set<Project>()
			.AsNoTracking()
			.Include(x => x.Contact)
			.Include(x => x.Members)
			.Include(x => x.Stages.OrderBy(s => s.SortOrder))
				.ThenInclude(x => x.Histories.OrderByDescending(h => h.ChangedAt))
			.FirstOrDefaultAsync(x =>
				x.Id == id &&
				x.TenantId == tenantId);

		if (project is null)
			return null;

		var userTenantIds = project.Members
			.Select(x => x.UserTenantId)
			.Concat(project.Stages
				.Where(x => x.AssignedUserTenantId.HasValue)
				.Select(x => x.AssignedUserTenantId!.Value))
			.ToList();

		if (project.ProjectManagerUserTenantId.HasValue)
			userTenantIds.Add(project.ProjectManagerUserTenantId.Value);

		var names = await GetUserTenantNamesAsync(
			userTenantIds.Distinct().ToList(),
			tenantId);

		names.TryGetValue(
			project.ProjectManagerUserTenantId ?? 0,
			out var managerName);

		return new ProjectDetailsDto
		{
			Id = project.Id,
			Title = project.Title,
			Code = project.Code,
			ContactId = project.ContactId,
			ContactName = project.Contact?.Name,
			ProjectManagerUserTenantId = project.ProjectManagerUserTenantId,
			ProjectManagerName = managerName,
			Status = project.Status,
			Address = project.Address,
			Description = project.Description,
			StartDate = project.StartDate,
			PlannedEndDate = project.PlannedEndDate,
			ActualEndDate = project.ActualEndDate,
			HoldReason = project.HoldReason,
			CancellationReason = project.CancellationReason,

			Members = project.Members
				.OrderBy(x => x.Id)
				.Select(x =>
				{
					names.TryGetValue(x.UserTenantId, out var name);

					return new ProjectMemberDto
					{
						Id = x.Id,
						UserTenantId = x.UserTenantId,
						UserName = name ?? "کاربر نامشخص",
						JobTitle = null,
						RoleTitle = x.RoleTitle,
						Notes = x.Notes
					};
				})
				.ToList(),

			Stages = project.Stages
				.OrderBy(x => x.SortOrder)
				.Select(x =>
				{
					string? assignedName = null;

					if (x.AssignedUserTenantId.HasValue)
					{
						names.TryGetValue(
							x.AssignedUserTenantId.Value,
							out assignedName);
					}

					return new ProjectStageDto
					{
						Id = x.Id,
						Title = x.Title,
						Description = x.Description,
						SortOrder = x.SortOrder,
						IsRequired = x.IsRequired,
						Status = x.Status,
						AssignedUserTenantId = x.AssignedUserTenantId,
						AssignedUserName = assignedName,
						PlannedStartDate = x.PlannedStartDate,
						PlannedEndDate = x.PlannedEndDate,
						StartedAt = x.StartedAt,
						CompletedAt = x.CompletedAt,
						WaitingReason = x.WaitingReason,
						NextFollowUpDate = x.NextFollowUpDate,
						Notes = x.Notes,
						Histories = x.Histories
							.OrderByDescending(h => h.ChangedAt)
							.Select(h => new ProjectStageHistoryDto
							{
								Id = h.Id,
								PreviousStatus = h.PreviousStatus,
								NewStatus = h.NewStatus,
								Description = h.Description,
								ChangedByUserId = h.ChangedByUserId,
								ChangedAt = h.ChangedAt
							})
							.ToList()
					};
				})
				.ToList()
		};
	}

	// ============================================
	// Create / Update / Delete
	// ============================================

	public async Task<int> CreateAsync(
		ProjectRequest request,
		int tenantId)
	{
		ValidateProjectRequest(request);

		await ValidateContactAsync(request.ContactId, tenantId);
		await ValidateUserTenantAsync(
			request.ProjectManagerUserTenantId,
			tenantId);

		var code = NormalizeOptional(request.Code);

		await EnsureProjectCodeIsUniqueAsync(
			code, tenantId, null);

		var project = new Project
		{
			TenantId = tenantId,
			Title = request.Title.Trim(),
			Code = code,
			ContactId = request.ContactId,
			ProjectManagerUserTenantId = request.ProjectManagerUserTenantId,
			Address = NormalizeOptional(request.Address),
			Description = NormalizeOptional(request.Description),
			StartDate = request.StartDate,
			PlannedEndDate = request.PlannedEndDate,
			Status = ProjectStatus.Draft
		};

		_context.Set<Project>().Add(project);
		await _context.SaveChangesAsync();

		return project.Id;
	}

	public async Task UpdateAsync(
		ProjectRequest request,
		int tenantId)
	{
		if (request.Id <= 0)
			throw new InvalidOperationException("شناسه پروژه معتبر نیست.");

		ValidateProjectRequest(request);

		var project = await _context.Set<Project>()
			.FirstOrDefaultAsync(x =>
				x.Id == request.Id &&
				x.TenantId == tenantId);

		if (project is null)
			throw new InvalidOperationException("پروژه پیدا نشد.");

		if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
			throw new InvalidOperationException(
				"پروژه تکمیل‌شده یا لغوشده قابل ویرایش نیست.");

		await ValidateContactAsync(request.ContactId, tenantId);
		await ValidateUserTenantAsync(
			request.ProjectManagerUserTenantId,
			tenantId);

		var code = NormalizeOptional(request.Code);

		await EnsureProjectCodeIsUniqueAsync(
			code, tenantId, project.Id);

		project.Title = request.Title.Trim();
		project.Code = code;
		project.ContactId = request.ContactId;
		project.ProjectManagerUserTenantId =
			request.ProjectManagerUserTenantId;
		project.Address = NormalizeOptional(request.Address);
		project.Description = NormalizeOptional(request.Description);
		project.StartDate = request.StartDate;
		project.PlannedEndDate = request.PlannedEndDate;

		await _context.SaveChangesAsync();
	}

	public async Task DeleteAsync(int id, int tenantId)
	{
		var project = await _context.Set<Project>()
			.Include(x => x.Members)
			.Include(x => x.Stages)
			.FirstOrDefaultAsync(x =>
				x.Id == id &&
				x.TenantId == tenantId);

		if (project is null)
			throw new InvalidOperationException("پروژه پیدا نشد.");

		if (project.Status != ProjectStatus.Draft)
			throw new InvalidOperationException(
				"فقط پروژه پیش‌نویس قابل حذف است.");

		var now = DateTime.UtcNow;
		var userId = _currentUser.UserId;

		project.IsDeleted = true;
		project.DeletedAt = now;
		project.DeletedBy = userId;

		foreach (var member in project.Members)
		{
			member.IsDeleted = true;
			member.DeletedAt = now;
			member.DeletedBy = userId;
		}

		foreach (var stage in project.Stages)
		{
			stage.IsDeleted = true;
			stage.DeletedAt = now;
			stage.DeletedBy = userId;
		}

		// تاریخچه‌ها حذف نمی‌شوند؛ سوابق قبلی حفظ می‌شوند.
		await _context.SaveChangesAsync();
	}

	// ============================================
	// Project Status
	// ============================================

	public async Task ChangeStatusAsync(
		ProjectStatusChangeRequest request,
		int tenantId)
	{
		var project = await _context.Set<Project>()
			.Include(x => x.Stages)
			.FirstOrDefaultAsync(x =>
				x.Id == request.ProjectId &&
				x.TenantId == tenantId);

		if (project is null)
			throw new InvalidOperationException("پروژه پیدا نشد.");

		if (!Enum.IsDefined(typeof(ProjectStatus), request.NewStatus))
			throw new InvalidOperationException("وضعیت پروژه معتبر نیست.");

		if (project.Status == request.NewStatus)
			throw new InvalidOperationException(
				"پروژه در وضعیت انتخاب‌شده قرار دارد.");

		var allowed = project.Status switch
		{
			ProjectStatus.Draft =>
				request.NewStatus is ProjectStatus.Active
					or ProjectStatus.Cancelled,

			ProjectStatus.Active =>
				request.NewStatus is ProjectStatus.OnHold
					or ProjectStatus.Completed
					or ProjectStatus.Cancelled,

			ProjectStatus.OnHold =>
				request.NewStatus is ProjectStatus.Active
					or ProjectStatus.Cancelled,

			_ => false
		};

		if (!allowed)
			throw new InvalidOperationException(
				"تغییر وضعیت پروژه از وضعیت فعلی به وضعیت انتخاب‌شده مجاز نیست.");

		if (request.NewStatus == ProjectStatus.OnHold)
		{
			if (string.IsNullOrWhiteSpace(request.HoldReason))
				throw new InvalidOperationException(
					"برای توقف پروژه، علت توقف الزامی است.");

			project.HoldReason = request.HoldReason.Trim();
		}
		else if (request.NewStatus == ProjectStatus.Active)
		{
			project.HoldReason = null;
		}

		if (request.NewStatus == ProjectStatus.Cancelled)
		{
			if (string.IsNullOrWhiteSpace(request.CancellationReason))
				throw new InvalidOperationException(
					"برای لغو پروژه، علت لغو الزامی است.");

			project.CancellationReason =
				request.CancellationReason.Trim();
		}

		if (request.NewStatus == ProjectStatus.Completed)
		{
			var incompleteRequiredStages = project.Stages.Any(x =>
				x.IsRequired &&
				x.Status is not ProjectStageStatus.Completed
					and not ProjectStageStatus.Skipped);

			if (incompleteRequiredStages)
				throw new InvalidOperationException(
					"تا زمانی که تمام مراحل الزامی تکمیل یا رد نشده‌اند، پروژه قابل تکمیل نیست.");

			project.ActualEndDate = DateOnly.FromDateTime(DateTime.Today);
		}

		project.Status = request.NewStatus;

		await _context.SaveChangesAsync();
	}

	// ============================================
	// Validation / Helpers
	// ============================================

	private static void ValidateProjectRequest(ProjectRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
			throw new InvalidOperationException("عنوان پروژه الزامی است.");

		if (request.Title.Trim().Length > 200)
			throw new InvalidOperationException(
				"عنوان پروژه نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.");

		if (request.Code?.Trim().Length > 50)
			throw new InvalidOperationException(
				"کد پروژه نمی‌تواند بیشتر از ۵۰ کاراکتر باشد.");

		if (request.StartDate.HasValue &&
			request.PlannedEndDate.HasValue &&
			request.PlannedEndDate < request.StartDate)
		{
			throw new InvalidOperationException(
				"تاریخ پایان برنامه‌ریزی‌شده نمی‌تواند قبل از تاریخ شروع باشد.");
		}
	}

	private async Task ValidateContactAsync(
		int? contactId,
		int tenantId)
	{
		if (!contactId.HasValue)
			return;

		var valid = await _context.Set<Contact>().AnyAsync(x =>
			x.Id == contactId.Value &&
			x.TenantId == tenantId &&
			x.IsActive);

		if (!valid)
			throw new InvalidOperationException(
				"طرف حساب انتخاب‌شده وجود ندارد، غیرفعال است یا متعلق به شرکت جاری نیست.");
	}

	private async Task ValidateUserTenantAsync(
		int? userTenantId,
		int tenantId)
	{
		if (!userTenantId.HasValue)
			return;

		var valid = await (
			from membership in _context.Set<UserTenant>()
			join user in _context.Set<ApplicationUser>()
				on membership.UserId equals user.Id
			where membership.Id == userTenantId.Value
				  && membership.TenantId == tenantId
				  && user.IsActive
			select membership.Id
		).AnyAsync();

		if (!valid)
			throw new InvalidOperationException(
				"کاربر انتخاب‌شده عضو فعال شرکت نیست یا حساب کاربری او غیرفعال است.");
	}

	private async Task EnsureProjectCodeIsUniqueAsync(
		string? code,
		int tenantId,
		int? exceptProjectId)
	{
		if (string.IsNullOrWhiteSpace(code))
			return;

		var exists = await _context.Set<Project>().AnyAsync(x =>
			x.TenantId == tenantId &&
			x.Code == code &&
			(!exceptProjectId.HasValue || x.Id != exceptProjectId.Value));

		if (exists)
			throw new InvalidOperationException(
				"این کد پروژه قبلاً در شرکت ثبت شده است.");
	}

	private async Task<Dictionary<int, string>> GetUserTenantNamesAsync(
		List<int> userTenantIds,
		int tenantId)
	{
		if (userTenantIds.Count == 0)
			return new Dictionary<int, string>();

		var rows = await (
			from membership in _context.Set<UserTenant>().AsNoTracking()
			join user in _context.Set<ApplicationUser>().AsNoTracking()
				on membership.UserId equals user.Id
			where userTenantIds.Contains(membership.Id)
				  && membership.TenantId == tenantId
			select new
			{
				membership.Id,
				membership.FirstName,
				membership.LastName,
				user.FullName,
				membership.JobTitle
			}
		).ToListAsync();

		return rows.ToDictionary(
			x => x.Id,
			x =>
			{
				var staffName =
					$"{x.FirstName} {x.LastName}".Trim();

				return !string.IsNullOrWhiteSpace(staffName)
					? staffName
					: x.FullName;
			});
	}

	private static string? NormalizeOptional(string? value)
		=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}