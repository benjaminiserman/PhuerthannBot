namespace PhuerthannBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

public class Program
{
    private static DiscordSocketClient Client { get; set; }

    private static YouTubeService Service { get; set; }

    private const string CONFIG_PATH = @"config.json";

    private const string COMMAND = "!phuerthann init";

    private static Config BotConfig { get; set; }

    private static Timer Timer { get; set; }

    private static HashSet<string> SendQueue { get; set; } = new();

    private static DateTime LastUpdate { get; set; }

    private static bool ShouldSend { get; set; } = false;

    private static string PlaylistID { get; set; }

    public static void Main() => MainAsync().GetAwaiter().GetResult();

    public static async Task MainAsync()
    {
        if (!File.Exists(CONFIG_PATH))
        {
            BotConfig = new Config();
            File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(BotConfig));
        }
        else BotConfig = JsonSerializer.Deserialize<Config>(File.ReadAllText(CONFIG_PATH));

        Config TestConfig = new();
        if (BotConfig.DiscordLoginToken == TestConfig.DiscordLoginToken ||
            BotConfig.YoutubeLoginToken == TestConfig.YoutubeLoginToken ||
            BotConfig.ChannelID == TestConfig.ChannelID)
        {
            Console.WriteLine("Config file not completed!");
            throw new Exception("Config file not complete!");
        }

        if (BotConfig.UpdateInterval < 5)
        {
            Console.WriteLine("Update Interval must be at least 5 minutes!");
            throw new Exception("Update Interval must be at least 5 minutes!");
        }

        BotConfig.ServerChannels ??= new();

        Service = new(new BaseClientService.Initializer()
        {
            ApiKey = BotConfig.YoutubeLoginToken,
            ApplicationName = "Phuerthann Bot"
        });

        try
        {
            var request = Service.Channels.List("contentDetails");
            request.Id = BotConfig.ChannelID;
            var channelResponse = await request.ExecuteAsync();
            Channel channel = channelResponse.Items.First();
            PlaylistID = channel.ContentDetails.RelatedPlaylists.Uploads;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Channel {BotConfig.ChannelID} not found!");
            Console.WriteLine(ex.Message);
            throw;
        }

        LastUpdate = DateTime.UtcNow;

        Client = new DiscordSocketClient();

        Client.MessageReceived += SlashCommandHandler;
        Client.GuildAvailable += Online;
        Client.Log += msg =>
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, BotConfig.DiscordLoginToken);
        await Client.SetGameAsync($"listening to {BotConfig.BandName}'s newest album!");
        await Client.StartAsync();

        Timer = new(60_000 * BotConfig.UpdateInterval);
        Timer.Elapsed += async (_, _) =>
        {
            if (Client.ConnectionState == ConnectionState.Connected)
            {
                HashSet<string> playlist = await GetUploads();

                if (playlist.Any())
                {
                    ShouldSend = true;
                    foreach (var x in playlist) SendQueue.Add(x);
                }
                else if (ShouldSend)
                {
                    await SendAnnouncement();
                    ShouldSend = false;
                }
            }
        };

        Timer.AutoReset = true;
        Timer.Enabled = true;

        await Task.Delay(-1);
    }

    private static void Timer_Elapsed(object sender, ElapsedEventArgs e) => throw new NotImplementedException();

    private static async Task SlashCommandHandler(SocketMessage command)
    {
        if (command.Content == COMMAND)
        {
            ulong guildID = ((SocketGuildChannel)command.Channel).Guild.Id;
            if (BotConfig.ServerChannels.ContainsKey(guildID))
            {
                BotConfig.ServerChannels[guildID] = command.Channel.Id;
            }
            else
            {
                BotConfig.ServerChannels.Add(guildID, command.Channel.Id);
            }

            await command.Channel.SendMessageAsync("Rebase complete.", messageReference: new MessageReference(command.Id));

            File.WriteAllText("config.json", JsonSerializer.Serialize(BotConfig));
        }
    }

    private static async Task Online(SocketGuild guild)
    {
        if (!BotConfig.ServerChannels.ContainsKey(guild.Id))
        {
            await guild.DefaultChannel.SendMessageAsync($"Hello! To enable me, use the `{COMMAND}` command in the channel you want me to send announcements in.");
        }
    }

    private static async Task<HashSet<string>> GetUploads()
    {
        var request = Service.Search.List("id");
        request.ChannelId = BotConfig.ChannelID;
        request.MaxResults = 20;
        request.PublishedAfter = $"{LastUpdate.Year}-{LastUpdate.Month}-{LastUpdate.Day}T{LastUpdate.Hour}:{LastUpdate.Minute}:{LastUpdate.Second}Z";
        request.PageToken = " ";
        request.Type = "video";

        LastUpdate = DateTime.UtcNow.AddMinutes(-1); // -1 just in case

        var playlistResponse = await request.ExecuteAsync();
        return (from x in playlistResponse.Items select x.Id.VideoId).ToHashSet();
    }

    private static async Task SendAnnouncement()
    {
        Console.WriteLine("Sending announcement...");
        string plural = SendQueue.Count == 1 ? "a new song" : "new songs";
        StringBuilder message = new($"{BotConfig.BandName} has released {plural}!\n");
        foreach (var x in SendQueue)
        {
            message.AppendLine($"https://www.youtube.com/watch?v={x}");
        }

        foreach (var kvp in BotConfig.ServerChannels)
        {
            try
            {
                await ((ITextChannel)Client.GetChannel(kvp.Value)).SendMessageAsync(message.ToString());
            }
            catch { }
        }

        SendQueue.Clear();
    }

    public record Config
    {
        public string DiscordLoginToken { get; set; } = "INSERT_DISCORD_TOKEN";
        public string YoutubeLoginToken { get; set; } = "INSERT_YOUTUBE_TOKEN";
        public string ChannelID { get; set; } = "INSERT_CHANNEL_ID";
        public string BandName { get; set; } = "INSERT_BAND_NAME";
        public Dictionary<ulong, ulong> ServerChannels { get; set; } = null;
        public double UpdateInterval { get; set; } = 5.0; // in minutes
    }
}