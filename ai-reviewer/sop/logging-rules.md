# Logging Rules

- Use structured logging with named placeholders (e.g., `{UserId}` not string interpolation).
- Log at appropriate levels: Information for flow, Warning for recoverable issues, Error for failures.
- Every public service method must log entry and exit at Information level.
- Exception logging must include the full exception object.
- Do not log sensitive data such as tokens, passwords, or PII.
- Include correlation IDs in all log entries for distributed tracing.
