namespace Busara.Ugs.Tests;

public sealed class MemoryStore : IPrivateStore
{
    private readonly Dictionary<string, StoredDocument> values = new();
    private readonly Dictionary<string, int> reads = new();
    private readonly object gate = new();
    private long revision;
    private string? raceId;
    private int remainingReaders;
    private TaskCompletionSource? readRace;
    private string? pauseId;
    private WritePause? pause;
    public bool LoseNextRoomAck;
    public bool LoseNextCandidateAck;
    public string? LoseNextAckFor;
    public int Conflicts;

    public sealed class WritePause
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task<StoredDocument?> Read(string id)
    {
        await Task.Yield();
        StoredDocument? value;
        Task? wait = null;
        lock (gate)
        {
            reads[id] = reads.GetValueOrDefault(id) + 1;
            value = values.GetValueOrDefault(id);
            if (raceId == id && readRace is not null)
            {
                wait = readRace.Task;
                if (--remainingReaders == 0)
                {
                    readRace.SetResult();
                    raceId = null;
                    readRace = null;
                }
            }
        }
        if (wait is not null) await wait;
        return value;
    }

    public void RaceNextTwoReads(string id)
    {
        lock (gate)
        {
            raceId = id;
            remainingReaders = 2;
            readRace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public WritePause PauseNextWrite(string id)
    {
        lock (gate)
        {
            pauseId = id;
            return pause = new WritePause();
        }
    }

    public async Task<bool> CompareExchange(string id, string json, string writeLock)
    {
        if (string.IsNullOrWhiteSpace(writeLock)) throw new ArgumentException("A write lock is required.");
        await Task.Yield();
        WritePause? wait = null;
        lock (gate)
        {
            if (pauseId == id) { wait = pause; pauseId = null; pause = null; }
        }
        if (wait is not null)
        {
            wait.Reached.SetResult();
            await wait.Resume.Task;
        }
        lock (gate)
        {
            if (!values.TryGetValue(id, out var previous))
                throw new InvalidOperationException("Absent-item CAS semantics are unsupported; provision the shard first.");
            if (previous.WriteLock != writeLock) { Conflicts++; return false; }
            values[id] = new StoredDocument(json, (++revision).ToString("x32"));
            if (LoseNextAckFor == id || (LoseNextRoomAck && IsRoom(id)))
            {
                LoseNextAckFor = null;
                LoseNextRoomAck = false;
                throw new IOException("Simulated lost ACK after commit.");
            }
            return true;
        }
    }

    public async Task CreateCandidate(string id, string json)
    {
        await Task.Yield();
        lock (gate)
        {
            // Cloud Save's unlocked SetItem is an unconditional upsert, not absent-create.
            values[id] = new StoredDocument(json, (++revision).ToString("x32"));
            if (LoseNextCandidateAck)
            {
                LoseNextCandidateAck = false;
                throw new IOException("Simulated lost candidate ACK after commit.");
            }
        }
    }

    public async Task ProvisionRegistrations()
    {
        for (int shard = 0; shard < MatchService.RegistrationShardCount; shard++)
            await CreateCandidate(MatchService.RegistrationShardKey(shard), Json.Encode(new RegistrationShard()));
    }

    public string PublishedGuestKey(string actor)
    {
        lock (gate)
            return Json.Decode<RegistrationShard>(values[MatchService.RegistrationKey(actor)].Json)
                .registrations[actor].guestDocumentId;
    }

    public static bool IsRoom(string id) =>
        id.StartsWith("busara_", StringComparison.Ordinal) && Guid.TryParseExact(id["busara_".Length..], "D", out _);
    public string[] Ids { get { lock (gate) return values.Keys.ToArray(); } }
    public int ReadCount(string id) { lock (gate) return reads.GetValueOrDefault(id); }
}
