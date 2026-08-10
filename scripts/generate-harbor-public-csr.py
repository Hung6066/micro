"""Generate the Harbor public TLS private key and CSR outside the repository.

The certificate chain is intentionally not generated here: it must be issued by
the enterprise CA/ACME issuer trusted by Harbor clients.
"""

from pathlib import Path
import argparse

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID


parser = argparse.ArgumentParser()
parser.add_argument("--secure-root", default=r"D:\secure\his-hope")
args = parser.parse_args()
root = Path(args.secure_root)
root.mkdir(parents=True, exist_ok=True)
key_path = root / "harbor_public_key.pem"
csr_path = root / "harbor_public.csr.pem"

if key_path.exists() or csr_path.exists():
    raise SystemExit("Refusing to overwrite existing Harbor public key/CSR")

key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
names = [x509.DNSName("harbor.myduchospital.com")]
csr = (
    x509.CertificateSigningRequestBuilder()
    .subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "harbor.myduchospital.com")]))
    .add_extension(x509.SubjectAlternativeName(names), critical=False)
    .sign(key, hashes.SHA256())
)

key_path.write_bytes(
    key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.PKCS8,
        serialization.NoEncryption(),
    )
)
csr_path.write_bytes(csr.public_bytes(serialization.Encoding.PEM))
for path in (key_path, csr_path):
    try:
        path.chmod(0o600)
    except OSError:
        pass
print(f"Generated {key_path} and {csr_path}; submit the CSR to the trusted CA.")
