namespace TypeSafe.Jev.Examples;

/// <summary>
/// Every failure is a JevException. Catch the specific ones you can act on and let the rest bubble up.
/// The client already retries 429s and 5xx, so seeing one here means the retries were exhausted.
/// </summary>
public static class ErrorHandling
{
    public static async Task RunAsync(IJevClient client)
    {
        try
        {
            var res = await client.SystemOneAsync("some text", new Dictionary<string, Question>
            {
                ["ok"] = Question.Noul("Is this text in English?"),
            });

            Console.WriteLine($"ok: {res.Noul("ok").Noul:P0}  (request {res.RequestId})");
        }
        catch (JevAuthenticationException)
        {
            Console.Error.WriteLine("Bad API key — check TYPESAFE_API_KEY.");
        }
        catch (JevRateLimitException ex)
        {
            // Retries are exhausted; back off at the application level.
            Console.Error.WriteLine($"Rate limited. Server suggested waiting {ex.RetryAfter?.TotalSeconds ?? 0:0}s.");
        }
        catch (JevUnprocessableEntityException ex)
        {
            // Usually a malformed question: too many choice options, fewer than two score levels, empty state.
            Console.Error.WriteLine($"The API rejected the request: {ex.Body}");
        }
        catch (JevInternalServerException ex)
        {
            Console.Error.WriteLine($"Jev is having a bad day ({(int)ex.Status}), request {ex.RequestId}. Try again later.");
        }
        catch (JevTimeoutException ex)
        {
            Console.Error.WriteLine($"No response within {ex.Timeout}. Raise JevClientOptions.Timeout for long states.");
        }
        catch (JevConnectionException ex)
        {
            Console.Error.WriteLine($"Could not reach the API: {ex.InnerException?.Message}");
        }
        catch (JevResponseValidationException ex)
        {
            // The call succeeded but the body was not what we expected — at {ex.FieldPath}.
            Console.Error.WriteLine($"Unexpected response shape at {ex.FieldPath}: {ex.Message}");
        }
    }
}
