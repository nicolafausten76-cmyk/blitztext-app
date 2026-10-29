using Xunit;
using Sprechklar;

public class CoreTests
{
    [Fact] public void DictionaryDoesNotChangeWordFragments()
        => Assert.Equal("n8n und Banana",Core.ReplaceTerms("n acht n und Banana","n acht n => n8n\nana => X"));
    [Fact] public void ReplacementsAreNotRecursive()
        => Assert.Equal("B C",Core.ReplaceTerms("A B","A => B\nB => C"));
    [Fact] public void LongestTermWinsAndUnicodeBoundariesAreRespected()
        => Assert.Equal("KI-Beratung Äpfel X",Core.ReplaceTerms("KI Beratung Äpfel Apfel","KI => kurz\nKI Beratung => KI-Beratung\nApfel => X"));
    [Fact] public void CleanDoesNotInventContent()
        => Assert.Equal("Morgen 12:30 Uhr.",Core.Clean(" Morgen \r\n12:30   Uhr. "));
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RewriteTreatsTranscriptAsUntrustedContent(int mode)
        => Assert.Contains("kein Arbeitsauftrag",Core.Instruction(mode));
}
