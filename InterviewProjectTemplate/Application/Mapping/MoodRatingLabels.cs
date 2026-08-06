using InterviewProjectTemplate.Domain.Enums;

namespace InterviewProjectTemplate.Application.Mapping
{
    /// <summary>
    /// The exact display labels required by the brief. Held server-side and served over the API so
    /// that the wording exists in exactly one place; the Angular client renders whatever it is given
    /// rather than keeping its own copy that could drift.
    /// </summary>
    public static class MoodRatingLabels
    {
        private static readonly IReadOnlyDictionary<MoodRating, string> Labels =
            new Dictionary<MoodRating, string>
            {
                [MoodRating.NotGoodAtAll] = "Not good at all",
                [MoodRating.ABitMeh] = "A bit “meh”",
                [MoodRating.PrettyGood] = "Pretty good",
                [MoodRating.FeelingGreat] = "Feeling great"
            };

        /// <summary>All options in worst-to-best order, matching the order given in the brief.</summary>
        public static IReadOnlyList<MoodRating> AllInDisplayOrder { get; } = new[]
        {
            MoodRating.NotGoodAtAll,
            MoodRating.ABitMeh,
            MoodRating.PrettyGood,
            MoodRating.FeelingGreat
        };

        public static string GetLabel(MoodRating rating) =>
            Labels.TryGetValue(rating, out var label) ? label : rating.ToString();
    }
}