using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Purrnet.Services;
using System.Security.Claims;

namespace Purrnet.Controllers.Api
{
    [ApiController]
    [Route("api/v1/admin")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IPackageService _packageService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, IPackageService packageService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _packageService = packageService;
            _logger = logger;
        }

        [HttpPost("packages/{packageId}/approve")]
        public async Task<IActionResult> ApprovePackage(int packageId)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var success = await _adminService.ApprovePackageAsync(packageId, adminId);

            return success ? Ok() : BadRequest("Failed to approve package");
        }

        [HttpPost("packages/{packageId}/reject")]
        public async Task<IActionResult> RejectPackage(int packageId, [FromBody] RejectRequest request)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var success = await _adminService.RejectPackageAsync(packageId, adminId, request.Reason);

            return success ? Ok() : BadRequest("Failed to reject package");
        }

        [HttpPost("packages/{packageId}/toggle")]
        public async Task<IActionResult> TogglePackageStatus(int packageId)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var success = await _adminService.TogglePackageStatusAsync(packageId, adminId);

            return success ? Ok() : BadRequest("Failed to toggle package status");
        }

        [HttpDelete("packages/{packageId}")]
        public async Task<IActionResult> DeletePackage(int packageId)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            var success = await _packageService.DeletePackageAsync(packageId);
            return success ? Ok() : BadRequest("Failed to delete package");
        }

        [HttpPut("packages/{packageId}")]
        public async Task<IActionResult> EditPackage(int packageId, [FromBody] Purrnet.Models.PurrConfig config)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            var userName = User.Identity?.Name ?? "admin";
            var success = await _packageService.UpdatePackageAsync(packageId, config, userName);
            return success ? Ok() : BadRequest("Failed to update package");
        }
    }

    public class RejectRequest
    {
        public string? Reason { get; set; }
    }
}
