const apiBase = "http://localhost:5000";
const deviceList = document.getElementById("deviceList");
const deviceName = document.getElementById("deviceName");
const deviceMeta = document.getElementById("deviceMeta");
const timeline = document.getElementById("timeline");
const deviceEvidence = document.getElementById("deviceEvidence");
const deviceTimeline = document.getElementById("deviceTimeline");
const deviceClassification = document.getElementById("deviceClassification");
const deviceRisk = document.getElementById("deviceRisk");
const deviceStats = document.getElementById("deviceStats");
const deviceSignals = document.getElementById("deviceSignals");
const deviceHistory = document.getElementById("deviceHistory");
const canvas = document.getElementById("netCanvas");
const ctx = canvas.getContext("2d");
const devicesTable = document.getElementById("devicesTable");
const settingsStatus = document.getElementById("settingsStatus");
const connectorsStatus = document.getElementById("connectorsStatus");
const connectorsState = document.getElementById("connectorsState");
const credStatus = document.getElementById("credStatus");
const fingerprintStatus = document.getElementById("fingerprintStatus");
const credList = document.getElementById("credList");
const networkSummary = document.getElementById("networkSummary");
const deviceCache = new Map();
const eventCache = [];
const nodePositions = new Map();

const connUpnp = document.getElementById("connUpnp");
const connSnmp = document.getElementById("connSnmp");
const connDhcp = document.getElementById("connDhcp");
const connUnifi = document.getElementById("connUnifi");
const connTpLink = document.getElementById("connTpLink");
const connNetgear = document.getElementById("connNetgear");
const connOrbi = document.getElementById("connOrbi");
const connOmada = document.getElementById("connOmada");
const connAsus = document.getElementById("connAsus");
const snmpHosts = document.getElementById("snmpHosts");
const snmpCommunity = document.getElementById("snmpCommunity");
const snmpCommunityCred = document.getElementById("snmpCommunityCred");
const snmpPort = document.getElementById("snmpPort");
const snmpWalkPath = document.getElementById("snmpWalkPath");
const snmpTimeout = document.getElementById("snmpTimeout");
const dhcpUrl = document.getElementById("dhcpUrl");
const dhcpHeader = document.getElementById("dhcpHeader");
const dhcpAuthValue = document.getElementById("dhcpAuthValue");
const dhcpAuthCred = document.getElementById("dhcpAuthCred");
const unifiBaseUrl = document.getElementById("unifiBaseUrl");
const unifiSite = document.getElementById("unifiSite");
const unifiUser = document.getElementById("unifiUser");
const unifiPass = document.getElementById("unifiPass");
const unifiPassCred = document.getElementById("unifiPassCred");
const unifiSkipTls = document.getElementById("unifiSkipTls");
const tplinkUrl = document.getElementById("tplinkUrl");
const tplinkHeader = document.getElementById("tplinkHeader");
const tplinkAuthValue = document.getElementById("tplinkAuthValue");
const tplinkAuthCred = document.getElementById("tplinkAuthCred");
const tplinkFormat = document.getElementById("tplinkFormat");
const tplinkFields = document.getElementById("tplinkFields");
const tplinkCols = document.getElementById("tplinkCols");
const netgearUrl = document.getElementById("netgearUrl");
const netgearHeader = document.getElementById("netgearHeader");
const netgearAuthValue = document.getElementById("netgearAuthValue");
const netgearAuthCred = document.getElementById("netgearAuthCred");
const netgearFormat = document.getElementById("netgearFormat");
const netgearFields = document.getElementById("netgearFields");
const netgearCols = document.getElementById("netgearCols");
const orbiUrl = document.getElementById("orbiUrl");
const orbiHeader = document.getElementById("orbiHeader");
const orbiAuthValue = document.getElementById("orbiAuthValue");
const orbiAuthCred = document.getElementById("orbiAuthCred");
const orbiFormat = document.getElementById("orbiFormat");
const orbiFields = document.getElementById("orbiFields");
const orbiCols = document.getElementById("orbiCols");
const omadaUrl = document.getElementById("omadaUrl");
const omadaHeader = document.getElementById("omadaHeader");
const omadaAuthValue = document.getElementById("omadaAuthValue");
const omadaAuthCred = document.getElementById("omadaAuthCred");
const omadaFormat = document.getElementById("omadaFormat");
const omadaFields = document.getElementById("omadaFields");
const omadaCols = document.getElementById("omadaCols");
const asusUrl = document.getElementById("asusUrl");
const asusHeader = document.getElementById("asusHeader");
const asusAuthValue = document.getElementById("asusAuthValue");
const asusAuthCred = document.getElementById("asusAuthCred");
const asusFormat = document.getElementById("asusFormat");
const asusFields = document.getElementById("asusFields");
const asusCols = document.getElementById("asusCols");
const saveConnectors = document.getElementById("saveConnectors");
const credName = document.getElementById("credName");
const credPurpose = document.getElementById("credPurpose");
const credSecret = document.getElementById("credSecret");
const createCred = document.getElementById("createCred");

const filterType = document.getElementById("filterType");
const filterVendor = document.getElementById("filterVendor");
const filterStatus = document.getElementById("filterStatus");
const searchInput = document.getElementById("searchInput");
const eventFilter = document.getElementById("eventFilter");
const eventTimeline = document.getElementById("eventTimeline");

const scanInterval = document.getElementById("scanInterval");
const pingTimeout = document.getElementById("pingTimeout");
const tcpTimeout = document.getElementById("tcpTimeout");
const maxPings = document.getElementById("maxPings");
const maxHosts = document.getElementById("maxHosts");
const tcpPorts = document.getElementById("tcpPorts");
const enableSsdp = document.getElementById("enableSsdp");
const saveSettings = document.getElementById("saveSettings");
const navItems = document.querySelectorAll(".nav-item[data-view]");
const views = document.querySelectorAll(".view[data-view]");
const topTitle = document.querySelector(".topbar .title");

function resizeCanvas() {
  const rect = canvas.getBoundingClientRect();
  canvas.width = rect.width;
  canvas.height = rect.height;
}

window.addEventListener("resize", resizeCanvas);
resizeCanvas();

function setView(name) {
  views.forEach((v) => {
    v.classList.toggle("hidden", v.dataset.view !== name);
  });
  navItems.forEach((n) => {
    n.classList.toggle("active", n.dataset.view === name);
  });
  if (topTitle) {
    if (name === "timeline") {
      topTitle.textContent = "Network Timeline";
    } else if (name === "devices") {
      topTitle.textContent = "Devices";
    } else {
      topTitle.textContent = "Network Map";
    }
  }
}

async function loadDevices() {
  const res = await fetch(`${apiBase}/api/devices`);
  const devices = await res.json();
  deviceCache.clear();
  devices.forEach((d) => deviceCache.set(d.deviceId, d));
  refreshFilters();
  renderFromCache();
}

async function loadEvents() {
  const res = await fetch(`${apiBase}/api/events`);
  const events = await res.json();
  eventCache.length = 0;
  events.forEach((e) => eventCache.push(e));
  renderTimeline();
  renderEventTimeline();
}

async function loadSettings() {
  const res = await fetch(`${apiBase}/api/settings`);
  const settings = await res.json();
  scanInterval.value = settings.scanIntervalSeconds ?? 60;
  pingTimeout.value = settings.pingTimeoutMs ?? 800;
  tcpTimeout.value = settings.tcpConnectTimeoutMs ?? 300;
  maxPings.value = settings.maxConcurrentPings ?? 64;
  maxHosts.value = settings.maxHostsPerSubnet ?? 1024;
  tcpPorts.value = (settings.tcpPorts || []).join(",");
  enableSsdp.checked = !!settings.enableSsdp;
}

async function loadCredentials() {
  const res = await fetch(`${apiBase}/api/credentials`);
  const creds = await res.json();
  fillCredentialSelect(snmpCommunityCred, creds);
  fillCredentialSelect(dhcpAuthCred, creds);
  fillCredentialSelect(unifiPassCred, creds);
  fillCredentialSelect(tplinkAuthCred, creds);
  fillCredentialSelect(netgearAuthCred, creds);
  fillCredentialSelect(orbiAuthCred, creds);
  fillCredentialSelect(omadaAuthCred, creds);
  fillCredentialSelect(asusAuthCred, creds);
  renderCredentialList(creds);
  return creds;
}

async function loadConnectors() {
  const res = await fetch(`${apiBase}/api/connectors`);
  const settings = await res.json();
  connUpnp.checked = !!settings.enabled?.["upnp-igd"];
  connSnmp.checked = !!settings.enabled?.["snmp"];
  connDhcp.checked = !!settings.enabled?.["dhcp-http"];
  connUnifi.checked = !!settings.enabled?.["unifi"];
  connTpLink.checked = !!settings.enabled?.["tplink"];
  connNetgear.checked = !!settings.enabled?.["netgear"];
  connOrbi.checked = !!settings.enabled?.["orbi"];
  connOmada.checked = !!settings.enabled?.["omada"];
  connAsus.checked = !!settings.enabled?.["asus"];
  snmpHosts.value = (settings.snmp?.hosts || []).join(",");
  snmpCommunity.value = settings.snmp?.community || "public";
  snmpPort.value = settings.snmp?.port ?? 161;
  snmpWalkPath.value = settings.snmp?.snmpWalkPath || "snmpwalk";
  snmpTimeout.value = settings.snmp?.timeoutSeconds ?? 3;
  dhcpUrl.value = settings.dhcpHttp?.url || "";
  dhcpHeader.value = settings.dhcpHttp?.authHeader || "";
  dhcpAuthValue.value = settings.dhcpHttp?.authValue || "";
  snmpCommunityCred.value = settings.snmp?.communityCredentialId || "";
  dhcpAuthCred.value = settings.dhcpHttp?.authValueCredentialId || "";
  unifiBaseUrl.value = settings.unifi?.baseUrl || "";
  unifiSite.value = settings.unifi?.site || "default";
  unifiUser.value = settings.unifi?.username || "";
  unifiPass.value = settings.unifi?.password || "";
  unifiPassCred.value = settings.unifi?.passwordCredentialId || "";
  unifiSkipTls.checked = !!settings.unifi?.skipTlsVerify;
  tplinkUrl.value = settings.tpLink?.url || "";
  tplinkHeader.value = settings.tpLink?.authHeader || "";
  tplinkAuthValue.value = settings.tpLink?.authValue || "";
  tplinkAuthCred.value = settings.tpLink?.authValueCredentialId || "";
  tplinkFormat.value = settings.tpLink?.format || "json";
  tplinkFields.value = [settings.tpLink?.ipField, settings.tpLink?.macField, settings.tpLink?.hostField]
    .filter(Boolean)
    .join(",");
  tplinkCols.value = [settings.tpLink?.ipColumn, settings.tpLink?.macColumn, settings.tpLink?.hostColumn]
    .filter((v) => v !== undefined && v !== null)
    .join(",");
  netgearUrl.value = settings.netgear?.url || "";
  netgearHeader.value = settings.netgear?.authHeader || "";
  netgearAuthValue.value = settings.netgear?.authValue || "";
  netgearAuthCred.value = settings.netgear?.authValueCredentialId || "";
  netgearFormat.value = settings.netgear?.format || "json";
  netgearFields.value = [settings.netgear?.ipField, settings.netgear?.macField, settings.netgear?.hostField]
    .filter(Boolean)
    .join(",");
  netgearCols.value = [settings.netgear?.ipColumn, settings.netgear?.macColumn, settings.netgear?.hostColumn]
    .filter((v) => v !== undefined && v !== null)
    .join(",");
  orbiUrl.value = settings.orbi?.url || "";
  orbiHeader.value = settings.orbi?.authHeader || "";
  orbiAuthValue.value = settings.orbi?.authValue || "";
  orbiAuthCred.value = settings.orbi?.authValueCredentialId || "";
  orbiFormat.value = settings.orbi?.format || "json";
  orbiFields.value = [settings.orbi?.ipField, settings.orbi?.macField, settings.orbi?.hostField]
    .filter(Boolean)
    .join(",");
  orbiCols.value = [settings.orbi?.ipColumn, settings.orbi?.macColumn, settings.orbi?.hostColumn]
    .filter((v) => v !== undefined && v !== null)
    .join(",");
  omadaUrl.value = settings.omada?.url || "";
  omadaHeader.value = settings.omada?.authHeader || "";
  omadaAuthValue.value = settings.omada?.authValue || "";
  omadaAuthCred.value = settings.omada?.authValueCredentialId || "";
  omadaFormat.value = settings.omada?.format || "json";
  omadaFields.value = [settings.omada?.ipField, settings.omada?.macField, settings.omada?.hostField]
    .filter(Boolean)
    .join(",");
  omadaCols.value = [settings.omada?.ipColumn, settings.omada?.macColumn, settings.omada?.hostColumn]
    .filter((v) => v !== undefined && v !== null)
    .join(",");
  asusUrl.value = settings.asus?.url || "";
  asusHeader.value = settings.asus?.authHeader || "";
  asusAuthValue.value = settings.asus?.authValue || "";
  asusAuthCred.value = settings.asus?.authValueCredentialId || "";
  asusFormat.value = settings.asus?.format || "json";
  asusFields.value = [settings.asus?.ipField, settings.asus?.macField, settings.asus?.hostField]
    .filter(Boolean)
    .join(",");
  asusCols.value = [settings.asus?.ipColumn, settings.asus?.macColumn, settings.asus?.hostColumn]
    .filter((v) => v !== undefined && v !== null)
    .join(",");
}

async function loadConnectorStatus() {
  const res = await fetch(`${apiBase}/api/connectors/status`);
  const status = await res.json();
  if (!Array.isArray(status) || status.length === 0) {
    connectorsState.textContent = "No connector status yet.";
    return;
  }
  const lines = status.map((s) => {
    const ok = s.lastSuccessUtc ? `ok ${new Date(s.lastSuccessUtc).toLocaleTimeString()}` : "ok -";
    const err = s.lastErrorUtc ? `err ${new Date(s.lastErrorUtc).toLocaleTimeString()}` : "err -";
    const msg = s.lastError ? ` (${s.lastError})` : "";
    return `${s.key}: ${ok}, ${err}${msg}`;
  });
  connectorsState.textContent = lines.join("\n");
}

async function loadFingerprints() {
  const res = await fetch(`${apiBase}/api/connectors/fingerprints`);
  const data = await res.json();
  const items = data.items || [];
  if (items.length === 0) {
    fingerprintStatus.textContent = "No gateway fingerprints yet.";
    return;
  }
  const lines = items.map((f) => {
    const vendor = f.vendor || "unknown";
    const connector = f.suggestedConnector ? ` -> ${f.suggestedConnector}` : "";
    return `${f.gatewayIp} ${vendor}${connector}`;
  });
  fingerprintStatus.textContent = lines.join("\n");
}

function fillCredentialSelect(select, creds) {
  select.innerHTML = "";
  const empty = document.createElement("option");
  empty.value = "";
  empty.textContent = "(none)";
  select.appendChild(empty);
  creds.forEach((c) => {
    const opt = document.createElement("option");
    opt.value = c.id;
    opt.textContent = `${c.name} (${c.purpose})`;
    select.appendChild(opt);
  });
}

async function saveSettingsToApi() {
  const ports = tcpPorts.value
    .split(",")
    .map((p) => parseInt(p.trim(), 10))
    .filter((p) => !Number.isNaN(p) && p > 0);

  const payload = {
    scanIntervalSeconds: parseInt(scanInterval.value, 10),
    pingTimeoutMs: parseInt(pingTimeout.value, 10),
    tcpConnectTimeoutMs: parseInt(tcpTimeout.value, 10),
    maxConcurrentPings: parseInt(maxPings.value, 10),
    maxHostsPerSubnet: parseInt(maxHosts.value, 10),
    enableSsdp: enableSsdp.checked,
    tcpPorts: ports,
  };

  const res = await fetch(`${apiBase}/api/settings`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error("Failed to save settings");
  }

  settingsStatus.textContent = "Saved.";
  setTimeout(() => (settingsStatus.textContent = ""), 2000);
}

saveSettings.addEventListener("click", () => {
  settingsStatus.textContent = "Saving...";
  saveSettingsToApi().catch(() => {
    settingsStatus.textContent = "Save failed.";
  });
});

async function saveConnectorsToApi() {
  const tpFields = (tplinkFields.value || "").split(",").map((v) => v.trim());
  const tpCols = (tplinkCols.value || "").split(",").map((v) => parseInt(v.trim(), 10));
  const ngFields = (netgearFields.value || "").split(",").map((v) => v.trim());
  const ngCols = (netgearCols.value || "").split(",").map((v) => parseInt(v.trim(), 10));
  const orbiFieldsList = (orbiFields.value || "").split(",").map((v) => v.trim());
  const orbiColsList = (orbiCols.value || "").split(",").map((v) => parseInt(v.trim(), 10));
  const omadaFieldsList = (omadaFields.value || "").split(",").map((v) => v.trim());
  const omadaColsList = (omadaCols.value || "").split(",").map((v) => parseInt(v.trim(), 10));
  const asusFieldsList = (asusFields.value || "").split(",").map((v) => v.trim());
  const asusColsList = (asusCols.value || "").split(",").map((v) => parseInt(v.trim(), 10));
  const payload = {
    enabled: {
      "upnp-igd": connUpnp.checked,
      snmp: connSnmp.checked,
      "dhcp-http": connDhcp.checked,
      unifi: connUnifi.checked,
      tplink: connTpLink.checked,
      netgear: connNetgear.checked,
      orbi: connOrbi.checked,
      omada: connOmada.checked,
      asus: connAsus.checked,
    },
    snmp: {
      hosts: snmpHosts.value.split(",").map((h) => h.trim()).filter((h) => h),
      community: snmpCommunity.value || "public",
      communityCredentialId: snmpCommunityCred.value || null,
      port: parseInt(snmpPort.value, 10) || 161,
      snmpWalkPath: snmpWalkPath.value || "snmpwalk",
      timeoutSeconds: parseInt(snmpTimeout.value, 10) || 3,
    },
    dhcpHttp: {
      url: dhcpUrl.value || "",
      authHeader: dhcpHeader.value || "",
      authValue: dhcpAuthValue.value || "",
      authValueCredentialId: dhcpAuthCred.value || null,
    },
    unifi: {
      baseUrl: unifiBaseUrl.value || "",
      site: unifiSite.value || "default",
      username: unifiUser.value || "",
      password: unifiPass.value || "",
      passwordCredentialId: unifiPassCred.value || null,
      skipTlsVerify: unifiSkipTls.checked,
    },
    tpLink: {
      url: tplinkUrl.value || "",
      authHeader: tplinkHeader.value || "",
      authValue: tplinkAuthValue.value || "",
      authValueCredentialId: tplinkAuthCred.value || null,
      format: tplinkFormat.value || "json",
      ipField: tpFields[0] || "ipAddress",
      macField: tpFields[1] || "macAddress",
      hostField: tpFields[2] || "hostname",
      ipColumn: Number.isNaN(tpCols[0]) ? 0 : tpCols[0],
      macColumn: Number.isNaN(tpCols[1]) ? 1 : tpCols[1],
      hostColumn: Number.isNaN(tpCols[2]) ? 2 : tpCols[2],
    },
    netgear: {
      url: netgearUrl.value || "",
      authHeader: netgearHeader.value || "",
      authValue: netgearAuthValue.value || "",
      authValueCredentialId: netgearAuthCred.value || null,
      format: netgearFormat.value || "json",
      ipField: ngFields[0] || "ipAddress",
      macField: ngFields[1] || "macAddress",
      hostField: ngFields[2] || "hostname",
      ipColumn: Number.isNaN(ngCols[0]) ? 0 : ngCols[0],
      macColumn: Number.isNaN(ngCols[1]) ? 1 : ngCols[1],
      hostColumn: Number.isNaN(ngCols[2]) ? 2 : ngCols[2],
    },
    orbi: {
      url: orbiUrl.value || "",
      authHeader: orbiHeader.value || "",
      authValue: orbiAuthValue.value || "",
      authValueCredentialId: orbiAuthCred.value || null,
      format: orbiFormat.value || "json",
      ipField: orbiFieldsList[0] || "ipAddress",
      macField: orbiFieldsList[1] || "macAddress",
      hostField: orbiFieldsList[2] || "hostname",
      ipColumn: Number.isNaN(orbiColsList[0]) ? 0 : orbiColsList[0],
      macColumn: Number.isNaN(orbiColsList[1]) ? 1 : orbiColsList[1],
      hostColumn: Number.isNaN(orbiColsList[2]) ? 2 : orbiColsList[2],
    },
    omada: {
      url: omadaUrl.value || "",
      authHeader: omadaHeader.value || "",
      authValue: omadaAuthValue.value || "",
      authValueCredentialId: omadaAuthCred.value || null,
      format: omadaFormat.value || "json",
      ipField: omadaFieldsList[0] || "ipAddress",
      macField: omadaFieldsList[1] || "macAddress",
      hostField: omadaFieldsList[2] || "hostname",
      ipColumn: Number.isNaN(omadaColsList[0]) ? 0 : omadaColsList[0],
      macColumn: Number.isNaN(omadaColsList[1]) ? 1 : omadaColsList[1],
      hostColumn: Number.isNaN(omadaColsList[2]) ? 2 : omadaColsList[2],
    },
    asus: {
      url: asusUrl.value || "",
      authHeader: asusHeader.value || "",
      authValue: asusAuthValue.value || "",
      authValueCredentialId: asusAuthCred.value || null,
      format: asusFormat.value || "json",
      ipField: asusFieldsList[0] || "ipAddress",
      macField: asusFieldsList[1] || "macAddress",
      hostField: asusFieldsList[2] || "hostname",
      ipColumn: Number.isNaN(asusColsList[0]) ? 0 : asusColsList[0],
      macColumn: Number.isNaN(asusColsList[1]) ? 1 : asusColsList[1],
      hostColumn: Number.isNaN(asusColsList[2]) ? 2 : asusColsList[2],
    },
  };

  const res = await fetch(`${apiBase}/api/connectors`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error("Failed to save connectors");
  }
  connectorsStatus.textContent = "Saved.";
  setTimeout(() => (connectorsStatus.textContent = ""), 2000);
}

saveConnectors.addEventListener("click", () => {
  connectorsStatus.textContent = "Saving...";
  saveConnectorsToApi().catch(() => {
    connectorsStatus.textContent = "Save failed.";
  });
});

async function createCredential() {
  const payload = {
    name: credName.value || "credential",
    purpose: credPurpose.value || "general",
    secret: credSecret.value || "",
  };
  const res = await fetch(`${apiBase}/api/credentials`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    throw new Error("Failed to create credential");
  }
  credStatus.textContent = "Created.";
  credSecret.value = "";
  await loadCredentials();
  setTimeout(() => (credStatus.textContent = ""), 2000);
}

createCred.addEventListener("click", () => {
  credStatus.textContent = "Creating...";
  createCredential().catch(() => {
    credStatus.textContent = "Create failed.";
  });
});

function renderCredentialList(creds) {
  credList.innerHTML = "";
  if (!Array.isArray(creds) || creds.length === 0) {
    credList.innerHTML = "<div class=\"meta\">No credentials stored.</div>";
    return;
  }

  creds.forEach((c) => {
    const row = document.createElement("div");
    row.className = "timeline-item";
    const label = document.createElement("span");
    label.textContent = `${c.name} (${c.purpose})`;
    const actions = document.createElement("span");
    const del = document.createElement("button");
    del.className = "btn";
    del.textContent = "Delete";
    del.addEventListener("click", () => deleteCredential(c.id));
    actions.appendChild(del);
    row.appendChild(label);
    row.appendChild(actions);
    credList.appendChild(row);
  });
}

async function deleteCredential(id) {
  credStatus.textContent = "Deleting...";
  try {
    const res = await fetch(`${apiBase}/api/credentials/${id}`, { method: "DELETE" });
    if (!res.ok) {
      throw new Error("delete failed");
    }
    await loadCredentials();
    credStatus.textContent = "Deleted.";
    setTimeout(() => (credStatus.textContent = ""), 2000);
  } catch {
    credStatus.textContent = "Delete failed.";
  }
}

function renderList(devices) {
  deviceList.innerHTML = "";
  devices.forEach((d) => {
    const item = document.createElement("div");
    item.className = "device-item";
    const title = document.createElement("div");
    title.textContent = `${d.hostname || "Unknown"} (${d.ipAddress || "no IP"})`;
    const sub = document.createElement("div");
    sub.className = "device-sub";
    const status = document.createElement("span");
    status.className = `badge ${d.isOnline ? "online" : "offline"}`;
    status.textContent = d.isOnline ? "online" : "offline";
    const lastSeen = document.createElement("span");
    lastSeen.textContent = `last seen ${formatTime(d.lastSeenUtc)}`;
    sub.appendChild(status);
    sub.appendChild(lastSeen);
    item.appendChild(title);
    item.appendChild(sub);
    item.addEventListener("click", () => selectDevice(d));
    deviceList.appendChild(item);
  });
}

function selectDevice(d) {
  deviceName.textContent = d.hostname || "Unknown device";
  deviceMeta.textContent = `IP: ${d.ipAddress || "-"}\nMAC: ${d.macAddress || "-"}\nVendor: ${d.vendor || "-"}\nType: ${d.typeGuess || "-"}\nConfidence: ${d.confidence || 0}`;
  loadDeviceEvidence(d.deviceId);
  loadDeviceClassification(d.deviceId);
  loadDeviceRisk(d.deviceId);
  loadDeviceStats(d.deviceId);
}

function renderCanvas(devices) {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const centerX = canvas.width / 2;
  const centerY = canvas.height / 2;
  const radius = Math.min(centerX, centerY) - 40;

  nodePositions.clear();
  devices.forEach((d, index) => {
    const angle = (index / Math.max(1, devices.length)) * Math.PI * 2;
    const x = centerX + Math.cos(angle) * radius;
    const y = centerY + Math.sin(angle) * radius;
    nodePositions.set(d.deviceId, { x, y, device: d });

    ctx.beginPath();
    ctx.fillStyle = d.isOnline ? "#4cc9f0" : "#5a6d76";
    ctx.arc(x, y, 10, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#e9f0f3";
    ctx.font = "12px Palatino Linotype";
    ctx.fillText(d.hostname || "Unknown", x + 14, y + 4);
  });
}

function renderTimeline() {
  timeline.innerHTML = "";
  eventCache.slice(0, 20).forEach((evt) => {
    const row = document.createElement("div");
    row.className = "timeline-item";
    const badge = document.createElement("span");
    badge.className = `badge ${evt.eventType}`;
    badge.textContent = evt.eventType.toUpperCase();
    const detail = document.createElement("span");
    detail.textContent = `${evt.deviceId.slice(0, 6)} - ${new Date(evt.occurredAtUtc).toLocaleTimeString()}`;
    row.appendChild(badge);
    row.appendChild(detail);
    timeline.appendChild(row);
  });
}

function renderEventTimeline() {
  eventTimeline.innerHTML = "";
  const type = eventFilter.value;
  eventCache
    .filter((e) => !type || e.eventType === type)
    .slice(0, 200)
    .forEach((evt) => {
      const row = document.createElement("div");
      row.className = "timeline-item";
      const badge = document.createElement("span");
      badge.className = `badge ${evt.eventType}`;
      badge.textContent = evt.eventType.toUpperCase();
      const detail = document.createElement("span");
      detail.textContent = `${evt.deviceId.slice(0, 6)} • ${formatTime(evt.occurredAtUtc)}`;
      row.appendChild(badge);
      row.appendChild(detail);
      eventTimeline.appendChild(row);
    });
}

function renderFromCache() {
  const devices = applyFilters(Array.from(deviceCache.values())).sort(
    (a, b) => new Date(b.lastSeenUtc) - new Date(a.lastSeenUtc)
  );
  renderList(devices);
  renderCanvas(devices);
  renderDevicesView(devices);
}

function formatTime(value) {
  if (!value) {
    return "-";
  }
  const dt = new Date(value);
  if (Number.isNaN(dt.getTime())) {
    return "-";
  }
  return dt.toLocaleTimeString();
}

function applyFilters(devices) {
  const query = (searchInput.value || "").toLowerCase();
  const type = filterType.value;
  const vendor = filterVendor.value;
  const status = filterStatus.value;
  return devices.filter((d) => {
    const matchQuery =
      !query ||
      (d.hostname || "").toLowerCase().includes(query) ||
      (d.ipAddress || "").toLowerCase().includes(query) ||
      (d.macAddress || "").toLowerCase().includes(query);
    const matchType = !type || (d.typeGuess || "") === type;
    const matchVendor = !vendor || (d.vendor || "") === vendor;
    const matchStatus =
      !status ||
      (status === "online" && d.isOnline) ||
      (status === "offline" && !d.isOnline);
    return matchQuery && matchType && matchVendor && matchStatus;
  });
}

function refreshFilters() {
  const devices = Array.from(deviceCache.values());
  const types = [...new Set(devices.map((d) => d.typeGuess).filter(Boolean))].sort();
  const vendors = [...new Set(devices.map((d) => d.vendor).filter(Boolean))].sort();
  fillSelect(filterType, "All types", types);
  fillSelect(filterVendor, "All vendors", vendors);
}

function loadFilterState() {
  const raw = localStorage.getItem("netwatch.filters");
  if (!raw) {
    return;
  }
  try {
    const saved = JSON.parse(raw);
    searchInput.value = saved.query || "";
    filterType.value = saved.type || "";
    filterVendor.value = saved.vendor || "";
    filterStatus.value = saved.status || "";
    eventFilter.value = saved.event || "";
  } catch {
  }
}

function saveFilterState() {
  const payload = {
    query: searchInput.value || "",
    type: filterType.value || "",
    vendor: filterVendor.value || "",
    status: filterStatus.value || "",
    event: eventFilter.value || "",
  };
  localStorage.setItem("netwatch.filters", JSON.stringify(payload));
}

function fillSelect(select, label, items) {
  const current = select.value;
  select.innerHTML = "";
  const first = document.createElement("option");
  first.value = "";
  first.textContent = label;
  select.appendChild(first);
  items.forEach((item) => {
    const opt = document.createElement("option");
    opt.value = item;
    opt.textContent = item;
    select.appendChild(opt);
  });
  select.value = current || "";
}

function startConnectorStatusPoll() {
  setInterval(() => {
    loadConnectorStatus();
    loadFingerprints();
    loadNetworkSummary();
  }, 15000);
}
async function loadDeviceEvidence(deviceId) {
  deviceEvidence.textContent = "Loading evidence...";
  deviceSignals.textContent = "";
  deviceHistory.textContent = "";
  deviceTimeline.innerHTML = "";
  try {
    const res = await fetch(`${apiBase}/api/devices/${deviceId}/observations?limit=20`);
    const rows = await res.json();
    deviceEvidence.textContent = rows
      .map((r) => `${r.source} • ${r.observedAtUtc}`)
      .join("\n");
    renderDeviceTimeline(rows);
    renderDeviceSignals(rows);
    renderDeviceHistory(rows);
  } catch {
    deviceEvidence.textContent = "Evidence unavailable.";
  }
}

async function loadDeviceClassification(deviceId) {
  deviceClassification.textContent = "Loading classification...";
  try {
    const res = await fetch(`${apiBase}/api/devices/${deviceId}/classification`);
    if (!res.ok) {
      deviceClassification.textContent = "No classification.";
      return;
    }
    const data = await res.json();
    const type = data.typeGuess || "unknown";
    const vendor = data.vendor || "unknown";
    const conf = data.confidence || 0;
    const reasons = (data.reasons || []).join(", ");
    deviceClassification.textContent = `Type: ${type}\nVendor: ${vendor}\nConfidence: ${conf}\nReasons: ${reasons || "-"}`;
  } catch {
    deviceClassification.textContent = "Classification unavailable.";
  }
}

async function loadDeviceStats(deviceId) {
  deviceStats.textContent = "Loading stats...";
  try {
    const res = await fetch(`${apiBase}/api/devices/${deviceId}/summary?hours=24`);
    if (!res.ok) {
      deviceStats.textContent = "Stats unavailable.";
      return;
    }
    const data = await res.json();
    const onlineHours = (data.onlineSeconds || 0) / 3600;
    deviceStats.textContent = `Online: ${onlineHours.toFixed(2)}h\nJoins: ${data.joinCount || 0}\nLeaves: ${data.leaveCount || 0}\nLast seen: ${formatTime(data.lastSeenUtc)}`;
  } catch {
    deviceStats.textContent = "Stats unavailable.";
  }
}

async function loadDeviceRisk(deviceId) {
  deviceRisk.textContent = "Loading risk...";
  try {
    const res = await fetch(`${apiBase}/api/devices/${deviceId}/risk`);
    if (!res.ok) {
      deviceRisk.textContent = "Risk unavailable.";
      return;
    }
    const data = await res.json();
    const reasons = (data.reasons || []).join(", ");
    deviceRisk.textContent = `Level: ${data.level}\nScore: ${data.score}\nReasons: ${reasons || "-"}`;
  } catch {
    deviceRisk.textContent = "Risk unavailable.";
  }
}
async function loadNetworkSummary() {
  try {
    const res = await fetch(`${apiBase}/api/analytics/summary?hours=24`);
    if (!res.ok) {
      networkSummary.textContent = "Summary unavailable.";
      return;
    }
    const data = await res.json();
    networkSummary.textContent = `Devices: ${data.deviceCount}\nOnline: ${data.onlineCount}\nJoins: ${data.joinCount}\nLeaves: ${data.leaveCount}`;
  } catch {
    networkSummary.textContent = "Summary unavailable.";
  }
}

function renderDeviceTimeline(rows) {
  deviceTimeline.innerHTML = "";
  rows.forEach((r) => {
    const row = document.createElement("div");
    row.className = "timeline-item";
    const badge = document.createElement("span");
    badge.className = "badge";
    badge.textContent = r.source.toUpperCase();
    const detail = document.createElement("span");
    detail.textContent = `${r.typeHint || r.serviceHint || "signal"} • ${formatTime(r.observedAtUtc)}`;
    row.appendChild(badge);
    row.appendChild(detail);
    deviceTimeline.appendChild(row);
  });
}

function renderDeviceSignals(rows) {
  const signals = new Set();
  rows.forEach((r) => {
    if (r.serviceHint) {
      signals.add(r.serviceHint);
    }
    if (r.typeHint) {
      signals.add(r.typeHint);
    }
  });
  deviceSignals.textContent = signals.size ? Array.from(signals).join("\n") : "No signals yet.";
}

function renderDeviceHistory(rows) {
  const ips = new Set();
  const macs = new Set();
  rows.forEach((r) => {
    if (r.ipAddress) {
      ips.add(r.ipAddress);
    }
    if (r.macAddress) {
      macs.add(r.macAddress);
    }
  });
  const parts = [];
  parts.push(`IPs: ${Array.from(ips).join(", ") || "-"}`);
  parts.push(`MACs: ${Array.from(macs).join(", ") || "-"}`);
  deviceHistory.textContent = parts.join("\n");
}

function renderDevicesView(devices) {
  devicesTable.innerHTML = "";
  const header = document.createElement("div");
  header.className = "device-row header";
  header.innerHTML = "<div>Name</div><div>IP</div><div>MAC</div><div>Status</div><div>Last Seen</div>";
  devicesTable.appendChild(header);
  devices.forEach((d) => {
    const row = document.createElement("div");
    row.className = "device-row";
    row.innerHTML = `
      <div>${d.hostname || "Unknown"}</div>
      <div>${d.ipAddress || "-"}</div>
      <div>${d.macAddress || "-"}</div>
      <div>${d.isOnline ? "online" : "offline"}</div>
      <div>${formatTime(d.lastSeenUtc)}</div>
    `;
    row.addEventListener("click", () => selectDevice(d));
    devicesTable.appendChild(row);
  });
}

function startStream() {
  const stream = new EventSource(`${apiBase}/api/stream`);
  stream.addEventListener("device", (evt) => {
    const device = JSON.parse(evt.data);
    if (device.deviceId) {
      deviceCache.set(device.deviceId, device);
      refreshFilters();
      renderFromCache();
    }
  });
  stream.addEventListener("event", (evt) => {
    const event = JSON.parse(evt.data);
    eventCache.unshift(event);
    if (eventCache.length > 200) {
      eventCache.pop();
    }
    renderTimeline();
    renderEventTimeline();
  });
  stream.onerror = () => {
    stream.close();
    setTimeout(startStream, 3000);
  };
}

filterType.addEventListener("change", renderFromCache);
filterVendor.addEventListener("change", renderFromCache);
filterStatus.addEventListener("change", renderFromCache);
searchInput.addEventListener("input", renderFromCache);
eventFilter.addEventListener("change", renderEventTimeline);
eventFilter.addEventListener("change", saveFilterState);
navItems.forEach((item) => {
  item.addEventListener("click", () => {
    setView(item.dataset.view);
  });
});
filterType.addEventListener("change", saveFilterState);
filterVendor.addEventListener("change", saveFilterState);
filterStatus.addEventListener("change", saveFilterState);
searchInput.addEventListener("input", saveFilterState);
canvas.addEventListener("click", (evt) => {
  const rect = canvas.getBoundingClientRect();
  const x = evt.clientX - rect.left;
  const y = evt.clientY - rect.top;
  let closest = null;
  let best = 9999;
  nodePositions.forEach((pos) => {
    const dx = pos.x - x;
    const dy = pos.y - y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist < best) {
      best = dist;
      closest = pos.device;
    }
  });
  if (closest && best <= 18) {
    selectDevice(closest);
  }
});

loadFilterState();

Promise.all([loadDevices(), loadSettings(), loadEvents(), loadCredentials(), loadConnectors()])
  .then(() => Promise.all([loadConnectorStatus(), loadFingerprints(), loadNetworkSummary()]))
  .then(() => {
    startConnectorStatusPoll();
    startStream();
  })
  .catch(() => {
    deviceList.innerHTML = "<div class=\"meta\">Core service not running.</div>";
  });

setView("map");
