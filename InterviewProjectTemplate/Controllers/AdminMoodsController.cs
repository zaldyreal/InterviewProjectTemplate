using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterviewProjectTemplate.Controllers
{
    /// <summary>
    /// Admin reporting endpoints. Every action requires a valid admin JWT; authorisation is applied
    /// at the controller so a newly added action is protected by default rather than by remembering
    /// to annotate it.
    /// </summary>
    [ApiController]
    [Route("api/admin/moods")]
    [Authorize(Roles = AdminAuthService.AdminRole)]
    [Produces("application/json")]
    public class AdminMoodsController : ControllerBase
    {
        private readonly IMoodService _moodService;

        public AdminMoodsController(IMoodService moodService)
        {
            _moodService = moodService;
        }

        /// <summary>All mood entries, most recent first.</summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(PagedResponse<AdminMoodEntryResponse>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PagedResponse<AdminMoodEntryResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = MoodService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            // Paged rather than returning the whole table: the endpoint has to stay usable once the
            // team has been logging moods daily for a year. The service clamps the page size.
            return Ok(await _moodService.GetAllAsync(page, pageSize, cancellationToken));
        }
    }
}
