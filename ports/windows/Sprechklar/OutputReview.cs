using System;
namespace Sprechklar;

public sealed record ApprovedInsertion(string Text, IntPtr Target, string Field, DateTimeOffset Expires);
public sealed class OutputReview
{
    private ApprovedInsertion? approved;
    public void Invalidate() => approved = null;
    public void Approve(string text, IntPtr target, string? field, DateTimeOffset now)
    {
        Invalidate();
        if (string.IsNullOrWhiteSpace(text) || target == IntPtr.Zero || field == null)
            throw new InvalidOperationException("Kein gültiges ursprüngliches Zielfeld. Text bei Bedarf manuell kopieren.");
        approved = new(text, target, field, now.AddSeconds(60));
    }
    // Consume BEFORE insertion, including failed attempts. Editing/new recordings revoke approval.
    public ApprovedInsertion? Take(string currentText, DateTimeOffset now)
    {
        var value = approved; approved = null;
        return value != null && value.Text == currentText && now <= value.Expires ? value : null;
    }
}
