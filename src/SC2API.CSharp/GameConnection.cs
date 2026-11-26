using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using SC2APIProtocol;

namespace SC2API_CSharp
{
    public class GameConnection
    {
        ProtobufProxy proxy = new ProtobufProxy();
        string address = "127.0.0.1";

        string starcraftExe;
        string starcraftDir;
        bool realtime;

        public GameConnection()
        { }

        public void StartSC2Instance(int port)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(starcraftExe);
            processStartInfo.Arguments = String.Format("-listen {0} -port {1} -displayMode 0", address, port);
            processStartInfo.WorkingDirectory = Path.Combine(starcraftDir, "Support64");
            Process.Start(processStartInfo);
        }

        public async Task Connect(int port)
        {

            for (int i = 0; i < 40; i++)
            {
                try
                {
                    await proxy.Connect(address, port);
                    return;
                }
                catch (WebSocketException) { }
                Thread.Sleep(2000);
            }
            throw new Exception("Unable to make a connection.");
        }

        public async Task CreateGame(String mapName, Race opponentRace, Difficulty opponentDifficulty)
        {
            RequestCreateGame createGame = new RequestCreateGame();
            // Keep local sessions observable at normal game speed. Ladder sessions retain stepped simulation.
            realtime = true;
            createGame.Realtime = realtime;

            string mapsDirectory = Path.Combine(starcraftDir, "Maps");
            string mapPath = Path.Combine(mapsDirectory, mapName);

            if (!File.Exists(mapPath))
            {
                string mapFileName = Path.GetFileName(mapName);
                string[] matches = Directory.Exists(mapsDirectory)
                    ? Directory.GetFiles(mapsDirectory, mapFileName, SearchOption.AllDirectories)
                    : Array.Empty<string>();

                if (matches.Length == 1)
                    mapPath = matches[0];
                else if (matches.Length > 1)
                    throw new Exception(
                        "Found more than one map named " + mapFileName + " under " + mapsDirectory
                        + ". Set MapName to a relative path to select one: "
                        + string.Join("; ", matches));
                else
                    throw new Exception(
                        "Could not find map " + mapName + " under " + mapsDirectory
                        + ". Keep the Maps subfolders intact; set MapName to a relative path if the map is in a subfolder.");
            }

            createGame.LocalMap = new LocalMap();
            createGame.LocalMap.MapPath = mapPath;

            PlayerSetup player1 = new PlayerSetup();
            createGame.PlayerSetup.Add(player1);
            player1.Type = PlayerType.Participant;

            PlayerSetup player2 = new PlayerSetup();
            createGame.PlayerSetup.Add(player2);
            player2.Race = opponentRace;
            player2.Type = PlayerType.Computer;
            player2.Difficulty = opponentDifficulty;

            Request request = new Request();
            request.CreateGame = createGame;
            Response response = await proxy.SendRequest(request);
        }

        private void readSettings()
        {
            string myDocuments = Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string executeInfo = Path.Combine(myDocuments, "Starcraft II", "ExecuteInfo.txt");
            if (File.Exists(executeInfo))
            {
                string[] lines = File.ReadAllLines(executeInfo);
                foreach (string line in lines)
                {
                    string argument = line.Substring(line.IndexOf('=') + 1).Trim();
                    if (line.Trim().StartsWith("executable"))
                    {
                        starcraftExe = argument;
                        starcraftDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(starcraftExe)));
                    }
                }
            }
            else
                throw new Exception("Unable to find ExecuteInfo.txt at " + executeInfo);
        }

        /// <summary>Creates the multiplayer game on the designated host SC2 instance.</summary>
        public async Task CreateLadderGame(string mapPath)
        {
            if (String.IsNullOrWhiteSpace(mapPath))
                throw new ArgumentException("A trusted multiplayer map path is required.", nameof(mapPath));
            if (!File.Exists(mapPath))
                throw new FileNotFoundException("The configured multiplayer map was not found.", mapPath);

            RequestCreateGame createGame = new RequestCreateGame();
            createGame.LocalMap = new LocalMap();
            createGame.LocalMap.MapPath = Path.GetFullPath(mapPath);

            // The host must advertise two human-controlled participants. Each bot runner
            // joins from its own SC2 process after this request succeeds.
            PlayerSetup player1 = new PlayerSetup();
            player1.Type = PlayerType.Participant;
            player1.Race = Race.Terran;
            createGame.PlayerSetup.Add(player1);

            PlayerSetup player2 = new PlayerSetup();
            player2.Type = PlayerType.Participant;
            player2.Race = Race.Terran;
            createGame.PlayerSetup.Add(player2);

            Request request = new Request();
            request.CreateGame = createGame;
            Response response = await proxy.SendRequest(request);
            if (response == null || response.CreateGame == null)
                throw new InvalidOperationException("The SC2 host did not acknowledge multiplayer game creation.");
            if (response.CreateGame.Error != ResponseCreateGame.Types.Error.Unset)
                throw new InvalidOperationException(
                    $"The SC2 host rejected multiplayer game creation: {response.CreateGame.Error} {response.CreateGame.ErrorDetails}".Trim());
        }

        public async Task<uint> JoinGame(Race race)
        {
            RequestJoinGame joinGame = new RequestJoinGame();
            joinGame.Race = race;

            joinGame.Options = new InterfaceOptions();
            joinGame.Options.Raw = true;
            joinGame.Options.Score = true;

            Request request = new Request();
            request.JoinGame = joinGame;
            Response response = await proxy.SendRequest(request);
            return response.JoinGame.PlayerId;
        }

        public async Task<uint> JoinGameLadder(Race race, int startPort)
        {
            RequestJoinGame joinGame = new RequestJoinGame();
            joinGame.Race = race;

            joinGame.SharedPort = startPort + 1;
            joinGame.ServerPorts = new PortSet();
            joinGame.ServerPorts.GamePort = startPort + 2;
            joinGame.ServerPorts.BasePort = startPort + 3;

            joinGame.ClientPorts.Add(new PortSet());
            joinGame.ClientPorts[0].GamePort = startPort + 4;
            joinGame.ClientPorts[0].BasePort = startPort + 5;

            joinGame.Options = new InterfaceOptions();
            joinGame.Options.Raw = true;
            joinGame.Options.ShowCloaked = true;
            joinGame.Options.Score = true;

            Request request = new Request();
            request.JoinGame = joinGame;

            Response response = await proxy.SendRequest(request);
            if (response?.JoinGame == null || response.JoinGame.PlayerId == 0)
                throw new InvalidOperationException("The SC2 client did not return a valid multiplayer player ID.");
            return response.JoinGame.PlayerId;
        }

        public async Task<ResponsePing> Ping()
        {
            Request request = new Request();
            request.Ping = new RequestPing();
            Response response = await proxy.SendRequest(request);
            return response.Ping;
        }

        public async Task RequestLeaveGame()
        {
            Request requestLeaveGame = new Request();
            requestLeaveGame.LeaveGame = new RequestLeaveGame();
            await proxy.SendRequest(requestLeaveGame);
        }

        public async Task SendRequest(Request request)
        {
            await proxy.SendRequest(request);
        }

        public async Task<ResponseQuery> SendQuery(RequestQuery query)
        {
            Request request = new Request();
            request.Query = query;
            Response response = await proxy.SendRequest(request);
            return response.Query;
        }

        /// <summary>
        /// Joins this already-running SC2 process as an observer. The host must
        /// supply an explicit observed-player perspective and a port block unique
        /// to this SC2 process. This method does not launch or own the process.
        /// </summary>
        public async Task<ResponseJoinGame> JoinObserver(uint observedPlayerId, int startPort)
        {
            RequestJoinGame joinGame = new RequestJoinGame();
            joinGame.ObservedPlayerId = observedPlayerId;
            joinGame.SharedPort = startPort + 1;
            joinGame.ServerPorts = new PortSet();
            joinGame.ServerPorts.GamePort = startPort + 2;
            joinGame.ServerPorts.BasePort = startPort + 3;
            joinGame.ClientPorts.Add(new PortSet());
            joinGame.ClientPorts[0].GamePort = startPort + 4;
            joinGame.ClientPorts[0].BasePort = startPort + 5;
            joinGame.Options = new InterfaceOptions();
            joinGame.Options.Raw = true;
            joinGame.Options.Score = true;

            Request request = new Request();
            request.JoinGame = joinGame;
            Response response = await proxy.SendRequest(request);
            if (response?.JoinGame == null)
                throw new InvalidOperationException("The SC2 observer client did not acknowledge JoinGame.");
            return response.JoinGame;
        }

        /// <summary>Gets one raw SC2 observation/status response from this session.</summary>
        public async Task<Response> Observe()
        {
            Request request = new Request();
            request.Observation = new RequestObservation();
            return await proxy.SendRequest(request);
        }

        /// <summary>Requests replay bytes from this SC2 session after terminal status.</summary>
        public async Task<Google.Protobuf.ByteString> SaveReplay()
        {
            Request request = new Request();
            request.SaveReplay = new RequestSaveReplay();
            Response response = await proxy.SendRequest(request);
            if (response?.SaveReplay == null || response.SaveReplay.Data == null || response.SaveReplay.Data.Length == 0)
                throw new InvalidOperationException("SC2 did not return replay bytes.");
            return response.SaveReplay.Data;
        }

        /// <summary>Requests metadata for replay bytes using the pinned SC2 protocol.</summary>
        public async Task<ResponseReplayInfo> GetReplayInfo(Google.Protobuf.ByteString replayData)
        {
            if (replayData == null || replayData.Length == 0)
                throw new ArgumentException("Non-empty replay bytes are required.", nameof(replayData));

            Request request = new Request();
            request.ReplayInfo = new RequestReplayInfo();
            request.ReplayInfo.ReplayData = replayData;
            Response response = await proxy.SendRequest(request);
            if (response?.ReplayInfo == null)
                throw new InvalidOperationException("SC2 did not return replay metadata.");
            if (response.ReplayInfo.Error != ResponseReplayInfo.Types.Error.Unset)
                throw new InvalidOperationException(
                    $"SC2 rejected replay metadata inspection: {response.ReplayInfo.Error} {response.ReplayInfo.ErrorDetails}".Trim());
            return response.ReplayInfo;
        }

        public async Task Run(Bot bot, uint playerId, string opponentID)
        {
            Request gameInfoReq = new Request();
            gameInfoReq.GameInfo = new RequestGameInfo();

            Response gameInfoResponse = await proxy.SendRequest(gameInfoReq);

            Request gameDataRequest = new Request();
            gameDataRequest.Data = new RequestData();
            gameDataRequest.Data.UnitTypeId = true;
            gameDataRequest.Data.AbilityId = true;
            gameDataRequest.Data.BuffId = true;
            gameDataRequest.Data.EffectId = true;
            gameDataRequest.Data.UpgradeId = true;

            Response dataResponse = await proxy.SendRequest(gameDataRequest);

            ResponsePing pingResponse = await Ping();

            bool start = true;

            while (true)
            {
                Request observationRequest = new Request();
                observationRequest.Observation = new RequestObservation();
                Response response = await proxy.SendRequest(observationRequest);

                ResponseObservation observation = response.Observation;

                if (observation == null)
                {
                    bot.OnEnd(observation, Result.Unset);
                    break;
                }
                if (response.Status == Status.Ended || response.Status == Status.Quit)
                {
                    var finalResult = Sc2PlayerResultResolver.GetRequiredResult(observation, playerId);
                    Console.WriteLine($"SC2_RESULT playerId={playerId} result={finalResult}");
                    bot.OnEnd(observation, finalResult);
                    break;
                }

                if (start)
                {
                    start = false;
                    bot.OnStart(gameInfoResponse.GameInfo, dataResponse.Data, pingResponse, observation, playerId, opponentID);
                }

                IEnumerable<SC2APIProtocol.Action> actions = bot.OnFrame(observation);

                Request actionRequest = new Request();
                actionRequest.Action = new RequestAction();
                actionRequest.Action.Actions.AddRange(actions);
                if (actionRequest.Action.Actions.Count > 0)
                {
                    var submittedActions = new List<SC2APIProtocol.Action>(actionRequest.Action.Actions);
                    Console.WriteLine($"[SC2 API] Submitting {submittedActions.Count} raw action(s).");
                    Response actionResponse = await proxy.SendRequest(actionRequest);
                    bot.OnActionResult(submittedActions, actionResponse?.Action);
                }

                if (bot is QueryAwareBot queryBot)
                {
                    RequestQuery query = queryBot.TakeQueryRequest();
                    if (query != null)
                    {
                        Request queryRequest = new Request();
                        queryRequest.Query = query;
                        Response queryResponse = await proxy.SendRequest(queryRequest);
                        queryBot.OnQueryResult(queryResponse?.Query);
                    }
                }

                if (!realtime)
                {
                    Request stepRequest = new Request();
                    stepRequest.Step = new RequestStep();
                    stepRequest.Step.Count = 1;
                    await proxy.SendRequest(stepRequest);
                }
            }
        }

        public async Task RunSinglePlayer(Bot bot, string map, Race myRace, Race opponentRace, Difficulty opponentDifficulty)
        {
            readSettings();
            StartSC2Instance(5678);
            await Connect(5678);
            await CreateGame(map, opponentRace, opponentDifficulty);
            uint playerId = await JoinGame(myRace);
            await Run(bot, playerId, null);
        }

        public async Task RunLadder(Bot bot, Race myRace, int gamePort, int startPort, String opponentID)
        {
            await Connect(gamePort);
            uint playerId = await JoinGameLadder(myRace, startPort);
            await Run(bot, playerId, opponentID);
        }

        public async Task RunLadder(Bot bot, Race myRace, string[] args)
        {
            CLArgs clargs = new CLArgs(args);
            if (clargs.CreateGame)
            {
                await Connect(clargs.GamePort);
                await CreateLadderGame(clargs.MapPath);
                uint hostPlayerId = await JoinGameLadder(myRace, clargs.StartPort);
                Console.WriteLine($"SC2_JOINED playerId={hostPlayerId}");
                await Run(bot, hostPlayerId, clargs.OpponentID);
                return;
            }

            await Connect(clargs.GamePort);
            uint playerId = await JoinGameLadder(myRace, clargs.StartPort);
            Console.WriteLine($"SC2_JOINED playerId={playerId}");
            await Run(bot, playerId, clargs.OpponentID);
        }
    }
}
