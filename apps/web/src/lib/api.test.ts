import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "@/lib/api";

describe("API error compatibility", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("reads RFC 7807 details and safe request references", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            type: "about:blank",
            title: "An unexpected error occurred.",
            status: 500,
            detail: "The request could not be completed.",
            traceId: "trace-123",
            correlationId: "correlation-456",
          }),
          {
            status: 500,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    const error = await api.listForms("test-token").catch((caught) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      errors: ["The request could not be completed."],
      statusCode: 500,
      traceId: "trace-123",
      correlationId: "correlation-456",
    });
  });

  it("keeps validation errors ahead of ProblemDetails fields", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            title: "Validation failed.",
            status: 400,
            errors: { email: ["A valid email is required."] },
          }),
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    const error = await api.listForms("test-token").catch((caught) => caught);

    expect(error).toMatchObject({
      errors: ["A valid email is required."],
      statusCode: 400,
    });
  });
});
