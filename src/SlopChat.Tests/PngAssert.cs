namespace SlopChat.Tests
{
  internal static class PngAssert
  {
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(byte[] bytes) => bytes.Length > Signature.Length && bytes.AsSpan(0, Signature.Length).SequenceEqual(Signature);
  }
}
