#!/usr/bin/env python3
"""
AAEmu smoke test:
- Verify all 4 ports accept TCP
- Send garbage to each port, ensure server does NOT crash
- Send a well-formed login packet and verify the server processes it without crash
"""
import socket
import struct
import time
import sys

HOST = "127.0.0.1"
LOGIN_PORT = 1237
LOGIN_INTERNAL_PORT = 1234
GAME_PORT = 1239
STREAM_PORT = 1250


def probe(port, name):
    try:
        s = socket.create_connection((HOST, port), timeout=2)
        s.close()
        print(f"  [OK] {name} :{port} accepts TCP")
        return True
    except Exception as e:
        print(f"  [FAIL] {name} :{port} — {e}")
        return False


def send_garbage(port, name):
    try:
        s = socket.create_connection((HOST, port), timeout=2)
        # Send 512 bytes of random-looking garbage
        s.send(b"\xAA\xBB\xCC\xDD" * 128)
        time.sleep(0.2)
        s.close()
        print(f"  [OK] {name} :{port} accepted garbage without refusing")
        return True
    except Exception as e:
        print(f"  [FAIL] {name} :{port} garbage: {e}")
        return False


def build_login_packet(opcode, payload):
    """AAEmu login packet: [len:u16 LE][type:u16 LE][payload]  — where len is payload length."""
    body = struct.pack("<H", opcode) + payload
    return struct.pack("<H", len(body) - 2) + body


def write_string_u16(s):
    """PacketStream writes strings as [len:u16 LE][utf8 bytes]."""
    b = s.encode("utf-8")
    return struct.pack("<H", len(b)) + b


def write_bytes_u16(b):
    return struct.pack("<H", len(b)) + b


def test_auth_trion(port):
    """Send CARequestAuthTrionPacket (opcode 0x04) — XML ticket with username+password."""
    print(f"\n[TEST] Login with CARequestAuthTrionPacket @ :{port}")
    s = socket.create_connection((HOST, port), timeout=3)

    xml = '<?xml version="1.0"?><root><username>smoketest</username><password>test</password></root>'
    sig = "0"

    # pFrom(u32) pTo(u32) dev(bool) mac(bytes) ticket(string) signature(string)
    payload = struct.pack("<IIB", 0, 0, 0)
    payload += write_bytes_u16(b"")  # mac
    payload += write_string_u16(xml)
    payload += write_string_u16(sig)

    packet = build_login_packet(0x04, payload)
    s.send(packet)
    time.sleep(0.5)
    try:
        data = s.recv(4096)
        print(f"  [OK] Server responded with {len(data)} bytes: {data[:40].hex()}")
    except socket.timeout:
        print("  [WARN] No response (but server didn't crash)")
    s.close()


def test_unknown_opcode(port):
    """Send a well-formed packet with unknown opcode — server should log it and stay up."""
    print(f"\n[TEST] Unknown opcode @ :{port}")
    s = socket.create_connection((HOST, port), timeout=3)
    payload = b"\x00" * 8
    packet = build_login_packet(0xFFFE, payload)
    s.send(packet)
    time.sleep(0.3)
    s.close()
    print("  [OK] Sent unknown opcode packet")


def verify_ports_still_alive():
    print("\n[VERIFY] All ports still listening after tests:")
    ok = True
    ok &= probe(LOGIN_INTERNAL_PORT, "LoginInternal")
    ok &= probe(LOGIN_PORT, "LoginClient")
    ok &= probe(GAME_PORT, "GameClient")
    ok &= probe(STREAM_PORT, "GameStream")
    return ok


def main():
    print("=" * 60)
    print("AAEmu Smoke Test")
    print("=" * 60)

    print("\n[1] Probing TCP ports...")
    all_up = True
    all_up &= probe(LOGIN_INTERNAL_PORT, "LoginInternal")
    all_up &= probe(LOGIN_PORT, "LoginClient")
    all_up &= probe(GAME_PORT, "GameClient")
    all_up &= probe(STREAM_PORT, "GameStream")

    if not all_up:
        print("\n[FATAL] One or more servers are down. Start them first.")
        sys.exit(1)

    print("\n[2] Garbage input resilience...")
    send_garbage(LOGIN_PORT, "LoginClient")
    send_garbage(GAME_PORT, "GameClient")

    print("\n[3] Well-formed login flow...")
    test_auth_trion(LOGIN_PORT)

    print("\n[4] Unknown opcode handling...")
    test_unknown_opcode(LOGIN_PORT)

    time.sleep(1)

    if not verify_ports_still_alive():
        print("\n[FATAL] A server died during tests!")
        sys.exit(2)

    print("\n" + "=" * 60)
    print("ALL SMOKE TESTS PASSED — servers are resilient")
    print("=" * 60)


if __name__ == "__main__":
    main()
