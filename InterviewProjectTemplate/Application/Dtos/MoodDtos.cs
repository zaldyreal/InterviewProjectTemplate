using System.ComponentModel.DataAnnotations;
using InterviewProjectTemplate.Domain.Enums;
using InterviewProjectTemplate.Infrastructure.Persistence.Configurations;

namespace InterviewProjectTemplate.Application.Dtos
{
    /// <summary>
    /// Inbound payload for a mood submission. Deliberately separate from the entity so that a client
    /// can never set Id, UserKey or CreatedAtUtc — those are the server's responsibility.
    /// </summary>
    public class CreateMoodEntryRequest
    {
        /// <summary>
        /// Nullable so that a missing or unparsable value is reported as "required" rather than
        /// silently binding to the default enum value.
        /// </summary>
        [Required(ErrorMessage = "Please select how you are feeling today.")]
        [EnumDataType(typeof(MoodRating), ErrorMessage = "The selected mood is not valid.")]
        public MoodRating? Rating { get; set; }

        [MaxLength(
            MoodEntryConfiguration.CommentMaxLength,
            ErrorMessage = "Comments cannot be longer than 1000 characters.")]
        public string? Comment { get; set; }
    }

    /// <summary>A stored mood entry as returned to a client.</summary>
    public class MoodEntryResponse
    {
        public int Id { get; init; }

        public MoodRating Rating { get; init; }

        /// <summary>Human-readable label, so the frontend never has to hardcode display text.</summary>
        public string RatingLabel { get; init; } = string.Empty;

        public string? Comment { get; init; }

        public DateOnly MoodDate { get; init; }

        public DateTime CreatedAtUtc { get; init; }
    }

    /// <summary>
    /// Whether the caller has already submitted today, used by the UI to show the form or the
    /// "already recorded" state on first load rather than only after a failed POST.
    /// </summary>
    public class TodayMoodStatusResponse
    {
        public bool HasSubmittedToday { get; init; }

        public DateOnly Date { get; init; }

        public MoodEntryResponse? Entry { get; init; }
    }

    /// <summary>One selectable mood option, served to the frontend to keep labels in one place.</summary>
    public class MoodOptionResponse
    {
        public MoodRating Value { get; init; }

        public string Label { get; init; } = string.Empty;
    }

    /// <summary>A page of mood entries for the admin report.</summary>
    public class AdminMoodEntryResponse : MoodEntryResponse
    {
        /// <summary>
        /// Shortened, non-reversible form of the anonymous user key. Enough for an admin to see that
        /// two entries came from the same person, without exposing the raw cookie value.
        /// </summary>
        public string UserReference { get; init; } = string.Empty;
    }

    public class PagedResponse<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public int TotalCount { get; init; }

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}