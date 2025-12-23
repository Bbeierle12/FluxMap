import hashlib
import hmac
import json
import os
import queue
import socket
import struct
import subprocess
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, HTTPServer

VERSION = "0.1.0"


def load_config(path):
    if not os.path.exists(path):
        return {
            "apiBase": "http://localhost:5000",
            "token": "",
            "hmacSecret": "",
            "registrationCode": "",
            "intervalSeconds": 30,
            "statusHost": "127.0.0.1",
            "statusPort": 8787,
            "enableMdns": True,
            "enableLlmnr": True,
            "enableSsdp": True,
            "enableArpTable": True,
            "arpIntervalSeconds": 60,
            "enableDhcpLease": False,
            "dhcpLeasePath": "/var/lib/dhcp/dhcpd.leases",
            "updateCheckFile": "",
            "updateCheckIntervalSeconds": 300,
            "queueMax": 1000,
            "batchSize": 50,
            "batchIntervalSeconds": 2,
        }
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def sign_request(secret, method, path, timestamp, body):
    message = f"{method}\n{path}\n{timestamp}\n{body}".encode("utf-8")
    digest = hmac.new(secret.encode("utf-8"), message, hashlib.sha256).hexdigest()
    return digest


def post_observation(api_base, token, hmac_secret, obs):
    body = json.dumps(obs)
    data = body.encode("utf-8")
    req = urllib.request.Request(
        f"{api_base}/api/observations",
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    if token:
        req.add_header("X-NetWatch-Token", token)
    if hmac_secret:
        timestamp = str(int(time.time()))
        signature = sign_request(hmac_secret, "POST", "/api/observations", timestamp, body)
        req.add_header("X-NetWatch-Timestamp", timestamp)
        req.add_header("X-NetWatch-Signature", signature)
    with urllib.request.urlopen(req, timeout=5) as resp:
        return resp.read().decode("utf-8")


def post_batch(api_base, token, hmac_secret, observations):
    body = json.dumps(observations)
    data = body.encode("utf-8")
    req = urllib.request.Request(
        f"{api_base}/api/observations/batch",
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    if token:
        req.add_header("X-NetWatch-Token", token)
    if hmac_secret:
        timestamp = str(int(time.time()))
        signature = sign_request(hmac_secret, "POST", "/api/observations/batch", timestamp, body)
        req.add_header("X-NetWatch-Timestamp", timestamp)
        req.add_header("X-NetWatch-Signature", signature)
    with urllib.request.urlopen(req, timeout=5) as resp:
        return resp.read().decode("utf-8")


def register_agent(api_base, code, name):
    payload = json.dumps({"code": code, "name": name}).encode("utf-8")
    req = urllib.request.Request(
        f"{api_base}/api/agent/register",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=5) as resp:
        data = json.loads(resp.read().decode("utf-8"))
        return data.get("token")


def save_config(path, cfg):
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(cfg, f, indent=2)
    except Exception:
        pass


def enqueue_observation(q, obs, status=None):
    try:
        q.put_nowait(obs)
    except queue.Full:
        try:
            q.get_nowait()
            q.put_nowait(obs)
            if status is not None:
                status["dropped"] = status.get("dropped", 0) + 1
        except Exception:
            pass


def start_sender(status, api_base, token, hmac_secret, q, batch_size, batch_interval):
    def run():
        while True:
            batch = []
            start = time.time()
            try:
                first = q.get()
                if first is None:
                    return
                batch.append(first)
                q.task_done()
            except Exception:
                continue

            while len(batch) < batch_size and (time.time() - start) < batch_interval:
                try:
                    obs = q.get(timeout=0.1)
                    if obs is None:
                        return
                    batch.append(obs)
                    q.task_done()
                except queue.Empty:
                    continue

            try:
                post_batch(api_base, token, hmac_secret, batch)
                status["lastPostUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            except Exception:
                status["errors"] += 1

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def start_status_server(status, host, port):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path == "/health":
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(b"{\"status\":\"ok\"}")
                return
            if self.path == "/stats":
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                payload = json.dumps(status).encode("utf-8")
                self.wfile.write(payload)
                return

            self.send_response(404)
            self.end_headers()

        def log_message(self, format, *args):
            return

    def run():
        httpd = HTTPServer((host, port), Handler)
        httpd.serve_forever()

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def start_udp_listener(name, group, port, q, status):
    def run():
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            sock.bind(("", port))

            if group != "255.255.255.255":
                mreq = struct.pack("=4sl", socket.inet_aton(group), socket.INADDR_ANY)
                sock.setsockopt(socket.IPPROTO_IP, socket.IP_ADD_MEMBERSHIP, mreq)

            while True:
                data, addr = sock.recvfrom(4096)
                obs = build_udp_observation(name, port, data, addr[0])
                if obs:
                    enqueue_observation(q, obs, status)
        except Exception:
            return

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def build_udp_observation(name, port, data, source_ip):
    obs = {
        "source": name,
        "ipAddress": source_ip,
        "typeHint": name,
        "serviceHint": f"udp/{port}",
    }

    if name == "ssdp-passive":
        parsed = parse_ssdp(data)
        if parsed.get("server"):
            obs["vendor"] = parsed.get("server")
        if parsed.get("st"):
            obs["serviceHint"] = parsed.get("st")
        if parsed.get("usn"):
            obs["hostname"] = parsed.get("usn")
        return obs

    if name in ("mdns", "llmnr"):
        parsed = parse_dns(data)
        if parsed.get("name"):
            obs["hostname"] = parsed.get("name")
        if parsed.get("rtype"):
            obs["serviceHint"] = parsed.get("rtype")
        return obs

    return obs


def parse_ssdp(data):
    try:
        text = data.decode("utf-8", errors="ignore")
    except Exception:
        return {}
    server = None
    st = None
    usn = None
    for line in text.splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        key = key.strip().lower()
        value = value.strip()
        if key == "server":
            server = value
        elif key == "st":
            st = value
        elif key == "usn":
            usn = value
    return {"server": server, "st": st, "usn": usn}


def parse_dns(data):
    if len(data) < 12:
        return {}
    qdcount = int.from_bytes(data[4:6], "big")
    ancount = int.from_bytes(data[6:8], "big")
    offset = 12
    name = None
    rtype = None

    if qdcount > 0:
        name, offset = read_name(data, offset)
        if offset + 4 <= len(data):
            qtype = int.from_bytes(data[offset : offset + 2], "big")
            rtype = dns_type_name(qtype)
    if not name and ancount > 0:
        name, offset = read_name(data, offset)
        if offset + 10 <= len(data):
            atype = int.from_bytes(data[offset : offset + 2], "big")
            rtype = dns_type_name(atype)

    return {"name": name, "rtype": rtype}


def read_name(data, offset, depth=0):
    if depth > 5 or offset >= len(data):
        return None, offset
    labels = []
    while offset < len(data):
        length = data[offset]
        if length == 0:
            offset += 1
            break
        if (length & 0xC0) == 0xC0:
            if offset + 1 >= len(data):
                break
            pointer = ((length & 0x3F) << 8) | data[offset + 1]
            part, _ = read_name(data, pointer, depth + 1)
            if part:
                labels.append(part)
            offset += 2
            break
        offset += 1
        if offset + length > len(data):
            break
        labels.append(data[offset : offset + length].decode("utf-8", errors="ignore"))
        offset += length
    return ".".join(labels) if labels else None, offset


def dns_type_name(value):
    return {
        1: "A",
        12: "PTR",
        16: "TXT",
        28: "AAAA",
        33: "SRV",
    }.get(value, f"TYPE{value}")


def start_arp_table_poller(interval, q, status):
    def run():
        while True:
            try:
                result = subprocess.run(
                    ["ip", "neigh"],
                    check=False,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.DEVNULL,
                    text=True,
                )
                for line in result.stdout.splitlines():
                    parts = line.split()
                    if "lladdr" in parts:
                        ip = parts[0]
                        mac = parts[parts.index("lladdr") + 1]
                        obs = {
                            "source": "arp-table",
                            "ipAddress": ip,
                            "macAddress": mac,
                            "typeHint": "arp-table",
                        }
                        enqueue_observation(q, obs, status)
            except Exception:
                pass
            time.sleep(interval)

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def parse_dhcp_leases(path, q, status):
    if not os.path.exists(path):
        return
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            current_ip = None
            current_mac = None
            for line in f:
                line = line.strip()
                if line.startswith("lease "):
                    current_ip = line.split()[1]
                elif line.startswith("hardware ethernet"):
                    current_mac = line.split()[2].rstrip(";")
                elif line == "}":
                    if current_ip and current_mac:
                        obs = {
                            "source": "dhcp-lease",
                            "ipAddress": current_ip,
                            "macAddress": current_mac,
                            "typeHint": "dhcp-lease",
                        }
                        enqueue_observation(q, obs, status)
                    current_ip = None
                    current_mac = None
    except Exception:
        return


def start_dhcp_lease_poller(path, interval, q, status):
    def run():
        while True:
            parse_dhcp_leases(path, q, status)
            time.sleep(interval)

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def start_update_checker(status, path, interval):
    def run():
        while True:
            status["lastUpdateCheckUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            status["updateAvailable"] = False
            status["updateVersion"] = None
            if path and os.path.exists(path):
                try:
                    with open(path, "r", encoding="utf-8") as f:
                        data = json.load(f)
                        version = data.get("version")
                        if version and version != VERSION:
                            status["updateAvailable"] = True
                            status["updateVersion"] = version
                except Exception:
                    pass
            time.sleep(interval)

    thread = threading.Thread(target=run, daemon=True)
    thread.start()


def main():
    config_path = os.environ.get("NETWATCH_AGENT_CONFIG", "config.json")
    cfg = load_config(config_path)
    api_base = cfg.get("apiBase", "http://localhost:5000")
    token = cfg.get("token", "")
    hmac_secret = cfg.get("hmacSecret", "")
    registration_code = cfg.get("registrationCode", "")
    interval = int(cfg.get("intervalSeconds", 30))
    status_host = cfg.get("statusHost", "127.0.0.1")
    status_port = int(cfg.get("statusPort", 8787))

    status = {
        "version": VERSION,
        "lastPostUtc": None,
        "errors": 0,
        "dropped": 0,
        "updateAvailable": False,
        "updateVersion": None,
        "lastUpdateCheckUtc": None,
    }

    if not token and registration_code:
        try:
            name = socket.gethostname()
            new_token = register_agent(api_base, registration_code, name)
            if new_token:
                cfg["token"] = new_token
                token = new_token
                save_config(config_path, cfg)
        except Exception:
            pass

    q = queue.Queue(maxsize=int(cfg.get("queueMax", 1000)))
    start_sender(
        status,
        api_base,
        token,
        hmac_secret,
        q,
        int(cfg.get("batchSize", 50)),
        int(cfg.get("batchIntervalSeconds", 2)),
    )
    start_status_server(status, status_host, status_port)

    if cfg.get("enableMdns", True):
        start_udp_listener("mdns", "224.0.0.251", 5353, q, status)
    if cfg.get("enableLlmnr", True):
        start_udp_listener("llmnr", "224.0.0.252", 5355, q, status)
    if cfg.get("enableSsdp", True):
        start_udp_listener("ssdp-passive", "239.255.255.250", 1900, q, status)
    if cfg.get("enableArpTable", True):
        start_arp_table_poller(int(cfg.get("arpIntervalSeconds", 60)), q, status)
    if cfg.get("enableDhcpLease", False):
        path = cfg.get("dhcpLeasePath", "/var/lib/dhcp/dhcpd.leases")
        start_dhcp_lease_poller(path, int(cfg.get("arpIntervalSeconds", 60)), q, status)
    start_update_checker(
        status,
        cfg.get("updateCheckFile", ""),
        int(cfg.get("updateCheckIntervalSeconds", 300)),
    )

    backoff = 1
    while True:
        obs = {
            "source": "kali-agent",
            "hostname": "kali-agent",
            "typeHint": "defensive-sensor",
        }
        try:
            enqueue_observation(q, obs, status)
            backoff = 1
            time.sleep(interval)
        except Exception:
            status["errors"] += 1
            time.sleep(min(backoff, 60))
            backoff = min(backoff * 2, 60)


if __name__ == "__main__":
    main()
