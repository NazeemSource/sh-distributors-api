using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Distributor.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Distributor.Api.Services;

// Receipt and business writes commit together. A retry after a dropped response
// returns the original result without repeating stock or payment movements.
public sealed class OfflineSyncMiddleware(RequestDelegate next)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, Receipt> MemoryReceipts = new();
    private sealed record Receipt(string Hash, int Status, string Body, string Versions);
    public const string Schema = "CREATE TABLE IF NOT EXISTS OfflineReceipts (ReceiptKey varchar(100) NOT NULL PRIMARY KEY, RequestHash char(64) NOT NULL, StatusCode int NOT NULL, ResponseBody longtext NOT NULL, Versions longtext NOT NULL)";

    public async Task InvokeAsync(HttpContext context, AppDbContext db, OfflineData data)
    {
        var request = context.Request;
        var snapshot = request.Path == "/api/offline/snapshot";
        if (context.User.Identity?.IsAuthenticated != true || !request.Path.StartsWithSegments("/api") || (!snapshot && (HttpMethods.IsGet(request.Method) || HttpMethods.IsOptions(request.Method)))) { await next(context); return; }
        var operation = request.Headers["X-Offline-Operation"].ToString();
        if (operation.Length > 0 && !Guid.TryParse(operation, out _)) { context.Response.StatusCode = 400; return; }
        var key = context.User.FindFirstValue(ClaimTypes.NameIdentifier) + ":" + operation;
        request.EnableBuffering();
        var body = await new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(); request.Body.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Method + request.Path + body)));
        var expected = new Dictionary<string, string>();
        try { if (request.Headers.TryGetValue("X-Offline-Versions", out var v)) expected = JsonSerializer.Deserialize<Dictionary<string, string>>(v.ToString()) ?? []; }
        catch (JsonException) { context.Response.StatusCode = 400; return; }
        if (expected.Count > 500) { context.Response.StatusCode = 400; return; }
        await Gate.WaitAsync(context.RequestAborted);
        var relational = db.Database.IsRelational(); var locked = false;
        var original = context.Response.Body;
        try {
            if (relational) {
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('distributor_offline_writes', 30)";
                locked = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
                if (!locked) throw new BusinessException("busy", "Another change is being saved. Retry shortly.", 503);
            }
            await using var transaction = relational ? await db.Database.BeginTransactionAsync() : null;
            Receipt? receipt = null;
            if (operation.Length > 0) {
                if (relational) {
                    await using var command = Command(db, "SELECT RequestHash, StatusCode, ResponseBody, Versions FROM OfflineReceipts WHERE ReceiptKey=@key", ("@key", key));
                    await using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync()) receipt = new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3));
                } else MemoryReceipts.TryGetValue(key, out receipt);
                if (receipt is not null) {
                    if (receipt.Hash != hash) throw new BusinessException("operation_reused", "This sync identifier has already been used for another change.", 409);
                    context.Response.StatusCode = receipt.Status; context.Response.ContentType = "application/json";
                    context.Response.Headers["X-Offline-Versions"] = receipt.Versions;
                    await context.Response.WriteAsync(receipt.Body); return;
                }
                foreach (var pair in expected) if (await data.Version(pair.Key) != pair.Value)
                    throw new BusinessException("sync_conflict", "This record changed on another device. Your pending changes are saved locally; review them before syncing.", 409);
            }
            await using var buffer = new MemoryStream(); context.Response.Body = buffer;
            await next(context);
            buffer.Position = 0; var responseBody = await new StreamReader(buffer, leaveOpen: true).ReadToEndAsync();
            if (context.Response.StatusCode < 400) {
                var versions = new Dictionary<string, string>();
                foreach (var pair in expected) versions[pair.Key] = await data.Version(pair.Key);
                var versionJson = JsonSerializer.Serialize(versions);
                context.Response.Headers["X-Offline-Versions"] = versionJson;
                if (operation.Length > 0) {
                    receipt = new(hash, context.Response.StatusCode, responseBody, versionJson);
                    if (relational) {
                        await using var command = Command(db, "INSERT INTO OfflineReceipts (ReceiptKey,RequestHash,StatusCode,ResponseBody,Versions) VALUES (@key,@hash,@status,@body,@versions)", ("@key", key), ("@hash", hash), ("@status", receipt.Status), ("@body", responseBody), ("@versions", versionJson));
                        await command.ExecuteNonQueryAsync();
                    } else MemoryReceipts[key] = receipt;
                }
                if (transaction is not null) await transaction.CommitAsync();
            } else if (transaction is not null) await transaction.RollbackAsync();
            context.Response.Body = original; await context.Response.WriteAsync(responseBody);
        }
        finally {
            context.Response.Body = original;
            try {
                if (relational) {
                    try { if (locked) { await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT RELEASE_LOCK('distributor_offline_writes')"; await command.ExecuteScalarAsync(); } }
                    finally { await db.Database.CloseConnectionAsync(); }
                }
            } finally { Gate.Release(); }
        }
    }
    private static DbCommand Command(AppDbContext db, string sql, params (string Name, object Value)[] values) {
        var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var (name, value) in values) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
        return command;
    }
}
