using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TimeTamer.Data;

namespace TimeTamer.Controllers
{
    [Authorize]
    public class AchievementsController : Controller
    {
        private readonly AppDbContext _context;

        public AchievementsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var achievements = await _context.UserAchievements
                .Include(a => a.AwardedByAdmin)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.AwardedAt)
                .ToListAsync();
            return View(achievements);
        }
    }
}
