using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Purrnet.Services;

namespace Purrnet.Pages.Admin
{
    [Authorize]
    public class MakeAdminModel : BasePageModel
    {
        private readonly IUserService _userService;
        
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public MakeAdminModel(IUserService userService, TestingModeService testingModeService) 
            : base(testingModeService)
        {
            _userService = userService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is already admin
            if (User.HasClaim("IsAdmin", "1"))
            {
                return RedirectToPage("/Admin/Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var success = await _userService.MakeFirstUserAdminAsync();
            
            if (success)
            {
                Message = "You are now an admin! Please log out and log back in for changes to take effect.";
                IsSuccess = true;
            }
            else
            {
                Message = "Failed to make you admin. You might not be the first user or already an admin.";
                IsSuccess = false;
            }

            return Page();
        }
    }
}
