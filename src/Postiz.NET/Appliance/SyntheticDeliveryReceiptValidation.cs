using System.Text.Json;

namespace Postiz.Appliance;

internal static class SyntheticDeliveryReceiptValidation
{
    internal static bool SyntheticIdentifier(string? value) => value is { Length: >= 1 and <= 128 }
        && AsciiLetterOrDigit(value[0])
        && value.All(c => AsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-');

    private static bool AsciiLetterOrDigit(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool Hash64(string? value) => value is { Length: 64 }
        && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');

    internal static void ValidateSyntheticReceipt(
        SyntheticDeliveryReceipt? receipt, string organizationId, string postId, string occurrenceId)
    {
        if (receipt is null || receipt.Contract != SyntheticDeliveryContract.Contract || receipt.Mode != SyntheticDeliveryContract.Mode
            || receipt.Status is not (SyntheticDeliveryStatuses.NotStarted or SyntheticDeliveryStatuses.Partial or SyntheticDeliveryStatuses.SimulatedPublished)
            || receipt.OrganizationId != organizationId || receipt.PostId != postId || receipt.OccurrenceId != occurrenceId
            || !SyntheticIdentifier(receipt.IntegrationId) || receipt.Slides is null || receipt.Events is null
            || receipt.Slides.Count > 20 || receipt.Events.Count > 1000)
            throw new JsonException("Invalid current synthetic delivery receipt contract or scope.");
        var hasJournal = receipt.Slides.Count > 0 || receipt.Events.Count > 0;
        if (hasJournal ? !Hash64(receipt.BodyHash) : receipt.BodyHash is not null)
            throw new JsonException("Invalid synthetic delivery journal hash.");
        for (var index = 0; index < receipt.Slides.Count; index++)
        {
            var slide = receipt.Slides[index];
            if (slide is null || slide.SlideIndex != index || slide.Status != "SimulatedCommitted"
                || slide.SyntheticReceiptId is not { Length: 74 }
                || !slide.SyntheticReceiptId.StartsWith("synthetic_", StringComparison.Ordinal)
                || !Hash64(slide.SyntheticReceiptId[10..]))
                throw new JsonException("Invalid synthetic slide receipt.");
        }
        for (var index = 0; index < receipt.Events.Count; index++)
        {
            var entry = receipt.Events[index];
            if (entry is null || entry.Ordinal != index + 1
                || entry.Kind is not ("Attempted" or "TransientRejected" or "SlideCommitted" or "ResponseLost" or "Completed"))
                throw new JsonException("Invalid synthetic delivery event journal.");
        }
        if (receipt.Status == SyntheticDeliveryStatuses.NotStarted && receipt.Slides.Count != 0
            || (receipt.Status is SyntheticDeliveryStatuses.Partial or SyntheticDeliveryStatuses.SimulatedPublished) && receipt.Slides.Count == 0
            || receipt.Status == SyntheticDeliveryStatuses.SimulatedPublished && !receipt.Events.Any(item => item.Kind == "Completed"))
            throw new JsonException("Invalid synthetic delivery completion state.");
    }

}
