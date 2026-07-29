namespace Forkcast.Api.Ai;

/// <summary>
/// The rule that lets a language model rewrite a plan description safely.
/// </summary>
/// <remarks>
/// Plan wording is the one place generated prose reaches the screen without passing through the
/// claim verifier, because a plan is described before anything has been simulated. Rather than
/// building a second verifier for it, the model is asked for words only, and any description
/// containing a digit is rejected outright. A blunt rule is the right kind of rule here: it
/// cannot be argued with, and the cost of it firing is that the hand-written description is
/// used instead.
/// </remarks>
public static class PlanWording
{
    public static bool IsAcceptable(string? description) =>
        !string.IsNullOrWhiteSpace(description)
        && description.Length <= 400
        && !description.Any(char.IsDigit);
}
