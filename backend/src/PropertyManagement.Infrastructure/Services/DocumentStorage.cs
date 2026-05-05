using PropertyManagement.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace PropertyManagement.Infrastructure.Services;

public class LocalFileDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalFileDocumentStorage(IConfiguration config)
    {
        _root = config["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "documents");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeFolder = Path.Combine(_root, SafeFolder(folder));
        Directory.CreateDirectory(safeFolder);
        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(safeFolder, safeName);
        await using (var fs = File.Create(fullPath))
        {
            await content.CopyToAsync(fs, ct);
        }
        return Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
    }

    public Task<Stream> OpenAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, storagePath);
        Stream s = File.OpenRead(full);
        return Task.FromResult(s);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, storagePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public bool Exists(string storagePath) => File.Exists(Path.Combine(_root, storagePath));

    private static string SafeFolder(string folder) => string.Concat(folder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/'));
}

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;
    public DataProtectionSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("PropertyManagement.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
