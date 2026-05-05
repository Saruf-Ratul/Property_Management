namespace PropertyManagement.Application.Abstractions;

public interface IDocumentStorage
{
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
    bool Exists(string storagePath);
}

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
