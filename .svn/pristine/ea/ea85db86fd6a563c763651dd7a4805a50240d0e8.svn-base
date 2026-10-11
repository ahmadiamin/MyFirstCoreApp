using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services.Interfaces;

namespace MyFirstCoreApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RolesController : Controller
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [Authorize(Policy = "Role.View")]
        public async Task<IActionResult> Index()
        {
            var roles = await _roleService.GetAllAsync();

            return View(roles);
        }


        // GET: /Admin/Roles/Create
        [HttpGet]
        [Authorize(Policy = "Role.Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Role.Create")]
        public async Task<IActionResult> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "نام نقش الزامی است.");

                return View();
            }

            var normalizedName = name.Trim();

            if (await _roleService.ExistsAsync(normalizedName))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "این نقش قبلاً وجود دارد.");

                return View();
            }

            var result =
                await _roleService.CreateAsync(normalizedName);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);

            return View();
        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }
        }

        [HttpGet]
        [Authorize(Policy = "Role.Edit")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var model =
                await _roleService.GetEditModelAsync(id);

            if (model == null)
                return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "Role.Edit")]
        public async Task<IActionResult> Edit(
            EditRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadPermissions(model);
                return View(model);
            }

            var result =
                await _roleService.UpdateAsync(model);

            if (!result.Succeeded)
            {
                AddErrors(result);

                await LoadPermissions(model);

                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }


        private async Task LoadPermissions(
    EditRoleViewModel model)
        {
            model.PermissionGroups =
                await _roleService.GetPermissionGroupsAsync(
                    model.Id);
        }
    }
}