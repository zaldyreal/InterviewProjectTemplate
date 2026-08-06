using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InterviewProjectTemplate.Infrastructure.Persistence.Converters
{
    /// <summary>
    /// Maps <see cref="DateOnly"/> to a <see cref="DateTime"/> at midnight for storage.
    /// <para>
    /// Applied explicitly rather than relying on native provider support: it keeps the mapping
    /// identical between the MySQL provider used at runtime and the SQLite provider used in tests,
    /// so a passing test genuinely reflects production behaviour.
    /// </para>
    /// </summary>
    public class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
    {
        public DateOnlyConverter()
            : base(
                dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
                dateTime => DateOnly.FromDateTime(dateTime))
        {
        }
    }

    /// <summary>
    /// Allows EF Core to compare and hash <see cref="DateOnly"/> values when change-tracking.
    /// </summary>
    public class DateOnlyComparer : ValueComparer<DateOnly>
    {
        public DateOnlyComparer()
            : base(
                (left, right) => left.DayNumber == right.DayNumber,
                dateOnly => dateOnly.GetHashCode())
        {
        }
    }
}