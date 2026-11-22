using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyFirstCoreApp.Areas.Projects.Controllers;

// Preserve older bookmarks while the expense module now lives under Accounting.
[Area("Projects"), Authorize]
public sealed class ProjectExpensesController : Controller
{
    [Authorize(Policy = "ProjectExpense.View")]
    public IActionResult Index(int? projectId) => RedirectToAction("Index", "Expenses", new { area = "Accounting", projectId });

    [Authorize(Policy = "ProjectExpense.Create")]
    public IActionResult Create(int? projectId) => RedirectToAction("Create", "Expenses", new { area = "Accounting", projectId });

    [Authorize(Policy = "ProjectExpense.View")]
    public IActionResult Details(int id) => RedirectToAction("Details", "Expenses", new { area = "Accounting", id });
}
