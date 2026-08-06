using FluentAssertions;
using InterviewProjectTemplate.Application.Abstractions;
using InterviewProjectTemplate.Application.Dtos;
using InterviewProjectTemplate.Application.Exceptions;
using InterviewProjectTemplate.Application.Services;
using InterviewProjectTemplate.Controllers;
using InterviewProjectTemplate.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InterviewProjectTemplate.Tests.Controllers
{
    /// <summary>
    /// Controller-level tests: they verify the HTTP contract the Angular client depends on. The
    /// business rules themselves are covered against a real database in
    /// <see cref="Application.MoodServiceTests"/>.
    /// </summary>
    public class MoodsControllerTests
    {
        private static readonly DateOnly Today = new(2026, 8, 6);
        private const string UserKey = "11111111111111111111111111111111";

        private readonly IMoodService _moodService = Substitute.For<IMoodService>();
        private readonly IUserKeyProvider _userKeyProvider = Substitute.For<IUserKeyProvider>();
        private readonly MoodsController _controller;

        public MoodsControllerTests()
        {
            _userKeyProvider.GetOrCreateUserKey().Returns(UserKey);
            _controller = new MoodsController(_moodService, _userKeyProvider);
        }

        [Fact]
        public async Task Create_Returns201WhenTheMoodIsRecorded()
        {
            var request = new CreateMoodEntryRequest { Rating = MoodRating.PrettyGood };

            _moodService
                .CreateAsync(UserKey, request, Arg.Any<CancellationToken>())
                .Returns(new MoodEntryResponse
                {
                    Id = 1,
                    Rating = MoodRating.PrettyGood,
                    RatingLabel = "Pretty good",
                    MoodDate = Today
                });

            var result = await _controller.Create(request, CancellationToken.None);

            var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            created.StatusCode.Should().Be(StatusCodes.Status201Created);
            created.Value.Should().BeOfType<MoodEntryResponse>()
                .Which.RatingLabel.Should().Be("Pretty good");
        }

        [Fact]
        public async Task Create_Returns409WithAUserFacingMessageOnASecondSubmission()
        {
            // The brief requires an error message to appear on a repeat submission. 409 is the
            // accurate status, and the detail text is what the UI shows the user.
            var request = new CreateMoodEntryRequest { Rating = MoodRating.ABitMeh };

            _moodService
                .CreateAsync(UserKey, request, Arg.Any<CancellationToken>())
                .ThrowsAsync(new DuplicateMoodEntryException(Today));

            var result = await _controller.Create(request, CancellationToken.None);

            var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);

            var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
            problem.Title.Should().Be("Mood already recorded");
            problem.Detail.Should().Contain("already recorded your mood today");
            problem.Extensions.Should().ContainKey("moodDate");
            problem.Extensions["moodDate"].Should().Be("2026-08-06");
        }

        [Fact]
        public async Task Create_IdentifiesTheCallerByTheAnonymousCookieKey()
        {
            var request = new CreateMoodEntryRequest { Rating = MoodRating.FeelingGreat };

            _moodService
                .CreateAsync(Arg.Any<string>(), request, Arg.Any<CancellationToken>())
                .Returns(new MoodEntryResponse { Id = 1, MoodDate = Today });

            await _controller.Create(request, CancellationToken.None);

            _userKeyProvider.Received(1).GetOrCreateUserKey();
            await _moodService.Received(1)
                .CreateAsync(UserKey, request, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetToday_ReturnsTheSubmissionStatusForTheCaller()
        {
            _moodService
                .GetTodayStatusAsync(UserKey, Arg.Any<CancellationToken>())
                .Returns(new TodayMoodStatusResponse
                {
                    HasSubmittedToday = true,
                    Date = Today,
                    Entry = new MoodEntryResponse { Id = 7, MoodDate = Today }
                });

            var result = await _controller.GetToday(CancellationToken.None);

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeOfType<TodayMoodStatusResponse>()
                .Which.HasSubmittedToday.Should().BeTrue();
        }

        [Fact]
        public void GetOptions_ReturnsTheMoodOptionsFromTheService()
        {
            _moodService.GetOptions().Returns(new List<MoodOptionResponse>
            {
                new() { Value = MoodRating.NotGoodAtAll, Label = "Not good at all" }
            });

            var result = _controller.GetOptions();

            var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeAssignableTo<IReadOnlyList<MoodOptionResponse>>()
                .Which.Should().HaveCount(1);
        }
    }
}
