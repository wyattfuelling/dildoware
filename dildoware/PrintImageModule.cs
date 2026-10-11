
using Discord;
using Discord.Interactions;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class PrintImageModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly HttpClient Http = new HttpClient();

    [SlashCommand("printimg", "Print an image from a direct URL")]
    public async Task PrintImageFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp))
        {
            await RespondAsync(
                "Please provide a valid HTTP or HTTPS image URL.",
                ephemeral: true);
            return;
        }

        // Defer while the image downloads and prints.
        await DeferAsync(ephemeral: true);

        string? imagePath = null;

        try
        {
            using var response = await Http.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            string? contentType =
                response.Content.Headers.ContentType?.MediaType;

            if (contentType == null ||
                !contentType.StartsWith(
                    "image/", StringComparison.OrdinalIgnoreCase))
            {
                await FollowupAsync(
                    "That URL did not return an image.",
                    ephemeral: true);
                return;
            }

            string extension = contentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/bmp" => ".bmp",
                "image/gif" => ".gif",
                _ => throw new InvalidOperationException(
                    "Unsupported image format. Please use PNG, JPEG, BMP, or GIF.")
            };

            imagePath = Path.Combine(
                Path.GetTempPath(),
                $"discord_print_{Guid.NewGuid()}{extension}");

            await using (var file = File.Create(imagePath))
            {
                await response.Content.CopyToAsync(file);
            }

            await Task.Run(() => Program.PrintImage(imagePath));

            await FollowupAsync(
                "Image print job submitted successfully!",
                ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Image URL printing failed: {ex}");

            await FollowupAsync(
                $"Could not print the image: {ex.Message}",
                ephemeral: true);
        }
        finally
        {
            if (imagePath != null)
            {
                try
                {
                    if (File.Exists(imagePath))
                        File.Delete(imagePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Temporary file cleanup failed: {ex.Message}");
                }
            }
        }
    }
}
