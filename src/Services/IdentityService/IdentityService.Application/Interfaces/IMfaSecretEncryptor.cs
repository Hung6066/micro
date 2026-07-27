namespace His.Hope.IdentityService.Application.Interfaces;

public interface IMfaSecretEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
