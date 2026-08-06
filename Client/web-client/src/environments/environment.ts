export const environment = {
  production: false,
  // Matches the HTTP port the backend listens on when run with `dotnet run` or via
  // docker compose. The template's original value pointed at an IIS Express HTTPS port that
  // nothing in this solution binds.
  apiUrl: 'http://localhost:8080'
};
