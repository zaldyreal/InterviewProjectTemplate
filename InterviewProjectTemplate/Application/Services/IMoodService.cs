using InterviewProjectTemplate.Application.Dtos;

namespace InterviewProjectTemplate.Application.Services
{
    public interface IMoodService
    {
        /// <summary>
        /// Records today's mood for the given anonymous user.
        /// </summary>
        /// <exception cref="Exceptions.DuplicateMoodEntryException">
        /// Thrown when the user has already recorded a mood today.
        /// </exception>
        Task<MoodEntryResponse> CreateAsync(
            string userKey,
            CreateMoodEntryRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the user has already submitted today, and the entry if so. Lets the UI render the
        /// correct state on first load instead of discovering it through a rejected submission.
        /// </summary>
        Task<TodayMoodStatusResponse> GetTodayStatusAsync(
            string userKey,
            CancellationToken cancellationToken = default);

        /// <summary>All mood entries, most recent first, paged for the admin report.</summary>
        Task<PagedResponse<AdminMoodEntryResponse>> GetAllAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>The selectable mood options with their display labels.</summary>
        IReadOnlyList<MoodOptionResponse> GetOptions();
    }
}
