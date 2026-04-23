using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Ludo;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Ludo Console";
        Console.CursorVisible = false;

        new LudoLauncher().Run();
    }
}

internal enum PlayerKind
{
    LocalHuman,
    RemoteHuman,
    Bot,
}

internal enum PlayerColor
{
    Red,
    Green,
    Yellow,
    Blue,
}

internal enum SessionMode
{
    Local,
    Host,
    Client,
}

internal sealed class LudoLauncher
{
    public void Run()
    {
        while (true)
        {
            Console.Clear();
            WriteTitle("LUDO CONSOLE by HMovaghari");
            Console.WriteLine("README says this project is a console Ludo game with these core rules:");
            Console.WriteLine("- each player has 4 tokens");
            Console.WriteLine("- you usually need a 6 to leave base");
            Console.WriteLine("- rolling a 6 gives another turn");
            Console.WriteLine("- landing on an opponent sends it back to base");
            Console.WriteLine("- first player to bring all 4 tokens home wins");
            Console.WriteLine();
            Console.WriteLine("1. Single player (You + 3 bots)");
            Console.WriteLine("2. Host network game");
            Console.WriteLine("3. Join network game");
            Console.WriteLine("Q. Exit");
            Console.WriteLine();
            Console.Write("Select mode: ");

            var key = Console.ReadKey(true).Key;
            try
            {
                switch (key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        new LudoGame(SessionConfig.CreateLocal()).Run();
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        RunHost();
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        RunClient();
                        break;

                    case ConsoleKey.Q:
                        return;
                }
            }
            catch (SocketException exception)
            {
                ShowInfo($"Network error: {exception.SocketErrorCode}");
            }
            catch (IOException exception)
            {
                ShowInfo($"Connection closed: {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                ShowInfo(exception.Message);
            }
        }
    }

    private static void RunHost()
    {
        Console.Clear();
        WriteTitle("Host Network Game");

        Console.Write("Your name: ");
        var hostName = ReadRequiredText("Host");
        var remotePlayers = ReadInt("Number of remote players (1-3): ", 1, 3);
        var port = ReadInt("Port (1024-65535, default 27015): ", 1024, 65535, 27015);

        new LudoGame(SessionConfig.CreateHost(hostName, remotePlayers, port)).Run();
    }

    private static void RunClient()
    {
        Console.Clear();
        WriteTitle("Join Network Game");

        Console.Write("Your name: ");
        var playerName = ReadRequiredText("Player");
        Console.Write("Host IP or name: ");
        var host = ReadRequiredText("127.0.0.1");
        var port = ReadInt("Port (1024-65535, default 27015): ", 1024, 65535, 27015);

        new LudoNetworkClient(SessionConfig.CreateClient(playerName, host, port)).Run();
    }

    private static string ReadRequiredText(string fallback)
    {
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadInt(string prompt, int min, int max, int? defaultValue = null)
    {
        while (true)
        {
            Console.Write(prompt);
            var raw = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(raw) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            if (int.TryParse(raw, out var value) && value >= min && value <= max)
            {
                return value;
            }
        }
    }

    private static void WriteTitle(string title)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"=== {title} ===");
        ConsoleResetColor();
        Console.WriteLine();
    }

    private static void ConsoleResetColor()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    private static void ShowInfo(string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        ConsoleResetColor();
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey(true);
    }
}

internal sealed record SessionConfig(
    SessionMode Mode,
    string LocalPlayerName,
    int RemoteHumanCount,
    int Port,
    string? HostAddress = null)
{
    public static SessionConfig CreateLocal()
    {
        return new SessionConfig(SessionMode.Local, "You", 0, 0);
    }

    public static SessionConfig CreateHost(string localPlayerName, int remoteHumanCount, int port)
    {
        return new SessionConfig(SessionMode.Host, localPlayerName, remoteHumanCount, port);
    }

    public static SessionConfig CreateClient(string localPlayerName, string hostAddress, int port)
    {
        return new SessionConfig(SessionMode.Client, localPlayerName, 0, port, hostAddress);
    }
}

internal static class LudoBoard
{
    public const int TrackLength = 52;
    public const int FinalProgress = 57;

    public static readonly (int Row, int Col)[] Track =
    [
        (6, 1), (6, 2), (6, 3), (6, 4), (6, 5),
        (5, 6), (4, 6), (3, 6), (2, 6), (1, 6), (0, 6),
        (0, 7),
        (0, 8), (1, 8), (2, 8), (3, 8), (4, 8), (5, 8),
        (6, 9), (6, 10), (6, 11), (6, 12), (6, 13), (6, 14),
        (7, 14),
        (8, 14), (8, 13), (8, 12), (8, 11), (8, 10), (8, 9),
        (9, 8), (10, 8), (11, 8), (12, 8), (13, 8), (14, 8),
        (14, 7),
        (14, 6), (13, 6), (12, 6), (11, 6), (10, 6), (9, 6),
        (8, 5), (8, 4), (8, 3), (8, 2), (8, 1), (8, 0),
        (7, 0),
        (6, 0),
    ];

    public static readonly Dictionary<PlayerColor, int> StartIndices = new()
    {
        [PlayerColor.Red] = 0,
        [PlayerColor.Green] = 13,
        [PlayerColor.Yellow] = 26,
        [PlayerColor.Blue] = 39,
    };

    public static readonly Dictionary<PlayerColor, int> SafeTrackIndices = new()
    {
        [PlayerColor.Red] = 0,
        [PlayerColor.Green] = 13,
        [PlayerColor.Yellow] = 26,
        [PlayerColor.Blue] = 39,
    };

    public static readonly Dictionary<PlayerColor, (int Row, int Col)[]> HomeLanes = new()
    {
        [PlayerColor.Red] = [(7, 1), (7, 2), (7, 3), (7, 4), (7, 5), (7, 6)],
        [PlayerColor.Green] = [(1, 7), (2, 7), (3, 7), (4, 7), (5, 7), (6, 7)],
        [PlayerColor.Yellow] = [(7, 13), (7, 12), (7, 11), (7, 10), (7, 9), (7, 8)],
        [PlayerColor.Blue] = [(13, 7), (12, 7), (11, 7), (10, 7), (9, 7), (8, 7)],
    };

    public static readonly Dictionary<PlayerColor, (int Row, int Col)[]> Yards = new()
    {
        [PlayerColor.Red] = [(1, 1), (1, 2), (2, 1), (2, 2)],
        [PlayerColor.Green] = [(1, 12), (1, 13), (2, 12), (2, 13)],
        [PlayerColor.Yellow] = [(12, 12), (12, 13), (13, 12), (13, 13)],
        [PlayerColor.Blue] = [(12, 1), (12, 2), (13, 1), (13, 2)],
    };

    public static int GetTrackIndex(PlayerColor color, int progress)
    {
        return (StartIndices[color] + progress) % TrackLength;
    }

    public static bool TryGetTokenCoordinate(PlayerColor color, int tokenIndex, int progress, out (int Row, int Col) coordinate)
    {
        if (progress == -1)
        {
            coordinate = Yards[color][tokenIndex];
            return true;
        }

        if (progress == FinalProgress)
        {
            coordinate = (7, 7);
            return true;
        }

        if (progress >= 52)
        {
            coordinate = HomeLanes[color][progress - 52];
            return true;
        }

        coordinate = Track[GetTrackIndex(color, progress)];
        return true;
    }

    public static string DescribeProgress(int progress)
    {
        return progress switch
        {
            -1 => "Base",
            FinalProgress => "Home",
            >= 52 => $"Lane {progress - 51}",
            _ => $"Track {progress + 1}",
        };
    }

    public static string GetShortName(PlayerColor color)
    {
        return color switch
        {
            PlayerColor.Red => "R",
            PlayerColor.Green => "G",
            PlayerColor.Yellow => "Y",
            PlayerColor.Blue => "B",
            _ => "?",
        };
    }

    public static ConsoleColor GetConsoleColor(PlayerColor color)
    {
        return color switch
        {
            PlayerColor.Red => ConsoleColor.Red,
            PlayerColor.Green => ConsoleColor.Green,
            PlayerColor.Yellow => ConsoleColor.Yellow,
            PlayerColor.Blue => ConsoleColor.Cyan,
            _ => ConsoleColor.White,
        };
    }
}

internal sealed class LudoGame
{
    private readonly SessionConfig config;
    private readonly Random random = new();
    private readonly List<LudoPlayer> players = [];
    private readonly Dictionary<int, RemotePeer> remotePeers = [];
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private TcpListener? listener;
    private int currentPlayerIndex;
    private int? lastDice;
    private string message = "Roll a 6 to bring a token out of base.";

    public LudoGame(SessionConfig config)
    {
        this.config = config;
        InitializePlayers();
    }

    public void Run()
    {
        try
        {
            if (config.Mode == SessionMode.Host)
            {
                SetupHostConnections();
            }

            currentPlayerIndex = 0;
            BroadcastState(Array.Empty<int>());

            while (true)
            {
                var player = players[currentPlayerIndex];
                var extraTurn = TakeTurn(player);

                if (player.Tokens.All(token => token.Progress == LudoBoard.FinalProgress))
                {
                    message = $"{player.Name} wins the match!";
                    BroadcastState(Array.Empty<int>());
                    NotifyGameOver(player.Name);
                    ShowExitPrompt(config.Mode == SessionMode.Host ? "Press any key to return to the menu..." : "Press any key to finish...");
                    return;
                }

                if (!extraTurn)
                {
                    currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
                }
            }
        }
        finally
        {
            foreach (var peer in remotePeers.Values)
            {
                peer.Dispose();
            }

            listener?.Stop();
        }
    }

    private void InitializePlayers()
    {
        players.Add(new LudoPlayer(config.LocalPlayerName, PlayerColor.Red, PlayerKind.LocalHuman, 0));

        if (config.Mode == SessionMode.Local)
        {
            players.Add(new LudoPlayer("Bot Green", PlayerColor.Green, PlayerKind.Bot, 1));
            players.Add(new LudoPlayer("Bot Yellow", PlayerColor.Yellow, PlayerKind.Bot, 2));
            players.Add(new LudoPlayer("Bot Blue", PlayerColor.Blue, PlayerKind.Bot, 3));
            return;
        }

        for (var seat = 1; seat <= config.RemoteHumanCount; seat++)
        {
            players.Add(new LudoPlayer($"Remote {seat}", (PlayerColor)seat, PlayerKind.RemoteHuman, seat));
        }

        for (var seat = config.RemoteHumanCount + 1; seat < 4; seat++)
        {
            players.Add(new LudoPlayer($"Bot {((PlayerColor)seat)}", (PlayerColor)seat, PlayerKind.Bot, seat));
        }
    }

    private void SetupHostConnections()
    {
        listener = new TcpListener(IPAddress.Any, config.Port);
        listener.Start();

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== HOSTING LUDO ===");
        ConsoleResetColor();
        Console.WriteLine($"Port: {config.Port}");
        Console.WriteLine($"Waiting for {config.RemoteHumanCount} remote player(s)...");
        Console.WriteLine();

        for (var seat = 1; seat <= config.RemoteHumanCount; seat++)
        {
            Console.WriteLine($"Waiting for seat {seat} ({(PlayerColor)seat})...");
            var client = listener.AcceptTcpClient();
            client.NoDelay = true;

            var connection = new JsonLineConnection(client, jsonOptions);
            var join = connection.Receive<JoinRequestMessage>();
            var player = players[seat];
            player.Name = string.IsNullOrWhiteSpace(join.PlayerName) ? player.Name : join.PlayerName;

            remotePeers[player.SeatIndex] = new RemotePeer(player.SeatIndex, connection);
            connection.Send(new ServerEnvelope
            {
                Type = ServerMessageType.Welcome,
                Welcome = new WelcomeMessage
                {
                    SeatIndex = player.SeatIndex,
                    AssignedColor = player.Color,
                    PlayerName = player.Name,
                    HostName = players[0].Name,
                },
            });

            Console.ForegroundColor = LudoBoard.GetConsoleColor(player.Color);
            Console.WriteLine($"Connected: {player.Name} as {player.Color}");
            ConsoleResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("All remote players joined...");
        Thread.Sleep(3000);
    }

    private void ConsoleResetColor()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    private bool TakeTurn(LudoPlayer player)
    {
        lastDice = null;
        BroadcastState(Array.Empty<int>());

        switch (player.Kind)
        {
            case PlayerKind.LocalHuman:
                Console.Write("Press Enter to roll the dice...");
                WaitForEnter();
                break;
            case PlayerKind.RemoteHuman:
                RequestRemoteRoll(player);
                break;
            default:
                Pause(700);
                break;
        }

        var dice = random.Next(1, 7);
        lastDice = dice;
        var movableTokens = GetMovableTokens(player, dice);

        message = movableTokens.Count == 0
            ? $"{player.Name} rolled {dice}. No legal move this turn."
            : $"{player.Name} rolled {dice}.";

        BroadcastState(movableTokens);

        if (movableTokens.Count == 0)
        {
            Pause(900);
            return dice == 6;
        }

        var tokenIndex = player.Kind switch
        {
            PlayerKind.LocalHuman => AskLocalHumanForToken(player, movableTokens),
            PlayerKind.RemoteHuman => AskRemoteHumanForToken(player, movableTokens),
            _ => SelectBotMove(player, movableTokens, dice),
        };

        ApplyMove(player, tokenIndex, dice);
        BroadcastState(Array.Empty<int>());
        Pause(900);
        return dice == 6;
    }

    private void RequestRemoteRoll(LudoPlayer player)
    {
        message = $"Waiting for {player.Name} on the network...";
        BroadcastState(Array.Empty<int>());

        try
        {
            var peer = remotePeers[player.SeatIndex];
            peer.Connection.Send(new ServerEnvelope
            {
                Type = ServerMessageType.PromptRoll,
                Prompt = "Press Enter on your client to roll the dice.",
            });

            var action = peer.Connection.Receive<ClientActionMessage>();
            if (action.Action != ClientActionType.RollDice)
            {
                throw new InvalidOperationException($"Invalid roll action received from {player.Name}.");
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidOperationException)
        {
            ConvertRemoteSeatToBot(player, $"{player.Name} disconnected before rolling. Bot takes over.");
        }
    }

    private int AskRemoteHumanForToken(LudoPlayer player, IReadOnlyList<int> movableTokens)
    {
        try
        {
            var peer = remotePeers[player.SeatIndex];
            peer.Connection.Send(new ServerEnvelope
            {
                Type = ServerMessageType.PromptChoose,
                Prompt = $"Choose token: {string.Join(", ", movableTokens.Select(index => player.Tokens[index].Name))}",
                TokenOptions = movableTokens.ToList(),
            });

            var action = peer.Connection.Receive<ClientActionMessage>();
            if (action.Action != ClientActionType.ChooseToken || !action.TokenIndex.HasValue || !movableTokens.Contains(action.TokenIndex.Value))
            {
                throw new InvalidOperationException($"Invalid token choice received from {player.Name}.");
            }

            return action.TokenIndex.Value;
        }
        catch (Exception exception) when (exception is IOException or SocketException or InvalidOperationException)
        {
            ConvertRemoteSeatToBot(player, $"{player.Name} disconnected while choosing a token. Bot takes over.");
            return SelectBotMove(player, movableTokens, lastDice ?? 1);
        }
    }

    private void ConvertRemoteSeatToBot(LudoPlayer player, string reason)
    {
        if (player.Kind != PlayerKind.RemoteHuman)
        {
            return;
        }

        if (remotePeers.Remove(player.SeatIndex, out var peer))
        {
            peer.Dispose();
        }

        player.Kind = PlayerKind.Bot;
        player.Name = $"Bot {player.Color}";
        message = reason;
        BroadcastState(Array.Empty<int>());
        Pause(900);
    }

    private void ApplyMove(LudoPlayer player, int tokenIndex, int dice)
    {
        var token = player.Tokens[tokenIndex];
        var previousState = LudoBoard.DescribeProgress(token.Progress);

        token.Progress = token.Progress == -1 ? 0 : token.Progress + dice;

        var captures = token.Progress is >= 0 and < LudoBoard.TrackLength
            ? HandleCapture(player, token)
            : [];

        var newState = LudoBoard.DescribeProgress(token.Progress);
        message = $"{player.Name} moved {token.Name} from {previousState} to {newState}.";

        if (captures.Count > 0)
        {
            message += $" Captured: {string.Join(", ", captures)}.";
        }

        if (dice == 6)
        {
            message += " Rolled 6, play again.";
        }
    }

    private List<string> HandleCapture(LudoPlayer player, LudoToken movedToken)
    {
        var trackIndex = LudoBoard.GetTrackIndex(player.Color, movedToken.Progress);
        if (LudoBoard.SafeTrackIndices.ContainsValue(trackIndex))
        {
            return [];
        }

        var captured = new List<string>();
        foreach (var opponent in players.Where(other => other.SeatIndex != player.SeatIndex))
        {
            foreach (var token in opponent.Tokens.Where(token => token.Progress is >= 0 and < LudoBoard.TrackLength))
            {
                if (LudoBoard.GetTrackIndex(opponent.Color, token.Progress) == trackIndex)
                {
                    token.Progress = -1;
                    captured.Add($"{opponent.Name} {token.Name}");
                }
            }
        }

        return captured;
    }

    private List<int> GetMovableTokens(LudoPlayer player, int dice)
    {
        var movable = new List<int>();
        for (var i = 0; i < player.Tokens.Count; i++)
        {
            var token = player.Tokens[i];
            if (token.Progress == LudoBoard.FinalProgress)
            {
                continue;
            }

            if (token.Progress == -1)
            {
                if (dice == 6)
                {
                    movable.Add(i);
                }

                continue;
            }

            if (token.Progress + dice <= LudoBoard.FinalProgress)
            {
                movable.Add(i);
            }
        }

        return movable;
    }

    private int AskLocalHumanForToken(LudoPlayer player, IReadOnlyList<int> movableTokens)
    {
        while (true)
        {
            Console.WriteLine();
            Console.Write("Choose token ");
            Console.ForegroundColor = LudoBoard.GetConsoleColor(player.Color);
            Console.Write(string.Join(", ", movableTokens.Select(index => player.Tokens[index].Name)));
            ConsoleResetColor();
            Console.Write(": ");

            var key = Console.ReadKey(true).Key;
            var picked = key switch
            {
                ConsoleKey.A => 0,
                ConsoleKey.B => 1,
                ConsoleKey.C => 2,
                ConsoleKey.D => 3,
                _ => -1,
            };

            if (movableTokens.Contains(picked))
            {
                return picked;
            }
        }
    }

    private int SelectBotMove(LudoPlayer player, IReadOnlyList<int> movableTokens, int dice)
    {
        var captureMove = movableTokens.FirstOrDefault(index => WouldCapture(player, player.Tokens[index], dice), -1);
        if (captureMove != -1)
        {
            return captureMove;
        }

        var enterMove = movableTokens.FirstOrDefault(index => player.Tokens[index].Progress == -1, -1);
        if (enterMove != -1)
        {
            return enterMove;
        }

        return movableTokens.OrderByDescending(index => player.Tokens[index].Progress).First();
    }

    private bool WouldCapture(LudoPlayer player, LudoToken token, int dice)
    {
        var simulatedProgress = token.Progress == -1 ? 0 : token.Progress + dice;
        if (simulatedProgress < 0 || simulatedProgress >= LudoBoard.TrackLength)
        {
            return false;
        }

        var trackIndex = LudoBoard.GetTrackIndex(player.Color, simulatedProgress);
        if (LudoBoard.SafeTrackIndices.ContainsValue(trackIndex))
        {
            return false;
        }

        return players
            .Where(other => other.SeatIndex != player.SeatIndex)
            .SelectMany(other => other.Tokens, (other, opponentToken) => new { other, opponentToken })
            .Any(entry => entry.opponentToken.Progress is >= 0 and < LudoBoard.TrackLength
                && LudoBoard.GetTrackIndex(entry.other.Color, entry.opponentToken.Progress) == trackIndex);
    }

    private void BroadcastState(IReadOnlyList<int> movableTokens)
    {
        var snapshot = BuildSnapshot(movableTokens);
        Console.Clear();
        LudoRenderer.RenderSnapshot(snapshot);

        if (config.Mode != SessionMode.Host)
        {
            return;
        }

        foreach (var peer in remotePeers.Values.ToList())
        {
            try
            {
                peer.Connection.Send(new ServerEnvelope
                {
                    Type = ServerMessageType.State,
                    Snapshot = snapshot,
                });
            }
            catch (Exception exception) when (exception is IOException or SocketException)
            {
                var player = players.First(candidate => candidate.SeatIndex == peer.SeatIndex);
                ConvertRemoteSeatToBot(player, $"{player.Name} disconnected. Bot takes over.");
            }
        }
    }

    private GameSnapshot BuildSnapshot(IReadOnlyList<int> movableTokens)
    {
        return new GameSnapshot
        {
            Players = players.Select(player => new PlayerSnapshot
            {
                SeatIndex = player.SeatIndex,
                Name = player.Name,
                Color = player.Color,
                Kind = player.Kind,
                TokenProgress = player.Tokens.Select(token => token.Progress).ToList(),
            }).ToList(),
            CurrentPlayerIndex = currentPlayerIndex,
            Dice = lastDice,
            Message = message,
            HighlightSeatIndex = players[currentPlayerIndex].SeatIndex,
            HighlightedTokens = movableTokens.ToList(),
        };
    }

    private void NotifyGameOver(string winnerName)
    {
        if (config.Mode != SessionMode.Host)
        {
            return;
        }

        foreach (var peer in remotePeers.Values.ToList())
        {
            try
            {
                peer.Connection.Send(new ServerEnvelope
                {
                    Type = ServerMessageType.GameOver,
                    Prompt = winnerName,
                });
            }
            catch (Exception)
            {
                // Best effort only; the host can still finish the match locally.
            }
        }
    }

    private static void ShowExitPrompt(string prompt)
    {
        Console.WriteLine();
        Console.WriteLine(prompt);
        Console.ReadKey(true);
    }

    private static void WaitForEnter()
    {
        while (Console.ReadKey(true).Key != ConsoleKey.Enter)
        {
        }
    }

    private static void Pause(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }
}

internal sealed class LudoNetworkClient
{
    private readonly SessionConfig config;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public LudoNetworkClient(SessionConfig config)
    {
        this.config = config;
    }

    public void Run()
    {
        using var tcpClient = new TcpClient();
        tcpClient.Connect(config.HostAddress!, config.Port);
        tcpClient.NoDelay = true;

        using var connection = new JsonLineConnection(tcpClient, jsonOptions);
        connection.Send(new JoinRequestMessage { PlayerName = config.LocalPlayerName });

        GameSnapshot? latestSnapshot = null;

        while (true)
        {
            var envelope = connection.Receive<ServerEnvelope>();
            switch (envelope.Type)
            {
                case ServerMessageType.Welcome:
                    ShowWelcome(envelope.Welcome ?? throw new InvalidOperationException("Welcome payload missing."));
                    break;

                case ServerMessageType.State:
                    latestSnapshot = envelope.Snapshot ?? throw new InvalidOperationException("State payload missing.");
                    Console.Clear();
                    LudoRenderer.RenderSnapshot(latestSnapshot);
                    break;

                case ServerMessageType.PromptRoll:
                    if (latestSnapshot is not null)
                    {
                        Console.Clear();
                        LudoRenderer.RenderSnapshot(latestSnapshot);
                    }

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(envelope.Prompt);
                    ConsoleResetColor();
                    Console.Write("Press Enter to roll... ");
                    WaitForEnter();
                    connection.Send(new ClientActionMessage { Action = ClientActionType.RollDice });
                    break;

                case ServerMessageType.PromptChoose:
                    if (latestSnapshot is not null)
                    {
                        Console.Clear();
                        LudoRenderer.RenderSnapshot(latestSnapshot);
                    }

                    var tokenIndex = AskForToken(envelope.TokenOptions ?? []);
                    connection.Send(new ClientActionMessage
                    {
                        Action = ClientActionType.ChooseToken,
                        TokenIndex = tokenIndex,
                    });
                    break;

                case ServerMessageType.GameOver:
                    if (latestSnapshot is not null)
                    {
                        Console.Clear();
                        LudoRenderer.RenderSnapshot(latestSnapshot);
                    }

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Winner: {envelope.Prompt}");
                    ConsoleResetColor();
                    Console.WriteLine("Press any key to close the client...");
                    Console.ReadKey(true);
                    return;
            }
        }
    }

    private void ConsoleResetColor()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    private static void ShowWelcome(WelcomeMessage welcome)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== CONNECTED TO HOST ===");
        ConsoleResetColor2();
        Console.WriteLine($"Host: {welcome.HostName}");
        Console.WriteLine($"You are {welcome.PlayerName} ({welcome.AssignedColor}).");
        Console.WriteLine("Waiting for the match to start...");
    }

    private static void ConsoleResetColor2()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    private static int AskForToken(IReadOnlyList<int> tokenOptions)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"Choose token: {string.Join(", ", tokenOptions.Select(index => ((char)('A' + index)).ToString()))}");
            var key = Console.ReadKey(true).Key;
            var picked = key switch
            {
                ConsoleKey.A => 0,
                ConsoleKey.B => 1,
                ConsoleKey.C => 2,
                ConsoleKey.D => 3,
                _ => -1,
            };

            if (tokenOptions.Contains(picked))
            {
                return picked;
            }
        }
    }

    private static void WaitForEnter()
    {
        while (Console.ReadKey(true).Key != ConsoleKey.Enter)
        {
        }
    }
}

internal static class LudoRenderer
{
    public static void RenderSnapshot(GameSnapshot snapshot)
    {
        WriteTopPanel(snapshot);
        var board = CreateBoardMap();
        PaintStaticBoard(board);
        PaintTokens(board, snapshot);

        for (var row = 0; row < 15; row++)
        {
            for (var col = 0; col < 15; col++)
            {
                WriteCell(board[row, col]);
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(snapshot.Message);
        ConsoleResetColor();
        Console.WriteLine();

        foreach (var player in snapshot.Players.OrderBy(player => player.SeatIndex))
        {
            Console.ForegroundColor = LudoBoard.GetConsoleColor(player.Color);
            Console.Write($"{player.Name,-12}");
            ConsoleResetColor();
            Console.Write(" ");
            Console.WriteLine(string.Join(" | ", player.TokenProgress.Select((progress, index) =>
                $"{((char)('A' + index)).ToString()}:{LudoBoard.DescribeProgress(progress)}")));
        }
    }

    private static void ConsoleResetColor()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
    }

    private static void WriteTopPanel(GameSnapshot snapshot)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== LUDO CONSOLE by HMovaghari ===");
        ConsoleResetColor();
        Console.Write("Turn: ");
        var currentPlayer = snapshot.Players.First(player => player.SeatIndex == snapshot.HighlightSeatIndex);
        Console.ForegroundColor = LudoBoard.GetConsoleColor(currentPlayer.Color);
        Console.Write(currentPlayer.Name);
        ConsoleResetColor();
        Console.Write("   Dice: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(snapshot.Dice?.ToString() ?? "-");
        ConsoleResetColor();
        Console.WriteLine("Board: S = safe start, + = home lane, HM = goal, >A/>B/>C/>D = legal moves");
        Console.WriteLine();
    }

    private static Cell[,] CreateBoardMap()
    {
        var board = new Cell[15, 15];
        for (var row = 0; row < 15; row++)
        {
            for (var col = 0; col < 15; col++)
            {
                board[row, col] = new Cell("   ", ConsoleColor.DarkGray, ConsoleColor.Black);
            }
        }

        return board;
    }

    private static void PaintStaticBoard(Cell[,] board)
    {
        PaintZone(board, 0, 0, 5, 5, ConsoleColor.DarkRed);
        PaintZone(board, 0, 10, 5, 5, ConsoleColor.DarkGreen);
        PaintZone(board, 10, 10, 5, 5, ConsoleColor.DarkYellow);
        PaintZone(board, 10, 0, 5, 5, ConsoleColor.DarkBlue);

        foreach (var coordinate in LudoBoard.Track)
        {
            board[coordinate.Row, coordinate.Col] = new Cell(" . ", ConsoleColor.DarkGray, ConsoleColor.Black);
        }

        foreach (var pair in LudoBoard.SafeTrackIndices)
        {
            var coordinate = LudoBoard.Track[pair.Value];
            board[coordinate.Row, coordinate.Col] = new Cell(" S ", LudoBoard.GetConsoleColor(pair.Key), ConsoleColor.Black);
        }

        foreach (var pair in LudoBoard.HomeLanes)
        {
            foreach (var coordinate in pair.Value)
            {
                board[coordinate.Row, coordinate.Col] = new Cell(" + ", LudoBoard.GetConsoleColor(pair.Key), ConsoleColor.Black);
            }
        }

        foreach (var pair in LudoBoard.Yards)
        {
            var label = LudoBoard.GetShortName(pair.Key);
            foreach (var coordinate in pair.Value)
            {
                board[coordinate.Row, coordinate.Col] = new Cell($"{label}B ", LudoBoard.GetConsoleColor(pair.Key), ConsoleColor.Black);
            }
        }

        board[7, 7] = new Cell(" HM", ConsoleColor.White, ConsoleColor.Black);
    }

    private static void PaintZone(Cell[,] board, int startRow, int startCol, int height, int width, ConsoleColor background)
    {
        for (var row = startRow; row < startRow + height; row++)
        {
            for (var col = startCol; col < startCol + width; col++)
            {
                board[row, col] = new Cell("   ", ConsoleColor.Black, background);
            }
        }
    }

    private static void PaintTokens(Cell[,] board, GameSnapshot snapshot)
    {
        var cellTokens = new Dictionary<(int Row, int Col), List<(PlayerSnapshot Player, int TokenIndex)>>();

        foreach (var player in snapshot.Players)
        {
            for (var tokenIndex = 0; tokenIndex < player.TokenProgress.Count; tokenIndex++)
            {
                if (!LudoBoard.TryGetTokenCoordinate(player.Color, tokenIndex, player.TokenProgress[tokenIndex], out var coordinate))
                {
                    continue;
                }

                if (!cellTokens.TryGetValue(coordinate, out var tokens))
                {
                    tokens = [];
                    cellTokens[coordinate] = tokens;
                }

                tokens.Add((player, tokenIndex));
            }
        }

        foreach (var entry in cellTokens)
        {
            var first = entry.Value[0];
            var text = entry.Value.Count == 1
                ? $"{LudoBoard.GetShortName(first.Player.Color)}{((char)('A' + first.TokenIndex)).ToString()}"
                : $"{LudoBoard.GetShortName(first.Player.Color)}{entry.Value.Count} ";

            if (snapshot.HighlightSeatIndex == first.Player.SeatIndex && snapshot.HighlightedTokens.Contains(first.TokenIndex))
            {
                text = $">{((char)('A' + first.TokenIndex)).ToString()}";
            }

            board[entry.Key.Row, entry.Key.Col] = new Cell(text, LudoBoard.GetConsoleColor(first.Player.Color), ConsoleColor.Black);
        }
    }

    private static void WriteCell(Cell cell)
    {
        Console.ForegroundColor = cell.Foreground;
        Console.BackgroundColor = cell.Background;
        Console.Write(cell.Text.PadRight(3)[..3]);
        ConsoleResetColor();
    }

    private readonly record struct Cell(string Text, ConsoleColor Foreground, ConsoleColor Background);
}

internal sealed class LudoPlayer
{
    public LudoPlayer(string name, PlayerColor color, PlayerKind kind, int seatIndex)
    {
        Name = name;
        Color = color;
        Kind = kind;
        SeatIndex = seatIndex;
        Tokens =
        [
            new LudoToken(0),
            new LudoToken(1),
            new LudoToken(2),
            new LudoToken(3),
        ];
    }

    public string Name { get; set; }

    public PlayerColor Color { get; }

    public PlayerKind Kind { get; set; }

    public int SeatIndex { get; }

    public List<LudoToken> Tokens { get; }
}

internal sealed class LudoToken
{
    public LudoToken(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public int Progress { get; set; } = -1;

    public string Name => ((char)('A' + Index)).ToString();
}

internal sealed class RemotePeer : IDisposable
{
    public RemotePeer(int seatIndex, JsonLineConnection connection)
    {
        SeatIndex = seatIndex;
        Connection = connection;
    }

    public int SeatIndex { get; }

    public JsonLineConnection Connection { get; }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

internal sealed class JsonLineConnection : IDisposable
{
    private readonly TcpClient client;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly JsonSerializerOptions jsonOptions;

    public JsonLineConnection(TcpClient client, JsonSerializerOptions jsonOptions)
    {
        this.client = client;
        this.jsonOptions = jsonOptions;

        var stream = client.GetStream();
        reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true)
        {
            AutoFlush = true,
        };
    }

    public void Send<T>(T payload)
    {
        writer.WriteLine(JsonSerializer.Serialize(payload, jsonOptions));
    }

    public T Receive<T>()
    {
        var line = reader.ReadLine();
        if (line is null)
        {
            throw new IOException("Remote connection closed.");
        }

        return JsonSerializer.Deserialize<T>(line, jsonOptions)
            ?? throw new InvalidOperationException("Received invalid JSON payload.");
    }

    public void Dispose()
    {
        writer.Dispose();
        reader.Dispose();
        client.Dispose();
    }
}

internal sealed class ServerEnvelope
{
    public ServerMessageType Type { get; set; }

    public string? Prompt { get; set; }

    public List<int>? TokenOptions { get; set; }

    public WelcomeMessage? Welcome { get; set; }

    public GameSnapshot? Snapshot { get; set; }
}

internal sealed class WelcomeMessage
{
    public int SeatIndex { get; set; }

    public PlayerColor AssignedColor { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;
}

internal sealed class JoinRequestMessage
{
    public string PlayerName { get; set; } = string.Empty;
}

internal sealed class ClientActionMessage
{
    public ClientActionType Action { get; set; }

    public int? TokenIndex { get; set; }
}

internal enum ServerMessageType
{
    Welcome,
    State,
    PromptRoll,
    PromptChoose,
    GameOver,
}

internal enum ClientActionType
{
    RollDice,
    ChooseToken,
}

internal sealed class GameSnapshot
{
    public List<PlayerSnapshot> Players { get; set; } = [];

    public int CurrentPlayerIndex { get; set; }

    public int? Dice { get; set; }

    public string Message { get; set; } = string.Empty;

    public int HighlightSeatIndex { get; set; }

    public List<int> HighlightedTokens { get; set; } = [];
}

internal sealed class PlayerSnapshot
{
    public int SeatIndex { get; set; }

    public string Name { get; set; } = string.Empty;

    public PlayerColor Color { get; set; }

    public PlayerKind Kind { get; set; }

    public List<int> TokenProgress { get; set; } = [];
}
