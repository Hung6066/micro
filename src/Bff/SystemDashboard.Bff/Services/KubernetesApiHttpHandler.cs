using System.Security.Cryptography.X509Certificates;

namespace SystemDashboard.Bff.Services;

internal static class KubernetesApiHttpHandler
{
    public static HttpMessageHandler Create()
    {
        var handler = new HttpClientHandler();
        const string caPath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";
        if (!File.Exists(caPath)) return handler;
        var ca = new X509Certificate2(File.ReadAllBytes(caPath));
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
        {
            if (certificate is null) return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(new X509Certificate2(certificate));
        };
        return handler;
    }
}
