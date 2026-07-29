using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forkcast.Api.Diagnostics;
using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Api.Ai;

/// <summary>
/// Azure OpenAI at the language boundary: reading incident text, wording the plans, and writing
/// the executive explanation.
/// </summary>
/// <remarks>
/// <para>
/// Three things keep this safe. The model is only ever asked for structure or for words. Its
/// output is validated before it is used, and rejected output falls back to the deterministic
/// provider rather than propagating. And no call it makes is on the critical path: every method
/// here degrades to <see cref="DeterministicIntelligence"/>, so an outage, a rate limit or a
/// malformed response costs polish, never an answer.
/// </para>
/// <para>
/// The executive summary it writes is handed the verified claims as input and is checked against
/// them afterwards by <see cref="Core.Verification.ClaimVerifier"/>. It is not trusted to have
/// followed instructions.
/// </para>
/// </remarks>
public sealed class AzureOpenAiIntelligence(
    HttpClient client,
    AzureOpenAiOptions options,
    DeterministicIntelligence fallback,
    ILogger<AzureOpenAiIntelligence> logger) : IIncidentIntelligence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ProviderName => "Azure OpenAI";

    public bool IsLive => true;

    public async Task<ExtractionResult> ExtractAsync(
        string narrative,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(narrative);

        const string system =
            """
            You transcribe operational incident reports into a fixed JSON schema.

            Return ONLY this object, using null for anything the report does not state:
            {
              "title": string|null,
              "site": string|null,
              "detectedAtLocalTime": "HH:mm"|null,
              "deadlineLocalTime": "HH:mm"|null,
              "vehicleCount": integer|null,
              "operationalChargePointCount": integer|null,
              "failedChargePointCount": integer|null,
              "priorityVehicleCount": integer|null,
              "minInitialStateOfChargePct": number|null,
              "maxInitialStateOfChargePct": number|null,
              "failures": [string]
            }

            Rules. Transcribe only what the report states; never estimate a missing figure.
            operationalChargePointCount is what still works, not what failed. vehicleCount is the
            fleet that must depart, not the subset on priority routes. Every "failures" entry
            must be a short phrase drawn from the report.
            """;

        var draft = await CompleteJsonAsync<IncidentDraft>(system, narrative, 700, cancellationToken);
        if (draft is null)
        {
            return await fallback.ExtractAsync(narrative, cancellationToken);
        }

        return new ExtractionResult
        {
            Draft = draft with { Failures = draft.Failures ?? [] },
            Source = "azure-openai",
            Notes = ["Incident read by Azure OpenAI. Every figure below is still simulated locally."]
        };
    }

    public async Task<IReadOnlyList<PlanNarrative>> DescribePlansAsync(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(plans);

        const string system =
            """
            You write one-sentence descriptions of operational response plans for a duty manager.

            Return ONLY: {"plans":[{"planId":string,"description":string}]}

            Rules. Describe what the yard team will DO, in plain words. Use NO digits and NO
            numerals of any kind: not counts, not costs, not power ratings, not times. A
            description containing a digit will be discarded. Keep each under thirty words.
            """;

        var request = JsonSerializer.Serialize(
            new
            {
                incident = new { incident.Title, incident.Site },
                plans = plans.Select(p => new { planId = p.Id, p.Name, p.Actions })
            },
            Json);

        var response = await CompleteJsonAsync<PlanDescriptions>(system, request, 500, cancellationToken);
        if (response?.Plans is null)
        {
            return await fallback.DescribePlansAsync(incident, plans, cancellationToken);
        }

        var wording = response.Plans.ToDictionary(p => p.PlanId ?? string.Empty, StringComparer.Ordinal);

        return plans
            .Select(plan =>
            {
                var candidate = wording.GetValueOrDefault(plan.Id)?.Description;
                return new PlanNarrative
                {
                    PlanId = plan.Id,
                    Name = plan.Name,
                    Description = PlanWording.IsAcceptable(candidate) ? candidate! : plan.Description
                };
            })
            .ToList();
    }

    public async Task<string?> WriteExecutiveSummaryAsync(
        ExecutiveSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        const string system =
            """
            You write the executive paragraph beneath an operational recommendation.

            Return ONLY: {"summary": string}

            Rules, and they are enforced downstream. You may use ONLY the figures supplied in
            "claims", copied exactly as their displayValue reads. You may also refer to the seed
            and the trial count. Any other number causes the whole paragraph to be discarded and
            replaced, so if you are unsure of a figure, leave it out and write around it.
            Do not compute, combine, convert or round anything. Three or four sentences, plain
            British English, addressed to a manager deciding in the next ten minutes.
            """;

        var payload = JsonSerializer.Serialize(
            new
            {
                incident = request.IncidentTitle,
                recommendation = request.RecommendedHeadline,
                plan = request.RecommendedPlanName,
                decisionRule = request.DecisionRule,
                criticalConstraint = request.CriticalConstraint,
                seed = request.Seed,
                trialCount = request.TrialCount,
                claims = request.Claims.Select(c => new
                {
                    c.Id,
                    c.Label,
                    displayValue = c.DisplayValue,
                    c.Unit
                })
            },
            Json);

        var response = await CompleteJsonAsync<SummaryResponse>(system, payload, 450, cancellationToken);
        return string.IsNullOrWhiteSpace(response?.Summary) ? null : response.Summary.Trim();
    }

    public async Task<AssumptionOverride> InterpretChallengeAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);

        const string system =
            """
            You classify a what-if question about an overnight depot charging plan into exactly
            one supported lever, or into none.

            Return ONLY: {"kind": string, "value": number}

            kind must be one of:
              "BufferArrivalDelayMinutes"   value = minutes late
              "BufferUnavailable"           value = 0
              "AdditionalChargePointOutage" value = how many more go offline
              "DeadlineEarlierMinutes"      value = minutes earlier
              "FastChargerRepaired"         value = 1
              "None"                        value = 0

            Choose "None" whenever the question is off-topic, ambiguous, or asks about something
            outside this list. Reporting that a question is unsupported is a correct answer;
            guessing at one is not.
            """;

        var response = await CompleteJsonAsync<ChallengeClassification>(
            system, question, 120, cancellationToken);

        if (response?.Kind is null
            || !Enum.TryParse<AssumptionKind>(response.Kind, ignoreCase: true, out var kind)
            || kind == AssumptionKind.None)
        {
            // The deterministic matcher is a genuine second opinion here, not just a fallback:
            // it catches the phrasings the model shrugs at.
            return await fallback.InterpretChallengeAsync(question, cancellationToken);
        }

        return new AssumptionOverride
        {
            Kind = kind,
            Value = Math.Clamp(response.Value, 0, 1440),
            Label = Describe(kind, response.Value),
            Question = question
        };
    }

    private static string Describe(AssumptionKind kind, double value) => kind switch
    {
        AssumptionKind.BufferArrivalDelayMinutes =>
            $"The temporary battery buffer arrives {Duration(value)} late",
        AssumptionKind.BufferUnavailable => "The temporary battery buffer cannot be sourced",
        AssumptionKind.AdditionalChargePointOutage =>
            $"A further {value:0} charge point{(Math.Abs(value - 1) < 0.5 ? "" : "s")} goes offline",
        AssumptionKind.DeadlineEarlierMinutes =>
            $"Every departure is brought forward by {Duration(value)}",
        AssumptionKind.FastChargerRepaired => "The fast charger is repaired during the night",
        _ => "No supported assumption was recognised in this question"
    };

    private static string Duration(double minutes) => minutes switch
    {
        60 => "one hour",
        120 => "two hours",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{minutes:0} minutes"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60.0:0.#} hours")
    };

    /// <summary>
    /// One chat completion in JSON mode at temperature zero. Returns null on any failure, which
    /// every caller treats as "use the deterministic path".
    /// </summary>
    private async Task<T?> CompleteJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

            using var response = await client.PostAsJsonAsync(
                options.ChatCompletionsUri,
                new
                {
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0,
                    top_p = 1,
                    max_tokens = maxTokens,
                    response_format = new { type = "json_object" }
                },
                Json,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                Log.AzureOpenAiFallback(
                    logger,
                    new HttpRequestException($"Azure OpenAI returned {(int)response.StatusCode}."));
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(Json, timeout.Token);
            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            return string.IsNullOrWhiteSpace(content) ? null : JsonSerializer.Deserialize<T>(content, Json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.AzureOpenAiFallback(logger, exception);
            return null;
        }
    }

    private sealed record ChatCompletion([property: JsonPropertyName("choices")] Choice[]? Choices);

    private sealed record Choice([property: JsonPropertyName("message")] ChoiceMessage? Message);

    private sealed record ChoiceMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record SummaryResponse(string? Summary);

    private sealed record ChallengeClassification(string? Kind, double Value);

    private sealed record PlanDescriptions(PlanDescription[]? Plans);

    private sealed record PlanDescription(string? PlanId, string? Description);
}
