# URL Import Security Review

## Executive summary

The URL import feature has a confirmed server-side request forgery (SSRF) exposure. An active user can submit an arbitrary absolute HTTP(S) URL, and the API fetches it without restricting the destination. Automatic redirects are not revalidated, so the fetch can potentially reach internal services. The imported response content is returned to the caller, which creates a potential data-exposure path.

The review also identified unbounded response buffering and missing enforcement of the remote response content type. These create resource-exhaustion and content-validation risks, respectively.

**Decision status: Not yet fixed.** This report records the assessment; no application code was changed.

## Reviewed components

- `RagPlatform.Api/Services/WebContentService.cs` — URL validation, HTTP request, response reading and parsing.
- `RagPlatform.Api/Controllers/WebContentController.cs` — URL import endpoint and response behavior.
- `RagPlatform.Api/Program.cs` — named HTTP client registration, timeout and request headers.
- Related tests — no directly related WebContent import tests were found in the test project.

## Findings

### 1. SSRF through user-controlled URLs and redirects

**Severity: Medium**  
**Confidence: 9/10**  
**Evidence:** `WebContentService.cs:41–63`; `WebContentController.cs:9–11, 29–42`; HTTP client registration in `Program.cs:190–193`.

The service accepts an absolute URL using HTTP or HTTPS but does not restrict hostnames or resolved IP addresses. The HTTP client follows redirects without application-level destination revalidation. The content fetched is parsed, assigned to the caller's document, and returned by the endpoint.

**Attack scenario:** An active user submits a URL pointing to a loopback, private-network, link-local, or otherwise internal service. Alternatively, the user supplies a public URL that redirects to an internal destination. If that service returns readable content, the API may return that content to the importing user. This can expose internal service data and enable server-side access to destinations unavailable to the user directly.

### 2. Unbounded remote response buffering

**Severity: Medium**  
**Confidence: 9/10**  
**Evidence:** `WebContentService.cs:57`; timeout and HTTP client setup in `Program.cs:190–193`.

The response is read into memory without an explicit maximum byte limit. The configured request timeout does not bound the response size.

**Attack scenario:** A remote server returns a very large body, potentially slowly within the timeout. Repeated imports can consume excessive memory and processing capacity, affecting API availability.

### 3. Response content type is not enforced

**Severity: Low**  
**Confidence: 8/10**  
**Evidence:** `Program.cs:190–193`; `WebContentService.cs:51–58`.

The client advertises acceptable HTML media types, but the service does not verify the actual response content type before reading and parsing the body.

**Attack scenario:** A remote endpoint returns a body in an unexpected format or content type. The service still processes it as web content, which can cause incorrect parsing or unnecessary resource use. This is a validation weakness; the reviewed evidence does not establish a separate exploitable code-execution path.

## Recommended mitigations

1. **Destination and redirect validation:** Restrict fetches to an explicit host allowlist where practical. Otherwise, reject loopback, private, link-local, and other non-public addresses. Enforce validation against the address actually used for the connection to reduce DNS-rebinding risk. Disable automatic redirects or follow redirects manually and apply the same destination checks to every hop.
2. **Response-size limit:** Stream the response and enforce a strict maximum byte count before parsing. Reject oversized responses and release resources promptly.
3. **Content-type enforcement:** Validate the response's actual media type against the supported set before reading or parsing the body. Treat an unsupported type as an explicit import failure.
4. **Regression coverage:** Add tests for disallowed destinations, redirects to disallowed destinations, oversized bodies, and unsupported response content types. Include DNS/address validation behavior in a testable boundary appropriate to the implementation.

## Implementation priority

| Priority | Mitigation | Value | Effort |
|---|---|---:|---:|
| 1 | Destination checks at connection time and validation of every redirect hop | Very high | Moderate |
| 2 | Streaming response-size cap | High | Moderate |
| 3 | Enforce allowed response content types | Moderate | Low |
| 4 | Add regression tests for all hardened behaviors | High | Moderate |

## Decision status

**Implemented; runtime tests pending.** The URL import path now validates destinations and redirects, enforces a 5 MiB streamed response limit, and accepts only `text/html`, `application/xhtml+xml`, and `application/xml`. A missing `Content-Type` is explicitly rejected. The dedicated client bypasses system proxies so the connection-time destination check applies to the origin; environments requiring an outbound proxy must provide equivalent network-level restrictions or will need a proxy-compatible validated egress path. The API and test projects build successfully. Focused test execution is still pending because Smart App Control blocked test assembly loading on the local workstation. The original findings above describe the state at the time of the assessment.
