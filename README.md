# PhuerthannBot
A Discord bot that sends announcements when a channel posts new videos.


To use, download the executeable and run it. This will create a file called config.json. This config file has the following properties:

DiscordLoginToken => the login token for your Discord bot

YoutubeLoginToken => a Youtube API key

ChannelID => the ID of the youtube channel you want to get announcements for

BandName => the name of the channel you want announcements for. Cosmetic only

ServerChannels => a dictionary representing keyed by server ID with values being the channel ID of that server in which announcements should be sent. Don't touch this, it updates automatically.

UpdateInterval => the time (in minutes) between checks for videos.


Upon filling out the config file, just run the executeable and the bot should start up. To finish configuration, use the command "!phuerthann init" in the channel in which the bot should send announcements. The bot's configuration is now complete, and it should begin operation.
