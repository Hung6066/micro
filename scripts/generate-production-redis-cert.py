"""Issue the production Redis TLS certificate outside the repository secret store."""

from datetime import datetime, timedelta, timezone
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID


root = Path(r"D:\secure\his-hope")
ca = x509.load_pem_x509_certificate((root / "his_hope_ca.pem").read_bytes())
ca_key = serialization.load_pem_private_key((root / "his_hope_ca.key").read_bytes(), password=None)
key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
names = [
    "redis",
    "redis.his-hope",
    "redis.his-hope.svc",
    "redis.his-hope.svc.cluster.local",
    "his-hope-redis",
    "his-hope-redis.his-hope",
    "his-hope-redis.his-hope.svc",
    "his-hope-redis.his-hope.svc.cluster.local",
    *(f"his-hope-redis-{index}.his-hope-redis.his-hope.svc.cluster.local" for index in range(3)),
]
now = datetime.now(timezone.utc)
certificate = (
    x509.CertificateBuilder()
    .subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "his-hope-redis")]))
    .issuer_name(ca.subject)
    .public_key(key.public_key())
    .serial_number(x509.random_serial_number())
    .not_valid_before(now - timedelta(minutes=5))
    .not_valid_after(now + timedelta(days=825))
    .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
    .add_extension(
        x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH, ExtendedKeyUsageOID.CLIENT_AUTH]),
        critical=False,
    )
    .add_extension(x509.SubjectAlternativeName([x509.DNSName(name) for name in names]), critical=False)
    .sign(ca_key, hashes.SHA256())
)
(root / "redis_tls_key.pem").write_bytes(
    key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.TraditionalOpenSSL, serialization.NoEncryption())
)
(root / "redis_tls_cert.pem").write_bytes(certificate.public_bytes(serialization.Encoding.PEM))
print(f"Issued Redis certificate with {len(names)} DNS SANs")
