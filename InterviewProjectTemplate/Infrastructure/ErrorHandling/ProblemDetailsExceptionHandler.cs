using InterviewProjectTemplate.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InterviewProjectTemplate
{
    /// <summary>
    /// Converts unhandled exceptions into a consistent problem-details response.
    /// <para>
    /// Two goals: the Angular client always receives the same error shape regardless of what failed,
    /// and internal exception details never reach a client in production. The message is logged in
    /// full server-side so nothing is lost for diagnosis.
    /// </para>
    /// </summary>
    internal static class ProblemDetailsExceptionHandler
    {
        public static async Task HandleAsync(HttpContext context)
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = feature?.Error;

            if (exception is null)
            {
                return;
            }

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(ProblemDetailsExceptionHandler));

            logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            var (statusCode, title, detail) = Describe(exception, context);

            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            });
        }

        private static (int StatusCode, string Title, string Detail) Describe(
            Exception exception,
            HttpContext context)
        {
            var isDevelopment = context.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .IsDevelopment();

            return exception switch
            {
                // A duplicate reaching here means a code path did not translate it; still report it
                // accurately rather than as a server fault.
                DuplicateMoodEntryException duplicate => (
                    StatusCodes.Status409Conflict,
                    "Mood already recorded",
                    "You have already recorded your mood today. Please come back tomorrow."),

                // A cancelled request is the client's doing, not a server fault. 499 is nginx's
                // convention for it and keeps these out of genuine 5xx error rates.
                OperationCanceledException => (
                    499,
                    "Request cancelled",
                    "The request was cancelled before it completed."),

                ArgumentException argument => (
                    StatusCodes.Status400BadRequest,
                    "Invalid request",
                    argument.Message),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred",
                    // Only surface the real message outside production, where it aids debugging.
                    isDevelopment
                        ? exception.Message
                        : "Something went wrong while processing your request. Please try again.")
            };
        }
    }
}
