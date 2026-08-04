# 00008. Add retry policy for Azure AI Foundry calls

2026-08-04

## Status

__New__

## Context

The application makes calls to Azure AI Foundry, which can fail temporarily because of rate limiting, service unavailability, timeouts, or network interruptions. Treating every failed call as final makes otherwise recoverable operations unreliable and exposes transient infrastructure conditions directly to users.

Retrying requests can improve resilience, but it can also increase response time, consume additional quota, and amplify load during an outage. Azure AI Foundry may provide service-specific guidance such as `Retry-After` headers, while authentication, authorization, validation, and other non-transient failures should not be retried. Any retry behavior must therefore distinguish transient failures from permanent ones, remain bounded, respect cancellation and service guidance, and provide enough logging or telemetry to diagnose repeated failures without obscuring the original error.

A consistent retry policy is needed so that Azure AI Foundry calls do not implement different retry behavior independently and so that reliability, latency, quota usage, and operational visibility can be balanced across the application.

## Decision

Implement a single, shared retry policy for all Azure AI Foundry calls. The policy will retry only transient failures, including rate limiting, service unavailability, request timeouts, and recoverable network errors, using bounded exponential backoff with jitter. Service-provided guidance such as `Retry-After` will take precedence over calculated delays. Authentication, authorization, validation, cancellation, and other non-transient failures will not be retried.

Retries will have configurable limits on attempts and total elapsed time, will honor caller cancellation, and will emit structured telemetry for each retry and the final outcome. If all attempts fail, the original failure will be preserved and surfaced with retry details. Centralizing this behavior provides consistent resilience while limiting added latency, quota consumption, and load during outages.

## Consequences

*Pro's:*
- Transient Azure AI Foundry failures are less likely to cause user-visible operation failures.
- Retry behavior is consistent across all Azure AI Foundry integrations.
- Bounded attempts and elapsed time limit worst-case latency, quota consumption, and outage amplification.
- Exponential backoff with jitter reduces synchronized retry bursts and pressure on a degraded service.
- Respecting service-provided retry guidance improves alignment with Azure capacity and throttling behavior.
- Excluding non-transient failures avoids unnecessary requests and delays.
- Caller cancellation remains responsive during retry sequences.
- Structured telemetry improves diagnosis of repeated failures and retry effectiveness.
- Preserving the original failure maintains actionable error information after retries are exhausted.
- Configurable limits allow reliability and latency trade-offs to be tuned without duplicating retry logic.

*Con's:*
- Recoverable failures may still produce noticeably longer response times before success or final failure.
- Retries consume additional Azure AI Foundry quota and may increase operating costs.
- Misclassification of failures can retry permanent errors or prematurely fail transient ones (maintain and test centralized error classification).
- A shared policy may not optimally fit every operation's latency, idempotency, or quota characteristics (allow narrowly scoped configuration overrides).
- Retry telemetry increases logging volume and observability costs (apply sampling and retention controls where appropriate).
- Policy configuration changes can affect all Azure AI Foundry calls simultaneously, increasing the blast radius of mistakes (validate settings and roll out changes gradually).
- Non-idempotent requests could repeat side effects when outcomes are ambiguous (use idempotency mechanisms or exclude unsafe operations).
