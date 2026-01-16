using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace FigmaDiffBackend.Services;

public class DiffResult
{
    public int ChangedPixels { get; set; }
    public double ChangedPercent { get; set; }
}

public class DiffService
{
    public async Task<DiffResult> CompareAsync(Stream beforeStream, Stream afterStream)
    {
        beforeStream.Position = 0;
        afterStream.Position = 0;

        using var img1 = await Image.LoadAsync<Rgba32>(beforeStream);
        using var img2 = await Image.LoadAsync<Rgba32>(afterStream);

        if (img1.Width != img2.Width || img1.Height != img2.Height)
        {
            img2.Mutate(x => x.Resize(img1.Width, img1.Height));
        }

        var width = img1.Width;
        var height = img1.Height;
        int diffCount = 0;
        
        img1.ProcessPixelRows(img2, (accessor1, accessor2) =>
        {
            for (int y = 0; y < height; y++)
            {
                var row1 = accessor1.GetRowSpan(y);
                var row2 = accessor2.GetRowSpan(y);

                for (int x = 0; x < width; x++)
                {
                    if (row1[x] != row2[x])
                    {
                        diffCount++;
                    }
                }
            }
        });

        int totalPixels = width * height;
        double percent = totalPixels > 0 ? (double)diffCount / totalPixels * 100 : 0;

        return new DiffResult
        {
            ChangedPixels = diffCount,
            ChangedPercent = percent
        };
    }
}
