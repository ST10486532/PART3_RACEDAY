namespace RaceDay.Api.Services;

/// <summary>An uploaded image that passed validation. The caller owns (and must dispose) Content.</summary>
public record ValidatedImage(MemoryStream Content, string ContentType, string Extension);

/// <summary>Checks uploads by size and by file signature (not just the file name or content type).</summary>
public static class ImageValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;           // 5 MB per image
    public const long MaxRequestBytes = 6 * 1024 * 1024;    // allows for multipart overhead

    public static async Task<(ValidatedImage? Image, string? Error)> ValidateAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (null, "Please choose an image file.");
        if (file.Length > MaxBytes)
            return (null, "The image must be 5 MB or smaller.");

        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;

        var header = new byte[12];
        var read = await buffer.ReadAsync(header, 0, header.Length);
        buffer.Position = 0;

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (new ValidatedImage(buffer, "image/jpeg", ".jpg"), null);

        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return (new ValidatedImage(buffer, "image/png", ".png"), null);

        if (read >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return (new ValidatedImage(buffer, "image/gif", ".gif"), null);

        if (read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return (new ValidatedImage(buffer, "image/webp", ".webp"), null);

        await buffer.DisposeAsync();
        return (null, "Only JPG, PNG, GIF or WebP images are allowed.");
    }
}
//ST10486532
//Author: Gift Tshwaane