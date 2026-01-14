using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace FigmaDiffBackend.Services;

public class DiffResult
{
    public int ChangedPixels { get; set; }
    public double ChangedPercent { get; set; }
    public Stream? DiffImage { get; set; }
}

public class DiffService
{
    public async Task<DiffResult> CompareAsync(Stream beforeStream, Stream afterStream)
    {
        // Reset streams if needed
        beforeStream.Position = 0;
        afterStream.Position = 0;

        using var img1 = await Image.LoadAsync<Rgba32>(beforeStream);
        using var img2 = await Image.LoadAsync<Rgba32>(afterStream);

        // Ensure same size (naive resizing for MVP, or just crop/expand)
        if (img1.Width != img2.Width || img1.Height != img2.Height)
        {
            // For MVP: Resize img2 to match img1
            img2.Mutate(x => x.Resize(img1.Width, img1.Height));
        }

        var width = img1.Width;
        var height = img1.Height;
        var diffImg = new Image<Rgba32>(width, height);
        
        int diffCount = 0;
        var highlightColor = Color.Red.ToPixel<Rgba32>();
        
        // Parallel pixel processing? For simplicity use process pixel rows
        img1.ProcessPixelRows(img2, diffImg, (accessor1, accessor2, accessorDiff) =>
        {
            for (int y = 0; y < height; y++)
            {
                var row1 = accessor1.GetRowSpan(y);
                var row2 = accessor2.GetRowSpan(y);
                var rowDiff = accessorDiff.GetRowSpan(y);

                for (int x = 0; x < width; x++)
                {
                    if (row1[x] != row2[x])
                    {
                        diffCount++;
                        // Draw diff on heat map
                        // Basic: Show Red on diff, semi-transparent original otherwise?
                        // Or Just Red against transparent?
                        // Let's do: Diff pixels are Red, Matching pixels are low-opacity original
                        
                        rowDiff[x] = highlightColor;
                    }
                    else
                    {
                        // Faint background to define context
                        var p = row1[x];
                        p.A = 50; // Low Alpha
                        rowDiff[x] = p;
                    }
                }
            }
        });

        int totalPixels = width * height;
        double percent = totalPixels > 0 ? (double)diffCount / totalPixels * 100 : 0;

        var memoryStream = new MemoryStream();
        await diffImg.SaveAsPngAsync(memoryStream);
        memoryStream.Position = 0;

        return new DiffResult
        {
            ChangedPixels = diffCount,
            ChangedPercent = percent,
            DiffImage = memoryStream
        };
    }
}
