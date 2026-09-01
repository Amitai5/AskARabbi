namespace AskARabbiLIB.AI.Tools;

/// <summary>Contains exact calculated text that can be cited and deterministically validated.</summary>
/// <param name="CanonicalReference">Human-readable calculation reference.</param>
/// <param name="ExactText">Exact result text exposed to the model and validator.</param>
public sealed record AIToolEvidence(string CanonicalReference, string ExactText);
