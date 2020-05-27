﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using W3ChampionsStatisticService.CommonValueObjects;
using W3ChampionsStatisticService.PadEvents;
using W3ChampionsStatisticService.Ports;
using W3ChampionsStatisticService.ReadModelBase;

namespace W3ChampionsStatisticService.Matches
{
    public class MatchRepository : MongoDbRepositoryBase, IMatchRepository
    {
        public MatchRepository(MongoClient mongoClient) : base(mongoClient)
        {
        }

        public Task Insert(Matchup matchup)
        {
            return Upsert(matchup, m => m.MatchId == matchup.MatchId);
        }

        public async Task<List<Matchup>> LoadFor(
            string playerId,
            string opponentId = null,
            GateWay gateWay = GateWay.Undefined,
            GameMode gameMode = GameMode.Undefined,
            int pageSize = 100,
            int offset = 0)
        {
            var database = CreateClient();

            var mongoCollection = database.GetCollection<Matchup>(nameof(Matchup));
            var textSearchOpts = new TextSearchOptions();

            if (string.IsNullOrEmpty(opponentId))
            {
                return await mongoCollection
                    .Find(m => Builders<Matchup>.Filter.Text($"\"{playerId}\"", textSearchOpts).Inject()
                        && (gameMode == GameMode.Undefined || m.GameMode == gameMode)
                        && (gateWay == GateWay.Undefined || m.GateWay == gateWay))
                    .SortByDescending(s => s.Id)
                    .Skip(offset)
                    .Limit(pageSize)
                    .ToListAsync();
            }

            return await mongoCollection
                .Find(m =>
                    Builders<Matchup>.Filter.Text($"\"{playerId}\" \"{opponentId}\"", textSearchOpts).Inject()
                    && (gameMode == GameMode.Undefined || m.GameMode == gameMode)
                    && (gateWay == GateWay.Undefined || m.GateWay == gateWay))
                .SortByDescending(s => s.Id)
                .Skip(offset)
                .Limit(pageSize)
                .ToListAsync();
        }

        public Task<long> Count()
        {
            return CreateCollection<Matchup>().CountDocumentsAsync(x => true);
        }

        public Task<long> CountFor(
            string playerId,
            string opponentId = null,
            GateWay gateWay = GateWay.Undefined,
            GameMode gameMode = GameMode.Undefined)
        {
            var textSearchOpts = new TextSearchOptions();
            var mongoCollection = CreateCollection<Matchup>();
            if (string.IsNullOrEmpty(opponentId))
            {
                return mongoCollection.CountDocumentsAsync(m =>
                    Builders<Matchup>.Filter.Text($"\"{playerId}\"", textSearchOpts).Inject()
                    && (gameMode == GameMode.Undefined || m.GameMode == gameMode)
                    && (gateWay == GateWay.Undefined || m.GateWay == gateWay));
            }

            return mongoCollection.CountDocumentsAsync(m =>
                Builders<Matchup>.Filter.Text($"\"{playerId}\" \"{opponentId}\"", textSearchOpts).Inject()
                && (gameMode == GameMode.Undefined || m.GameMode == gameMode));
        }

        public async Task<MatchupDetail> LoadDetails(ObjectId id)
        {
            var originalMatch = await LoadFirst<MatchFinishedEvent>(t => t.Id == id);
            var match = await LoadFirst<Matchup>(t => t.Id == id);

            return new MatchupDetail
            {
                Match = match,
                PlayerScores = originalMatch?.result?.players.Select(p => CreateDetail(p)).ToList()
            };
        }

        public Task EnsureIndices()
        {
            var collection = CreateCollection<Matchup>();

            var matchUpLogBuilder = Builders<Matchup>.IndexKeys;

            var textIndex = new CreateIndexModel<Matchup>(
                matchUpLogBuilder
                .Text(x => x.Team1Players)
                .Text(x => x.Team2Players)
                .Text(x => x.Team3Players)
                .Text(x => x.Team4Players)
            );
            return collection.Indexes.CreateOneAsync(textIndex);
        }

        private PlayerScore CreateDetail(PlayerBlizzard playerBlizzard)
        {
            foreach (var player in playerBlizzard.heroes)
            {
                player.icon = player.icon.ParseReforgedName();
            }

            return new PlayerScore(
                playerBlizzard.battleTag,
                playerBlizzard.unitScore,
                playerBlizzard.heroes,
                playerBlizzard.heroScore,
                playerBlizzard.resourceScore);
        }

        public async Task<List<Matchup>> Load(
            GameMode gameMode = GameMode.Undefined,
            int offset = 0,
            int pageSize = 100)
        {
            var database = CreateClient();

            var mongoCollection = database.GetCollection<Matchup>(nameof(Matchup));

            var events = await mongoCollection.Find(m => gameMode == GameMode.Undefined || m.GameMode == gameMode)
                .SortByDescending(s => s.Id)
                .Skip(offset)
                .Limit(pageSize)
                .ToListAsync();

            return events;
        }

        public Task InsertOnGoingMatch(OnGoingMatchup matchup)
        {
            return Upsert(matchup, m => m.MatchId == matchup.MatchId);
        }


        public async Task<OnGoingMatchup> LoadOnGoingMatchForPlayer(string playerId)
        {
            var database = CreateClient();

            var mongoCollection = database.GetCollection<OnGoingMatchup>(nameof(OnGoingMatchup));

            return await mongoCollection
                .Find(m => m.Team1Players.Contains(playerId) 
                        || m.Team2Players.Contains(playerId)
                        || m.Team3Players.Contains(playerId)
                        || m.Team4Players.Contains(playerId)
                )
                .FirstOrDefaultAsync();
        }

        public Task DeleteOnGoingMatch(string matchId)
        {
            return Delete<OnGoingMatchup>(x => x.MatchId == matchId);
        }

        public async Task<List<OnGoingMatchup>> LoadOnGoingMatches(GameMode gameMode = GameMode.Undefined, int offset = 0, int pageSize = 100)
        {
            var database = CreateClient();

            var mongoCollection = database.GetCollection<OnGoingMatchup>(nameof(OnGoingMatchup));

            var events = await mongoCollection.Find(m => gameMode == GameMode.Undefined || m.GameMode == gameMode)
                .SortByDescending(s => s.Id)
                .Skip(offset)
                .Limit(pageSize)
                .ToListAsync();

            return events;
        }

        public Task<long> CountOnGoingMatches()
        {
            return CreateCollection<OnGoingMatchup>().CountDocumentsAsync(x => true);
        }
    }
}