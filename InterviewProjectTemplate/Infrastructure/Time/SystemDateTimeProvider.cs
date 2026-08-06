using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Options;
using Microsoft.Extensions.Options;

namespace InterviewProjectTemplate.Infrastructure.Time
{
    /// <summary>
    /// Real-clock implementation of <see cref="IDateTimeProvider"/>.
    /// </summary>
    public class SystemDateTimeProvider : IDateTimeProvider
    {
        private readonly TimeZoneInfo _timeZone;
        private readonly ILogger<SystemDateTimeProvider> _logger;

        public SystemDateTimeProvider(
            IOptions<MoodTrackerOptions> options,
            ILogger<SystemDateTimeProvider> logger)
        {
            _logger = logger;
            _timeZone = ResolveTimeZone(options.Value.TimeZone);
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateOnly Today =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone));

        /// <summary>
        /// Time zone database IDs differ between Windows ("AUS Eastern Standard Time") and the Linux
        /// container the app is deployed in ("Australia/Melbourne"). .NET 8 resolves both on either
        /// platform via ICU, but a misconfigured value should degrade to UTC with a warning rather
        /// than crash the application at startup.
        /// </summary>
        private TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception exception) when (
                exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                _logger.LogWarning(
                    exception,
                    "Configured time zone '{TimeZoneId}' could not be resolved. Falling back to UTC.",
                    timeZoneId);

                return TimeZoneInfo.Utc;
            }
        }
    }
}