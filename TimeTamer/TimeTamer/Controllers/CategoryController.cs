using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TimeTamer.Data;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

            var stats = await _context.TaskItems
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Category != null && !t.IsDeleted && !t.IsArchived)
                .GroupBy(t => t.Category!.TaskType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Type, v => v.Count);

            return View(stats);
        }
    }
}

