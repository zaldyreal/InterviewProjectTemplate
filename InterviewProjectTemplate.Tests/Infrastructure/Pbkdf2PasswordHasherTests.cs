using FluentAssertions;
using InterviewProjectTemplate.Infrastructure.Security;

namespace InterviewProjectTemplate.Tests.Infrastructure
{
    public class Pbkdf2PasswordHasherTests
    {
        private readonly Pbkdf2PasswordHasher _hasher = new();

        [Fact]
        public void Verify_AcceptsTheCorrectPassword()
        {
            var hash = _hasher.Hash("CorrectHorseBattery9!");

            _hasher.Verify("CorrectHorseBattery9!", hash).Should().BeTrue();
        }

        [Theory]
        [InlineData("wrong-password")]
        [InlineData("CorrectHorseBattery9")]
        [InlineData("correcthorsebattery9!")]
        public void Verify_RejectsAnIncorrectPassword(string attempt)
        {
            var hash = _hasher.Hash("CorrectHorseBattery9!");

            _hasher.Verify(attempt, hash).Should().BeFalse();
        }

        [Fact]
        public void Hash_NeverStoresThePasswordInRecoverableForm()
        {
            const string password = "CorrectHorseBattery9!";

            var hash = _hasher.Hash(password);

            hash.Should().NotContain(password);
        }

        [Fact]
        public void Hash_ProducesADifferentValueEachTimeForTheSamePassword()
        {
            // Distinct salts mean two admins sharing a password do not share a hash, and a precomputed
            // rainbow table cannot be applied across accounts.
            var first = _hasher.Hash("SamePassword1!");
            var second = _hasher.Hash("SamePassword1!");

            first.Should().NotBe(second);
        }

        [Fact]
        public void Hash_RecordsTheIterationCountSoTheWorkFactorCanBeRaisedLater()
        {
            var hash = _hasher.Hash("SamePassword1!");

            var segments = hash.Split('.');

            segments.Should().HaveCount(3);
            int.Parse(segments[0]).Should().BeGreaterThan(100_000);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-hash")]
        [InlineData("abc.def.ghi")]
        [InlineData("0.c2FsdA==.a2V5")]
        [InlineData("210000.!!!not-base64!!!.a2V5")]
        public void Verify_ReturnsFalseForAMalformedStoredHashRatherThanThrowing(string storedHash)
        {
            // Corrupt data must fail closed. Throwing here would turn a bad row into a 500 and would
            // let a caller distinguish corrupt storage from a wrong password.
            _hasher.Verify("any-password", storedHash).Should().BeFalse();
        }

        [Fact]
        public void Verify_ReturnsFalseForAnEmptyPassword()
        {
            var hash = _hasher.Hash("CorrectHorseBattery9!");

            _hasher.Verify(string.Empty, hash).Should().BeFalse();
        }

        [Fact]
        public void Hash_RejectsAnEmptyPassword()
        {
            var act = () => _hasher.Hash(string.Empty);

            act.Should().Throw<ArgumentException>();
        }
    }
}
