using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>
/// PdfPig's stream filters, with Flate decoding refused for a stream that would decompress past
/// <see cref="MaxDecodedSize"/>. PdfPig decodes a stream whole into memory, so a small stream that expands to gigabytes
/// (a decompression bomb) would exhaust it. A refused stream throws <see cref="StreamTooLargeException"/>.
/// </summary>
internal sealed class CappedFilterProvider : IFilterProvider
{
    public const long MaxDecodedSize = 256L * 1024 * 1024;

    public static readonly CappedFilterProvider Instance = new();

    private static readonly IFilterProvider Default = DefaultFilterProvider.Instance;

    public IReadOnlyList<IFilter> GetFilters(DictionaryToken dictionary) => Capped(Default.GetFilters(dictionary));

    public IReadOnlyList<IFilter> GetNamedFilters(IReadOnlyList<NameToken> names) => Capped(Default.GetNamedFilters(names));

    public IReadOnlyList<IFilter> GetAllFilters() => Capped(Default.GetAllFilters());

    private static IReadOnlyList<IFilter> Capped(IReadOnlyList<IFilter> filters) =>
        filters.Select(filter => filter is FlateFilter flate ? new CappedFlateFilter(flate) : filter).ToList();

    /// <summary>A Flate stream that would decompress to more than <see cref="MaxDecodedSize"/> bytes.</summary>
    public sealed class StreamTooLargeException()
        : Exception($"A stream decompresses to more than {MaxDecodedSize} bytes, so it wasn't read.");

    private sealed class CappedFlateFilter(FlateFilter flate) : IFilter
    {
        public bool IsSupported => flate.IsSupported;

        public Memory<byte> Decode(Memory<byte> input, DictionaryToken streamDictionary, IFilterProvider filterProvider,
            int filterIndex)
        {
            if (DecodedSizeExceedsCap(input)) throw new StreamTooLargeException();
            return flate.Decode(input, streamDictionary, filterProvider, filterIndex);
        }

        // Inflated into a pooled buffer, so measuring a bomb costs time but no memory. Data that doesn't inflate is
        // left to PdfPig, which reports it as it always has.
        private static bool DecodedSizeExceedsCap(Memory<byte> input)
        {
            if (input.Length < 2) return false;

            // Like PdfPig, skip the two-byte zlib header and inflate the raw deflate data after it.
            var bytes = MemoryMarshal.TryGetArray<byte>(input, out var array)
                ? array
                : new ArraySegment<byte>(input.ToArray());
            using var compressed = new MemoryStream(bytes.Array!, bytes.Offset + 2, bytes.Count - 2, writable: false);
            using var inflate = new DeflateStream(compressed, CompressionMode.Decompress);
            var buffer = ArrayPool<byte>.Shared.Rent(16384);
            try
            {
                long total = 0;
                int read;
                while ((read = inflate.Read(buffer)) > 0)
                {
                    total += read;
                    if (total > MaxDecodedSize) return true;
                }

                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
