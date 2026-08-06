using System.Security.Cryptography;
using System.Text;
using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Exceptions;
using InterviewProjectTemplate.Application.Mapping;
using InterviewProjectTemplate.Domain.Entities;
using InterviewProjectTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InterviewProjectTemplate.Application.Services
{
    public class MoodService : IMoodService
    {
        /// <summary>Guards against a client requesting an unbounded page of the admin report.</summary>
        public const int MaxPageSize = 200;

        public const int DefaultPageSize = 25;

        private readonly MoodTrackerDbContext _dbContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<MoodService> _logger;

        public MoodService(
            MoodTrackerDbContext dbContext,
            IDateTimeProvider dateTimeProvider,
            ILogger<MoodService> logger)
        {
            _dbContext = dbContext;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
        }

        public async Task<MoodEntryResponse> CreateAsync(
            string userKey,
            CreateMoodEntryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userKey);
            ArgumentNullException.ThrowIfNull(request);

            if (request.Rating is null)
            {
                throw new ArgumentException("A mood rating is required.", nameof(request));
            }

            var today = _dateTimeProvider.Today;

            // Fast path: a cheap check so the common case produces a clear error without relying on
            // an exception from the database.
            var alreadyRecorded = await _dbContext.MoodEntries
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.UserKey == userKey && entry.MoodDate == today,
                    cancellationToken);

            if (alreadyRecorded)
            {
                throw new DuplicateMoodEntryException(today);
            }

            var moodEntry = new MoodEntry
            {
                UserKey = userKey,
                MoodDate = today,
                Rating = request.Rating.Value,
                Comment = NormaliseComment(request.Comment),
                CreatedAtUtc = _dateTimeProvider.UtcNow
            };

            _dbContext.MoodEntries.Add(moodEntry);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                // Two simultaneous submissions can both clear the check above; the unique index is
                // what actually enforces the rule, so a violation here is the same business outcome.
                _logger.LogInformation(
                    exception,
                    "Concurrent duplicate mood submission rejected by the unique index for {MoodDate}.",
                    today);

                _dbContext.Entry(moodEntry).State = EntityState.Detached;

                throw new DuplicateMoodEntryException(today);
            }

            _logger.LogInformation(
                "Recorded mood {Rating} for {MoodDate}.",
                moodEntry.Rating,
                moodEntry.MoodDate);

            return ToResponse(moodEntry);
        }

        public async Task<TodayMoodStatusResponse> GetTodayStatusAsync(
            string userKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userKey);

            var today = _dateTimeProvider.Today;

            var entry = await _dbContext.MoodEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    moodEntry => moodEntry.UserKey == userKey && moodEntry.MoodDate == today,
                    cancellationToken);

            return new TodayMoodStatusResponse
            {
                HasSubmittedToday = entry is not null,
                Date = today,
                Entry = entry is null ? null : ToResponse(entry)
            };
        }

        public async Task<PagedResponse<AdminMoodEntryResponse>> GetAllAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize switch
            {
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => pageSize
            };

            var query = _dbContext.MoodEntries.AsNoTracking();

            var totalCount = await query.CountAsync(cancellationToken);

            var entries = await query
                // Most recent first, as required. Id is a stable tie-breaker so that entries sharing
                // a timestamp keep a deterministic order across pages.
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .ThenByDescending(entry => entry.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<AdminMoodEntryResponse>
            {
                Items = entries.Select(ToAdminResponse).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public IReadOnlyList<MoodOptionResponse> GetOptions() =>
            MoodRatingLabels.AllInDisplayOrder
                .Select(rating => new MoodOptionResponse
                {
                    Value = rating,
                    Label = MoodRatingLabels.GetLabel(rating)
                })
                .ToList();

        /// <summary>
        /// Treats a whitespace-only comment as no comment, so the database holds NULL rather than a
        /// meaningless empty string.
        /// </summary>
        private static string? NormaliseComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        private static MoodEntryResponse ToResponse(MoodEntry entry) => new()
        {
            Id = entry.Id,
            Rating = entry.Rating,
            RatingLabel = MoodRatingLabels.GetLabel(entry.Rating),
            Comment = entry.Comment,
            MoodDate = entry.MoodDate,
            CreatedAtUtc = entry.CreatedAtUtc
        };

        private static AdminMoodEntryResponse ToAdminResponse(MoodEntry entry) => new()
        {
            Id = entry.Id,
            Rating = entry.Rating,
            RatingLabel = MoodRatingLabels.GetLabel(entry.Rating),
            Comment = entry.Comment,
            MoodDate = entry.MoodDate,
            CreatedAtUtc = entry.CreatedAtUtc,
            UserReference = BuildUserReference(entry.UserKey)
        };

        /// <summary>
        /// Derives a short, stable, non-reversible label for an anonymous user. The admin can see
        /// that two entries came from the same person without the raw cookie value being exposed,
        /// which would otherwise let anyone holding the report impersonate that user's identity.
        /// </summary>
        private static string BuildUserReference(string userKey)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userKey));

            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

        /// <summary>
        /// Detects a unique-index violation without binding to a specific database provider, so the
        /// same code path is exercised by the SQLite-backed tests and by MySQL in production.
        /// </summary>
        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            // MySQL reports 1062 (ER_DUP_ENTRY); SQLite reports 19 (SQLITE_CONSTRAINT). Rather than
            // hardcoding provider error numbers, match on the constraint wording that both surface.
            var message = exception.InnerException?.Message ?? exception.Message;

            return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
        }
    }
}
