using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal static class Tab5Protocol
{
    internal const string Prefix = "@AIBOT ";
    internal const int MaximumFrame = 32768;
    internal static readonly Guid Service = new("7af50001-7f23-4a91-bc65-667a19320101");
    internal static readonly Guid Info = new("7af50002-7f23-4a91-bc65-667a19320101");
    internal static readonly Guid Input = new("7af50003-7f23-4a91-bc65-667a19320101");
    internal static bool ValidId(string? id) => id is not null && Regex.IsMatch(id, "^[0-9a-f]{12}$");
    internal static bool ValidNonce(string? value) => value is not null && Regex.IsMatch(value, "^[0-9a-f]{32}$");
    internal static string Proof(byte[] key, string message) => Convert.ToHexString(
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    internal static bool Verify(byte[] key, string message, string? proof)
    {
        if (proof is null || !Regex.IsMatch(proof, "^[0-9a-f]{64}$")) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Proof(key,message)), Convert.FromHexString(proof));
    }
    // AES-256-GCM: nonce[12], tag[16], ciphertext. BLE adds a uint32-LE length.
    internal static byte[] Encrypt(byte[] key, string challenge, byte[] clear,int maximum=MaximumFrame)
    {
        if (!ValidNonce(challenge) || clear.Length > maximum-28) throw new ArgumentException("Invalid TAB5 packet");
        var result = new byte[clear.Length+28];
        RandomNumberGenerator.Fill(result.AsSpan(0,12));
        using var aes = new AesGcm(key,16);
        aes.Encrypt(result.AsSpan(0,12),clear,result.AsSpan(28),result.AsSpan(12,16),Encoding.ASCII.GetBytes(challenge));
        return result;
    }
    internal static byte[] Decrypt(byte[] key, string challenge, byte[] packet,int maximum=MaximumFrame)
    {
        if (packet.Length<28 || packet.Length>maximum || !ValidNonce(challenge)) throw new ArgumentException("Invalid TAB5 packet");
        var clear = new byte[packet.Length-28];
        using var aes = new AesGcm(key,16);
        aes.Decrypt(packet.AsSpan(0,12),packet.AsSpan(28),packet.AsSpan(12,16),clear,Encoding.ASCII.GetBytes(challenge));
        return clear;
    }
    internal static byte[] WithLength(byte[] packet)
    {
        var frame = new byte[packet.Length+4];
        BinaryPrimitives.WriteUInt32LittleEndian(frame,(uint)packet.Length);
        packet.CopyTo(frame,4); return frame;
    }
    internal static byte[] Snapshot(StatusSnapshot snapshot, string id, string session, long sequence, object? resource=null, string?[]? assetIds=null, object? codexTasks=null, object? ota=null, object? connectionHealth=null) =>
        JsonSerializer.SerializeToUtf8Bytes(new {
            version=1, type="tab5_status", deviceId=id, session, sequence,
            data=new { imageBinary=1,rpcBinary=1,rpcBulk=1,rpcOtaZlib=1,snapshot.Time,snapshot.EpochUtc,epochMilliseconds=snapshot.CapturedAt.ToUnixTimeMilliseconds(),snapshot.UtcOffsetSeconds,snapshot.CapturedAt,
                snapshot.Codex,snapshot.Claude,
                pcInput=new {session,sequence,lastInputTickMs=SystemIdleTime.LastInputTickMilliseconds()},
                quotas=snapshot.Quotas is null?null:new {claude=SummaryQuota(snapshot.Quotas.Claude),codex=SummaryQuota(snapshot.Quotas.Codex,true)},
                snapshot.Weather,stocks=snapshot.Stocks is null ? null : snapshot.Stocks with {Quotes=snapshot.Stocks.Quotes.Take(20).ToArray()},
                systemMetrics=snapshot.SystemMetrics is null ? null : snapshot.SystemMetrics with {Samples=(snapshot.SystemMetrics.History??snapshot.SystemMetrics.Samples)?.TakeLast(64).ToArray()},snapshot.Music,
                domesticActivity=snapshot.DomesticActivity is null?null:new {snapshot.DomesticActivity.ActiveProvider,snapshot.DomesticActivity.State,snapshot.DomesticActivity.NeedsInput,snapshot.DomesticActivity.TokensToday},
                snapshot.FollowApp,resource,assetIds,codexTasks,ota,connectionHealth,
                calendar=Calendar(snapshot),quotaDetails=Tab5Details.Quotas(snapshot),quotaTrend=Tab5QuotaTrend.Snapshot() }
        },JsonDefaults.Options);
    private static object? SummaryQuota(ProviderQuotaSnapshot? quota,bool codex=false)=>quota is null?null:new {
        PrimaryPercent=!codex||Tab5Details.CodexHasFiveHour(quota.Plan)?quota.PrimaryPercent:null,quota.WeeklyPercent,quota.Stale};
    private static object Calendar(StatusSnapshot s) {
        var local=DateTimeOffset.FromUnixTimeSeconds(s.EpochUtc).UtcDateTime.AddSeconds(s.UtcOffsetSeconds);
        return Tab5Calendar.Snapshot(local);
    }
}
