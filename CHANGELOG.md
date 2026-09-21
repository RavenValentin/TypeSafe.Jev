# Changelog

All notable changes to this project. Versions follow [semantic versioning](https://semver.org).

## 0.1.0 — 2026-09-21

First release.

- `JevClient` over `HttpClient`: `POST /v1/systemone` and `GET /v1/models`, bearer auth, protected headers.
- Three question types — `Noul`, `Choice`, `Score` — plus `RawQuestion` for shapes the API gains first.
- `Question.Choice<TEnum>()` builds the option list from an enum, with `[Description]` as each rubric;
  `ChoiceAnswer.As<TEnum>()` maps the answer back.
- `JsonContent` for everywhere the API takes text, an object or an array; nodes are deep-cloned, so one
  rubric can be shared across questions and a question can be sent twice.
- Typed responses: derive from `SystemOneResponse`, declare answer properties, mark the spare ones
  `[OptionalAnswer]`.
- `RetryPolicy` with the official SDKs' defaults — 2 retries on 408/429/5xx, connection and timeout
  failures, 0.5s–5s backoff with jitter, `Retry-After` honoured, and a 30s budget for the whole call.
- Per-call `JevRequestOptions`: model, retry policy, timeout, extra headers and extra body fields.
- Exception hierarchy mirroring the official SDKs, each carrying status, body, request id and endpoint.
- Logging through `ILogger` with credentials redacted, and OpenTelemetry traces and metrics through
  `ActivitySource` and `Meter` — no package reference for either.
- `net8.0`, `net9.0` and `net10.0`; trim- and Native AOT-safe, proven by a native binary run in CI.
- Fifteen examples that run offline against a local fake, with no API key.
