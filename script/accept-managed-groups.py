#!/usr/bin/env python3
"""Exercise a generated sing-box managed group with isolated loopback fixtures."""

import argparse
import json
import os
import socket
import struct
import subprocess
import tempfile
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


def free_ports(count):
    sockets = [socket.socket() for _ in range(count)]
    try:
        for sock in sockets:
            sock.bind(("127.0.0.1", 0))
        return [sock.getsockname()[1] for sock in sockets]
    finally:
        for sock in sockets:
            sock.close()


def exact(sock, size):
    data = bytearray()
    while len(data) < size:
        part = sock.recv(size - len(data))
        if not part:
            raise RuntimeError("SOCKS connection closed early")
        data.extend(part)
    return bytes(data)


def connect_socks(port, command, target_port):
    sock = socket.create_connection(("127.0.0.1", port), timeout=3)
    sock.sendall(b"\x05\x01\x00")
    if exact(sock, 2) != b"\x05\x00":
        raise RuntimeError("SOCKS5 no-auth greeting rejected")
    sock.sendall(b"\x05" + bytes([command]) + b"\x00\x01\x7f\x00\x00\x01" + struct.pack("!H", target_port))
    header = exact(sock, 4)
    if header[:2] != b"\x05\x00":
        raise RuntimeError(f"SOCKS5 request failed: {header!r}")
    size = {1: 4, 4: 16}.get(header[3])
    if size is None:
        raise RuntimeError(f"Unsupported SOCKS5 address type {header[3]}")
    address = exact(sock, size)
    relay_port = struct.unpack("!H", exact(sock, 2))[0]
    relay_host = socket.inet_ntop(socket.AF_INET if size == 4 else socket.AF_INET6, address)
    return sock, (relay_host, relay_port)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("core", type=Path, help="path to sing-box executable")
    parser.add_argument("config", type=Path, help="generated managed-group frontend JSON")
    args = parser.parse_args()
    core = args.core.resolve()
    front = json.loads(args.config.read_text(encoding="utf-8"))
    socks_outbounds = [item for item in front["outbounds"] if item["type"] == "socks"]
    if len(socks_outbounds) < 2:
        raise ValueError("The frontend must contain at least two managed SOCKS outbounds")
    selector = next(item for item in front["outbounds"]
                    if item["type"] == "selector" and socks_outbounds[0]["tag"] in item["outbounds"])

    ports = free_ports(len(socks_outbounds) + 3)
    front_port, api_port, health_port, *backend_ports = ports
    front["inbounds"][0]["listen"] = "127.0.0.1"
    front["inbounds"][0]["listen_port"] = front_port
    for outbound, backend_port in zip(socks_outbounds, backend_ports, strict=True):
        outbound["server"] = "127.0.0.1"
        outbound["server_port"] = backend_port
    front["experimental"]["clash_api"]["external_controller"] = f"127.0.0.1:{api_port}"
    auto = next(item for item in front["outbounds"] if item["type"] == "urltest")
    auto["url"] = f"http://127.0.0.1:{health_port}/health"
    auto["interval"] = "1s"

    env = {key: value for key, value in os.environ.items() if key.lower() not in {
        "http_proxy", "https_proxy", "all_proxy", "no_proxy"
    }}
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    processes = []
    temp = tempfile.TemporaryDirectory(prefix="v2rayn-managed-acceptance-")
    root = Path(temp.name)
    health = None

    def api(path, payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        request = urllib.request.Request(
            f"http://127.0.0.1:{api_port}{path}", data=data,
            method="PUT" if payload is not None else "GET",
            headers={"Content-Type": "application/json"},
        )
        with opener.open(request, timeout=3) as response:
            body = response.read()
            return json.loads(body) if body else None

    def launch(name, config):
        path = root / f"{name}.json"
        path.write_text(json.dumps(config), encoding="utf-8")
        subprocess.run([str(core), "check", "-c", str(path)], check=True, env=env)
        log = (root / f"{name}.log").open("w", encoding="utf-8")
        process = subprocess.Popen([str(core), "run", "-c", str(path)], env=env, stdout=log, stderr=log)
        log.close()
        processes.append(process)
        return process

    try:
        class Health(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(200)
                self.send_header("Content-Length", "2")
                self.end_headers()
                self.wfile.write(b"ok")

            def log_message(self, *_args):
                pass

        health = ThreadingHTTPServer(("127.0.0.1", health_port), Health)
        threading.Thread(target=health.serve_forever, daemon=True).start()
        for index, backend_port in enumerate(backend_ports):
            launch(f"backend-{index}", {
                "log": {"level": "info"},
                "inbounds": [{"type": "socks", "tag": "in", "listen": "127.0.0.1", "listen_port": backend_port}],
                "outbounds": [{"type": "direct", "tag": "direct"}],
                "route": {"final": "direct"},
            })
        launch("frontend", front)

        deadline = time.monotonic() + 10
        while True:
            try:
                api("/proxies")
                break
            except Exception:
                if time.monotonic() >= deadline:
                    raise
                time.sleep(0.1)

        tcp_members, udp_members = [], []
        for index, outbound in enumerate(socks_outbounds):
            tag = outbound["tag"]
            api(f"/proxies/{selector['tag']}", {"name": tag})
            with socket.socket() as listener:
                listener.bind(("127.0.0.1", 0))
                listener.listen()
                listener.settimeout(3)
                client, _ = connect_socks(front_port, 1, listener.getsockname()[1])
                client.sendall(b"tcp-body")
                with client, listener.accept()[0] as peer:
                    if exact(peer, 8) != b"tcp-body":
                        raise AssertionError("TCP request body mismatch")
                    peer.sendall(b"tcp-ok")
                    if exact(client, 6) != b"tcp-ok":
                        raise AssertionError("TCP response body mismatch")
                target = f"127.0.0.1:{listener.getsockname()[1]}"
                log = (root / f"backend-{index}.log").read_text(encoding="utf-8")
                if target not in log:
                    raise AssertionError(f"{tag} did not carry the expected TCP target")
                tcp_members.append(tag)

            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as echo:
                echo.bind(("127.0.0.1", 0))
                echo.settimeout(3)
                control, relay = connect_socks(front_port, 3, 0)
                with control, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
                    udp.settimeout(3)
                    packet = b"\0\0\0\1\x7f\0\0\1" + struct.pack("!H", echo.getsockname()[1]) + b"udp-body"
                    udp.sendto(packet, relay)
                    body, address = echo.recvfrom(2048)
                    if body != b"udp-body":
                        raise AssertionError("UDP request body mismatch")
                    udp_members.append(tag)
                    echo.sendto(b"udp-ok", address)
                    response, _ = udp.recvfrom(2048)
                    if not response.endswith(b"udp-ok"):
                        raise AssertionError("UDP response body mismatch")
                target = f"127.0.0.1:{echo.getsockname()[1]}"
                log = (root / f"backend-{index}.log").read_text(encoding="utf-8")
                if target not in log:
                    raise AssertionError(f"{tag} did not carry the expected UDP target")

        auto_tag = auto["tag"]
        api(f"/proxies/{selector['tag']}", {"name": auto_tag})
        control, _ = connect_socks(front_port, 1, health_port)
        with control:
            control.sendall(b"GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n")
            if b"200" not in control.recv(1024):
                raise AssertionError("Health request through the managed group failed")
        time.sleep(1.2)
        selected = api(f"/proxies/{auto_tag}")["now"]
        failed_index = next(i for i, item in enumerate(socks_outbounds) if item["tag"] == selected)
        processes[failed_index].terminate()
        processes[failed_index].wait(timeout=3)
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            surviving = api(f"/proxies/{auto_tag}")["now"]
            if surviving != selected:
                break
            time.sleep(0.2)
        if surviving == selected:
            raise AssertionError("urltest did not fail over after its selected backend exited")

        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            listener.settimeout(3)
            client, _ = connect_socks(front_port, 1, listener.getsockname()[1])
            client.sendall(b"failover")
            with client, listener.accept()[0] as peer:
                if exact(peer, 8) != b"failover":
                    raise AssertionError("Failover request body mismatch")
                peer.sendall(b"ok")
                if exact(client, 2) != b"ok":
                    raise AssertionError("Failover response body mismatch")
            target = f"127.0.0.1:{listener.getsockname()[1]}"
            surviving_index = next(i for i, item in enumerate(socks_outbounds) if item["tag"] == surviving)
            if target not in (root / f"backend-{surviving_index}.log").read_text(encoding="utf-8"):
                raise AssertionError("Failover body did not pass through the surviving backend")
        print(json.dumps({
            "tcp_members": tcp_members,
            "udp_members": udp_members,
            "automatic_failover": [selected, surviving],
            "native_protocol_transport": "not tested",
        }, sort_keys=True))
    finally:
        for process in reversed(processes):
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
        if health is not None:
            health.shutdown()
            health.server_close()
        temp.cleanup()


if __name__ == "__main__":
    main()
