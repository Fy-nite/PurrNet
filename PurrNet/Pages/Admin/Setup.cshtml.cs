using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using Purrnet.Data;

namespace Purrnet.Pages.Admin
{
    [Authorize]
    public class SetupModel : PageModel
    {
        private readonly MongoDbContext _context;
        private readonly IConfiguration _configuration;

        public SetupModel(MongoDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Only allow if no admins exist yet
            var hasAdmins = await _context.Users.Find(u => u.IsAdmin).AnyAsync();
            if (hasAdmins)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // don't allow at all.

            return Page();
        }
    }
}
