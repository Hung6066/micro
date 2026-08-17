"""Issue a Vault server certificate for the production K3s service names.

The CA and generated key material stay outside the repository under D:\\secure.
This is an operator bootstrap utility, not application configuration.
"""

from datetime import datetime, timedelta, timezone
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID


ROOT = Path(r"D:\secure\his-hope")
CA_CERT = ROOT / "his_hope_ca.pem"
CA_KEY = ROOT / "his_hope_ca.key"
OUT_CERT = ROOT / "vault_k3s_cert.pem"
OUT_KEY = ROOT / "vault_k3s_key.pem"

ca_cert = x509.load_pem_x509_certificate(CA_CERT.read_bytes())
ca_key = serialization.load_pem_private_key(CA_KEY.read_bytes(), password=None)
key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

names = [
    "vault-active",
    "vault-active.his-hope",
    "vault-active.his-hope.svc",
    "vault-active.his-hope.svc.cluster.local",
    "vault-internal",
    "vault-internal.his-hope",
    "vault-internal.his-hope.svc",
    "vault-internal.his-hope.svc.cluster.local",
    "vault-0.vault-internal.his-hope.svc.cluster.local",
    "vault-1.vault-internal.his-hope.svc.cluster.local",
    "vault-2.vault-internal.his-hope.svc.cluster.local",
    "localhost",
]

subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, names[0])])
now = datetime.now(timezone.utc)
certificate = (
    x509.CertificateBuilder()
    .subject_name(subject)
    .issuer_name(ca_cert.subject)
    .public_key(key.public_key())
    .serial_number(x509.random_serial_number())
    .not_valid_before(now - timedelta(minutes=5))
    .not_valid_after(now + timedelta(days=825))
    .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
    .add_extension(
        x509.KeyUsage(
            digital_signature=True,
            key_encipherment=True,
            content_commitment=False,
            data_encipherment=False,
            key_agreement=False,
            key_cert_sign=False,
            crl_sign=False,
            encipher_only=False,
            decipher_only=False,
        ),
        critical=True,
    )
    .add_extension(
        x509.ExtendedKeyUsage(
            [ExtendedKeyUsageOID.SERVER_AUTH, ExtendedKeyUsageOID.CLIENT_AUTH]
        ),
        critical=False,
    )
    .add_extension(
        x509.SubjectAlternativeName([x509.DNSName(name) for name in names]),
        critical=False,
    )
    .sign(ca_key, hashes.SHA256())
)

OUT_KEY.write_bytes(
    key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.TraditionalOpenSSL,
        serialization.NoEncryption(),
    )
)
OUT_CERT.write_bytes(certificate.public_bytes(serialization.Encoding.PEM))
print(f"Issued {OUT_CERT} with {len(names)} production DNS SANs")
