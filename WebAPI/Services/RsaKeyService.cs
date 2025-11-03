using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebAPI.Services
{
    public class RsaKeyService
    {
        private readonly RSA _serverRsa;
        private readonly ConcurrentDictionary<string, string> _clientPublicKeys = new();

        public RsaKeyService()
        {
            _serverRsa = RSA.Create(2048);
        }

        public string GetServerPublicKeyBase64()
        {
            byte[] spki = _serverRsa.ExportSubjectPublicKeyInfo();
            return Convert.ToBase64String(spki);
        }

        public byte[] DecryptKeyBlock(byte[] encryptedKeyBlock)
        {
            return _serverRsa.Decrypt(encryptedKeyBlock, RSAEncryptionPadding.OaepSHA1);
        }

        public void SaveClientPublicKey(string clientId, string clientPublicKeyBase64)
        {
            _clientPublicKeys[clientId] = clientPublicKeyBase64;
        }

        public bool TryGetClientPublicKey(string clientId, out string? publicKeyBase64)
        {
            bool ok = _clientPublicKeys.TryGetValue(clientId, out string? value);
            publicKeyBase64 = value;
            return ok;
        }
    }
}


