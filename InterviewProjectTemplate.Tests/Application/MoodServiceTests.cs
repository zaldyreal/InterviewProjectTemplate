using FluentAssertions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Exceptions;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Domain.Entities;
using InterviewProjectTemplate.Domain.Enums;
using InterviewProjectTemplate.Infrastructure.Persistence;
using InterviewProjectTemplate.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InterviewProjectTemplate.Tests.Application
{
    public class MoodServiceTests : IDisposable
    {
        private static readonly DateOnly Today = new(2026, 8, 6);
        private const string UserKey = "11111111111111111111111111111111";
        private const string OtherUserKey = "22222222222222222222222222222222";

        private readonly SqliteDbContextFixture _fixture = new();
        private readonly FixedDateTimeProvider _clock = new(Today);

        public void Dispose() => _fixture.Dispose();

        private MoodService CreateService(MoodTrackerDbContext context) =>
            new(context, _clock, NullLogger<MoodService>.Instance);

        // ---- Recording a mood --------------------------------------------------------------------

        [Fact]
        public async Task CreateAsync_StoresTheEntryAgainstTodayAndTheCallersUserKey()
        {
            using var context = _fixture.CreateContext();

            var result = await CreateService(context).CreateAsync(
                UserKey,
                new CreateMoodEntryRequest
                {
                    Rating = MoodRating.PrettyGood,
                    Comment = "Shipped the release."
                });

            result.Rating.Should().Be(MoodRating.PrettyGood);
            result.RatingLabel.Should().Be("Pretty good");
            result.Comment.Should().Be("Shipped the release.");
            result.MoodDate.Should().Be(Today);

            using var verifyContext = _fixture.CreateContext();
            var stored = await verifyContext.MoodEntries.SingleAsync();

            stored.UserKey.Should().Be(UserKey);
            stored.MoodDate.Should().Be(Today);
            stored.Rating.Should().Be(MoodRating.PrettyGood);
            stored.CreatedAtUtc.Should().Be(_clock.UtcNow);
        }

        [Theory]
        [InlineData(MoodRating.NotGoodAtAll)]
        [InlineData(MoodRating.ABitMeh)]
        [InlineData(MoodRating.PrettyGood)]
        [InlineData(MoodRating.FeelingGreat)]
        public async Task CreateAsync_AcceptsEveryMoodOptionOfferedToUsers(MoodRating rating)
        {
            using var context = _fixture.CreateContext();

            var result = await CreateService(context)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = rating });

            result.Rating.Should().Be(rating);
        }

        // ---- The comment is optional -------------------------------------------------------------

        [Fact]
        public async Task CreateAsync_AllowsAnEntryWithNoComment()
        {
            using var context = _fixture.CreateContext();

            var result = await CreateService(context).CreateAsync(
                UserKey,
                new CreateMoodEntryRequest { Rating = MoodRating.FeelingGreat, Comment = null });

            result.Comment.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public async Task CreateAsync_TreatsABlankCommentAsNoComment(string comment)
        {
            using var context = _fixture.CreateContext();

            // A whitespace-only comment carries no information; storing NULL keeps the data honest
            // and means the admin report does not render empty comment rows.
            var result = await CreateService(context).CreateAsync(
                UserKey,
                new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh, Comment = comment });

            result.Comment.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_TrimsSurroundingWhitespaceFromTheComment()
        {
            using var context = _fixture.CreateContext();

            var result = await CreateService(context).CreateAsync(
                UserKey,
                new CreateMoodEntryRequest
                {
                    Rating = MoodRating.ABitMeh,
                    Comment = "  Long day.  "
                });

            result.Comment.Should().Be("Long day.");
        }

        // ---- One entry per user per day ----------------------------------------------------------

        [Fact]
        public async Task CreateAsync_RejectsASecondSubmissionOnTheSameDay()
        {
            using var firstContext = _fixture.CreateContext();
            await CreateService(firstContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.PrettyGood });

            using var secondContext = _fixture.CreateContext();
            var secondAttempt = async () => await CreateService(secondContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh });

            var exception = await secondAttempt.Should()
                .ThrowAsync<DuplicateMoodEntryException>();

            exception.Which.MoodDate.Should().Be(Today);

            using var verifyContext = _fixture.CreateContext();
            var stored = await verifyContext.MoodEntries.SingleAsync();

            // The original entry must survive untouched; a rejected duplicate must not overwrite it.
            stored.Rating.Should().Be(MoodRating.PrettyGood);
        }

        [Fact]
        public async Task CreateAsync_AllowsTheSameUserToSubmitAgainOnAFollowingDay()
        {
            using var firstContext = _fixture.CreateContext();
            await CreateService(firstContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh });

            _clock.AdvanceDays(1);

            using var secondContext = _fixture.CreateContext();
            var result = await CreateService(secondContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.FeelingGreat });

            result.MoodDate.Should().Be(Today.AddDays(1));

            using var verifyContext = _fixture.CreateContext();
            (await verifyContext.MoodEntries.CountAsync()).Should().Be(2);
        }

        [Fact]
        public async Task CreateAsync_DoesNotLetOneUsersEntryBlockAnother()
        {
            using var firstContext = _fixture.CreateContext();
            await CreateService(firstContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.NotGoodAtAll });

            using var secondContext = _fixture.CreateContext();
            var result = await CreateService(secondContext).CreateAsync(
                OtherUserKey,
                new CreateMoodEntryRequest { Rating = MoodRating.FeelingGreat });

            result.Rating.Should().Be(MoodRating.FeelingGreat);

            using var verifyContext = _fixture.CreateContext();
            (await verifyContext.MoodEntries.CountAsync()).Should().Be(2);
        }

        [Fact]
        public async Task Database_RejectsADuplicateEntryEvenWhenTheApplicationCheckIsBypassed()
        {
            // Proves the rule is enforced by the schema, not only by the service's pre-check. This is
            // what protects against two concurrent requests both passing that check.
            using var context = _fixture.CreateContext();

            context.MoodEntries.AddRange(
                new MoodEntry
                {
                    UserKey = UserKey,
                    MoodDate = Today,
                    Rating = MoodRating.PrettyGood,
                    CreatedAtUtc = _clock.UtcNow
                },
                new MoodEntry
                {
                    UserKey = UserKey,
                    MoodDate = Today,
                    Rating = MoodRating.ABitMeh,
                    CreatedAtUtc = _clock.UtcNow
                });

            var save = async () => await context.SaveChangesAsync();

            await save.Should().ThrowAsync<DbUpdateException>();
        }

        // ---- Today's status ----------------------------------------------------------------------

        [Fact]
        public async Task GetTodayStatusAsync_ReportsNotSubmittedForAFirstTimeVisitor()
        {
            using var context = _fixture.CreateContext();

            var status = await CreateService(context).GetTodayStatusAsync(UserKey);

            status.HasSubmittedToday.Should().BeFalse();
            status.Entry.Should().BeNull();
            status.Date.Should().Be(Today);
        }

        [Fact]
        public async Task GetTodayStatusAsync_ReturnsTheExistingEntryOnceSubmitted()
        {
            using var createContext = _fixture.CreateContext();
            await CreateService(createContext).CreateAsync(
                UserKey,
                new CreateMoodEntryRequest { Rating = MoodRating.PrettyGood, Comment = "Good day." });

            using var context = _fixture.CreateContext();
            var status = await CreateService(context).GetTodayStatusAsync(UserKey);

            status.HasSubmittedToday.Should().BeTrue();
            status.Entry!.Rating.Should().Be(MoodRating.PrettyGood);
            status.Entry.Comment.Should().Be("Good day.");
        }

        [Fact]
        public async Task GetTodayStatusAsync_IgnoresEntriesFromPreviousDays()
        {
            using var createContext = _fixture.CreateContext();
            await CreateService(createContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh });

            _clock.AdvanceDays(1);

            using var context = _fixture.CreateContext();
            var status = await CreateService(context).GetTodayStatusAsync(UserKey);

            status.HasSubmittedToday.Should().BeFalse();
        }

        // ---- Admin report -----------------------------------------------------------------------

        [Fact]
        public async Task GetAllAsync_ReturnsEveryUsersEntriesMostRecentFirst()
        {
            await SeedEntriesAcrossDaysAsync();

            using var context = _fixture.CreateContext();
            var page = await CreateService(context).GetAllAsync(page: 1, pageSize: 25);

            page.TotalCount.Should().Be(3);
            page.Items.Should().HaveCount(3);
            page.Items.Select(entry => entry.CreatedAtUtc)
                .Should().BeInDescendingOrder();
            page.Items.First().Comment.Should().Be("newest");
        }

        [Fact]
        public async Task GetAllAsync_PagesResultsAndReportsTheTotalCount()
        {
            await SeedEntriesAcrossDaysAsync();

            using var context = _fixture.CreateContext();
            var page = await CreateService(context).GetAllAsync(page: 2, pageSize: 2);

            page.Items.Should().HaveCount(1);
            page.TotalCount.Should().Be(3);
            page.Page.Should().Be(2);
            page.TotalPages.Should().Be(2);
            page.Items.Single().Comment.Should().Be("oldest");
        }

        [Theory]
        [InlineData(0, 25)]
        [InlineData(-5, 25)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(1, 10_000)]
        public async Task GetAllAsync_ClampsOutOfRangePagingParameters(int page, int pageSize)
        {
            // A client must not be able to request page 0 or an unbounded page size; the service
            // normalises rather than throwing, so the admin page cannot be broken by a bad query string.
            await SeedEntriesAcrossDaysAsync();

            using var context = _fixture.CreateContext();
            var result = await CreateService(context).GetAllAsync(page, pageSize);

            result.Page.Should().BeGreaterThanOrEqualTo(1);
            result.PageSize.Should().BeInRange(1, MoodService.MaxPageSize);
            result.Items.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_IdentifiesEntriesByAStableReferenceRatherThanTheRawUserKey()
        {
            using var firstContext = _fixture.CreateContext();
            await CreateService(firstContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh });

            _clock.AdvanceDays(1);

            using var secondContext = _fixture.CreateContext();
            await CreateService(secondContext)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = MoodRating.PrettyGood });

            using var thirdContext = _fixture.CreateContext();
            await CreateService(thirdContext)
                .CreateAsync(OtherUserKey, new CreateMoodEntryRequest { Rating = MoodRating.PrettyGood });

            using var context = _fixture.CreateContext();
            var page = await CreateService(context).GetAllAsync(1, 25);

            var references = page.Items.Select(entry => entry.UserReference).ToList();

            // The same person is recognisable across days, different people are distinguishable, and
            // the raw cookie value is never exposed to the admin report.
            references.Should().OnlyContain(reference => !reference.Contains(UserKey));
            references.Distinct().Should().HaveCount(2);
        }

        private async Task SeedEntriesAcrossDaysAsync()
        {
            using var context = _fixture.CreateContext();

            context.MoodEntries.AddRange(
                new MoodEntry
                {
                    UserKey = UserKey,
                    MoodDate = Today.AddDays(-2),
                    Rating = MoodRating.NotGoodAtAll,
                    Comment = "oldest",
                    CreatedAtUtc = _clock.UtcNow.AddDays(-2)
                },
                new MoodEntry
                {
                    UserKey = UserKey,
                    MoodDate = Today.AddDays(-1),
                    Rating = MoodRating.ABitMeh,
                    Comment = "middle",
                    CreatedAtUtc = _clock.UtcNow.AddDays(-1)
                },
                new MoodEntry
                {
                    UserKey = OtherUserKey,
                    MoodDate = Today,
                    Rating = MoodRating.FeelingGreat,
                    Comment = "newest",
                    CreatedAtUtc = _clock.UtcNow
                });

            await context.SaveChangesAsync();
        }

        // ---- Options ----------------------------------------------------------------------------

        [Fact]
        public void GetOptions_ReturnsTheFourMoodsWithTheExactLabelsFromTheBrief()
        {
            using var context = _fixture.CreateContext();

            var options = CreateService(context).GetOptions();

            options.Select(option => option.Label).Should().Equal(
                "Not good at all",
                "A bit “meh”",
                "Pretty good",
                "Feeling great");
        }

        // ---- Guard clauses ----------------------------------------------------------------------

        [Fact]
        public async Task CreateAsync_RejectsAMissingRating()
        {
            using var context = _fixture.CreateContext();

            var act = async () => await CreateService(context)
                .CreateAsync(UserKey, new CreateMoodEntryRequest { Rating = null });

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_RejectsAMissingUserKey(string userKey)
        {
            using var context = _fixture.CreateContext();

            var act = async () => await CreateService(context)
                .CreateAsync(userKey, new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh });

            await act.Should().ThrowAsync<ArgumentException>();
        }
    }
}
