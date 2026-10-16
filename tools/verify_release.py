"""Inspect PE/CLR bytes without loading or executing the inspected EXE.

Compare an independently compiled EXE, allowing only the PE build timestamp
and CLR module version ID to differ. No modified executable is written.
Python 3 standard library only.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import struct
import zipfile


def sha(data):
    return hashlib.sha256(data).hexdigest()


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


def u32(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def inspect(data):
    pe = u32(data, 0x3C)
    if data[:2] != b"MZ" or data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Not a PE image")
    optional = pe + 24
    magic = u16(data, optional)
    directory = optional + {0x10B: 96, 0x20B: 112}[magic]
    sections = optional + u16(data, pe + 20)

    def raw(rva):
        for index in range(u16(data, pe + 6)):
            section = sections + index * 40
            size, start, raw_size, pointer = struct.unpack_from("<IIII", data, section + 8)
            if start <= rva < start + max(size, raw_size):
                offset = pointer + rva - start
                if not 0 <= offset < len(data):
                    raise ValueError("Invalid section offset")
                return offset
        raise ValueError("RVA is outside the image sections")

    clr = raw(u32(data, directory + 14 * 8))
    metadata = raw(u32(data, clr + 8))
    if data[metadata:metadata + 4] != b"BSJB":
        raise ValueError("Invalid CLR metadata")
    storage = metadata + 16 + u32(data, metadata + 12)
    stream_count = u16(data, storage + 2)
    cursor = storage + 4
    streams = {}
    for _ in range(stream_count):
        relative, size = struct.unpack_from("<II", data, cursor)
        name_start = cursor + 8
        end = data.index(0, name_start)
        name = data[name_start:end].decode("ascii")
        streams[name] = (metadata + relative, size)
        cursor = name_start + ((end - name_start + 1 + 3) // 4) * 4
    tables, _ = streams["#~"]
    heaps = data[tables + 6]
    valid = struct.unpack_from("<Q", data, tables + 8)[0]
    if not valid & 1 or u32(data, tables + 24) != 1:
        raise ValueError("Expected one CLR Module row")
    row = tables + 24 + valid.bit_count() * 4
    string_width = 4 if heaps & 1 else 2
    guid_width = 4 if heaps & 2 else 2
    mvid_index = int.from_bytes(data[row + 2 + string_width:row + 2 + string_width + guid_width], "little")
    guid_heap, guid_size = streams["#GUID"]
    mvid = guid_heap + (mvid_index - 1) * 16
    if mvid_index < 1 or mvid + 16 > guid_heap + guid_size:
        raise ValueError("Invalid MVID")
    normalized = bytearray(data)
    normalized[pe + 8:pe + 12] = bytes(4)
    normalized[mvid:mvid + 16] = bytes(16)

    resource = raw(u32(data, clr + 24))
    resource_size = u32(data, clr + 28)
    jar_size = u32(data, resource)
    if not 0 < jar_size <= resource_size - 4 or resource + resource_size > len(data):
        raise ValueError("Invalid embedded resource")
    if any(data[resource + 4 + jar_size:resource + resource_size]):
        raise ValueError("Unexpected additional nonzero resource data")
    jar = data[resource + 4:resource + 4 + jar_size]
    return bytes(normalized), jar


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original", type=Path)
    parser.add_argument("rebuilt", type=Path)
    parser.add_argument("agent", type=Path)
    parser.add_argument("asm", type=Path)
    parser.add_argument("compiled_classes", type=Path)
    args = parser.parse_args()
    original = args.original.read_bytes()
    rebuilt = args.rebuilt.read_bytes()
    original_normalized, embedded = inspect(original)
    rebuilt_normalized, rebuilt_embedded = inspect(rebuilt)
    expected_asm = "6f3828a215c920059a5efa2fb55c233d6c54ec5cadca99ce1b1bdd10077c7ddd"
    if sha(args.asm.read_bytes()) != expected_asm:
        raise ValueError("ASM dependency checksum mismatch")
    with zipfile.ZipFile(io.BytesIO(embedded)) as jar, zipfile.ZipFile(args.asm) as vendor:
        if sum(entry.file_size for entry in jar.infolist()) > 50_000_000:
            raise ValueError("Unexpectedly large JAR contents")
        classes = sorted(name for name in jar.namelist() if name.startswith("dev/") and name.endswith(".class"))
        if not classes:
            raise ValueError("Missing agent classes")
        for name in classes:
            if jar.read(name) != (args.compiled_classes / name).read_bytes():
                raise ValueError("Agent source/bytecode mismatch: " + name)
        library = sorted(name for name in jar.namelist() if name.startswith("org/objectweb/asm/") and not name.endswith("/"))
        if not library:
            raise ValueError("Missing ASM classes")
        for name in library:
            if jar.read(name) != vendor.read(name):
                raise ValueError("ASM dependency mismatch: " + name)
        unexpected = [name for name in jar.namelist() if not name.endswith("/")
                      and name not in classes + library + ["META-INF/MANIFEST.MF", "LICENSE", "THIRD_PARTY.txt"]]
        if unexpected:
            raise ValueError("Unexpected JAR entries: " + str(unexpected))
    report = {
        "release_exe_sha256": sha(original),
        "release_exe_bytes": len(original),
        "rebuilt_exe_sha256": sha(rebuilt),
        "normalized_exe_sha256": sha(original_normalized),
        "rebuilt_normalized_exe_sha256": sha(rebuilt_normalized),
        "exe_matches_rebuild_except_pe_timestamp_and_clr_mvid": original_normalized == rebuilt_normalized,
        "embedded_agent_sha256": sha(embedded),
        "embedded_agent_matches_published_jar": embedded == args.agent.read_bytes(),
        "rebuilt_exe_embeds_same_agent": embedded == rebuilt_embedded,
        "agent_classes_matching_independent_javac_output": classes,
        "asm_dependency_sha256": expected_asm,
        "bundled_asm_entries_matching_dependency": len(library),
        "limitation": "Integrity/source correspondence evidence, not a malware verdict or antivirus clearance."
    }
    print(json.dumps(report, indent=2))
    if not all(report[key] for key in (
            "exe_matches_rebuild_except_pe_timestamp_and_clr_mvid",
            "embedded_agent_matches_published_jar", "rebuilt_exe_embeds_same_agent")):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
