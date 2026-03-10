# Security Rules

- Never log secrets, tokens, or API keys.
- All external input must be validated before processing.
- Use parameterized queries for any database operations. Note: Entity Framework Core LINQ queries (e.g., FindAsync, FirstOrDefaultAsync, Where) are inherently parameterized and should not be flagged. Only flag raw SQL usage (FromSqlRaw, ExecuteSqlRaw) that does not use parameters.
- Secrets must come from configuration or environment variables, never hardcoded.
- All HTTP endpoints must validate Content-Type headers.
- Implement rate limiting on public-facing endpoints.
- Sanitize all user-provided strings before including in logs or responses.
