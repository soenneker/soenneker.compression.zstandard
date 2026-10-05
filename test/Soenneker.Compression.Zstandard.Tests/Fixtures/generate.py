# Regenerate with Python 3.14's independent libzstd implementation.
from compression import zstd
from pathlib import Path
root = Path(__file__).parent
payload = ''.join(f'{{"event":{i},"field":"email","value":"person{i % 37}@example.com","label":"héllo 🚀"}}\n' for i in range(6000)).encode()
for level in (1, 6, 19):
    compressor = zstd.ZstdCompressor(options={zstd.CompressionParameter.compression_level: level, zstd.CompressionParameter.checksum_flag: 1})
    (root / f'reference-{level}.zst').write_bytes(compressor.compress(payload) + compressor.flush())
