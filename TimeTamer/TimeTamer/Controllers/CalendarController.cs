using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TimeTamer.Data;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly AppDbContext _context;
        public CalendarController(AppDbContext context) => _context = context;
        private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        public async Task<IActionResult> Index(int? year, int? month)
        {
            var selected = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);
            var next = selected.AddMonths(1);
            var userId = UserId();
            var tasks = await _context.TaskItems.Include(t => t.Category)
                .Where(t => t.UserId == userId && !t.IsDeleted && !t.IsArchived && ((t.StartTime >= selected && t.StartTime < next) || (t.Deadline >= selected && t.Deadline < next)))
                .OrderBy(t => t.StartTime ?? t.Deadline)
                .ToListAsync();
            ViewBag.SelectedMonth = selected;
            return View(tasks);
        }
    }
}


