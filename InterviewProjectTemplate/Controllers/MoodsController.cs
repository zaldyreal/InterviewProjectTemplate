using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Exceptions;
using InterviewProjectTemplate.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace InterviewProjectTemplate.Controllers
{
    /// <summary>
    /// Public mood endpoints. Intentionally unauthenticated: the brief requires the once-per-day
    /// rule to work without authentication, so callers are identified by an anonymous cookie.
    /// </summary>
    [ApiController]
    [Route("api/moods")]
    [Produces("application/json")]
    public class MoodsController : ControllerBase
    {
        private readonly IMoodService _moodService;
        private readonly IUserKeyProvider _userKeyProvider;

        public MoodsController(IMoodService moodService, IUserKeyProvider userKeyProvider)
        {
            _moodService = moodService;
            _userKeyProvider = userKeyProvider;
        }

        /// <summary>The four selectable moods and their display labels.</summary>
        [HttpGet("options")]
        [ProducesResponseType(typeof(IReadOnlyList<MoodOptionResponse>), StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<MoodOptionResponse>> GetOptions() =>
            Ok(_moodService.GetOptions());

        /// <summary>
        /// Whether the caller has already recorded a mood today. Called on page load so the UI can
        /// show the correct state immediately; also the request that issues the identity cookie to a
        /// first-time visitor.
        /// </summary>
        [HttpGet("today")]
        [ProducesResponseType(typeof(TodayMoodStatusResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<TodayMoodStatusResponse>> GetToday(
            CancellationToken cancellationToken)
        {
            var userKey = _userKeyProvider.GetOrCreateUserKey();

            return Ok(await _moodService.GetTodayStatusAsync(userKey, cancellationToken));
        }

        /// <summary>Records the caller's mood for today.</summary>
        /// <response code="201">The mood was recorded.</response>
        /// <response code="400">The payload failed validation.</response>
        /// <response code="409">The caller has already recorded a mood today.</response>
        [HttpPost]
        [ProducesResponseType(typeof(MoodEntryResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MoodEntryResponse>> Create(
            [FromBody] CreateMoodEntryRequest request,
            CancellationToken cancellationToken)
        {
            var userKey = _userKeyProvider.GetOrCreateUserKey();

            try
            {
                var entry = await _moodService.CreateAsync(userKey, request, cancellationToken);

                return CreatedAtAction(nameof(GetToday), new { }, entry);
            }
            catch (DuplicateMoodEntryException exception)
            {
                // 409 Conflict is the accurate status: the request was well-formed but conflicts with
                // the current state of the resource. The message is written to be shown to the user
                // directly, satisfying the brief's "an error message should appear".
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Mood already recorded",
                    Detail = "You have already recorded your mood today. "
                           + "Please come back tomorrow.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    Extensions = { ["moodDate"] = exception.MoodDate.ToString("yyyy-MM-dd") }
                });
            }
        }
    }
}
