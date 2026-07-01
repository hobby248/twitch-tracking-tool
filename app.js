const STORAGE = {
  trackedChannels: "twitchPin.trackedChannels",
  twitchClientId: "twitchPin.twitchClientId",
  twitchToken: "twitchPin.twitchToken",
  oauthState: "twitchPin.oauthState",
  lowQuality: "twitchPin.lowQuality",
  viewerVolume: "twitchPin.viewerVolume",
  viewerMuted: "twitchPin.viewerMuted",
  navigationLock: "twitchPin.navigationLock",
  sidebarHidden: "twitchPin.sidebarHidden",
  oldPinnedChannel: "twitchPin.pinnedChannel",
  oldPinnedUser: "twitchPin.pinnedUser"
};

const DEFAULT_VOLUME = 0.01;
const PLAYBACK_KEEPALIVE_MS = 10000;
const OFFSCREEN_PLAYBACK_KEEPALIVE_MS = 1000;
const PLAYBACK_SCROLL_SETTLE_MS = 300;
const HOURLY_RELOAD_GRACE_MS = 1000;
const METADATA_REFRESH_MS = 60000;
const TWITCH_BATCH_SIZE = 100;

const refs = {
  appShell: document.querySelector("#appShell"),
  hideSidebarButton: document.querySelector("#hideSidebarButton"),
  showSidebarButton: document.querySelector("#showSidebarButton"),
  trackForm: document.querySelector("#trackForm"),
  channelInput: document.querySelector("#channelInput"),
  addButton: document.querySelector("#addButton"),
  trackBadge: document.querySelector("#trackBadge"),
  refreshButton: document.querySelector("#refreshButton"),
  clearAllButton: document.querySelector("#clearAllButton"),
  clientIdInput: document.querySelector("#clientIdInput"),
  oauthRedirectUrl: document.querySelector("#oauthRedirectUrl"),
  saveClientIdButton: document.querySelector("#saveClientIdButton"),
  loginWithTwitchButton: document.querySelector("#loginWithTwitchButton"),
  tokenInput: document.querySelector("#tokenInput"),
  tokenStatus: document.querySelector("#tokenStatus"),
  saveTokenButton: document.querySelector("#saveTokenButton"),
  refreshInfoButton: document.querySelector("#refreshInfoButton"),
  clearTokenButton: document.querySelector("#clearTokenButton"),
  tokenHelper: document.querySelector("#tokenHelper"),
  navigationLockToggle: document.querySelector("#navigationLockToggle"),
  openExtensionsFolderButton: document.querySelector("#openExtensionsFolderButton"),
  reloadExtensionsButton: document.querySelector("#reloadExtensionsButton"),
  extensionsStatusButton: document.querySelector("#extensionsStatusButton"),
  volumeSlider: document.querySelector("#volumeSlider"),
  volumeValue: document.querySelector("#volumeValue"),
  muteButton: document.querySelector("#muteButton"),
  openPointsModeButton: document.querySelector("#openPointsModeButton"),
  openOriginalGridButton: document.querySelector("#openOriginalGridButton"),
  openTwitchLoginButton: document.querySelector("#openTwitchLoginButton"),
  openClaimChannelButton: document.querySelector("#openClaimChannelButton"),
  openLiveChannelsButton: document.querySelector("#openLiveChannelsButton"),
  openDropsButton: document.querySelector("#openDropsButton"),
  openAllChannelsButton: document.querySelector("#openAllChannelsButton"),
  onlineCount: document.querySelector("#onlineCount"),
  offlineCount: document.querySelector("#offlineCount"),
  trackedList: document.querySelector("#trackedList"),
  stageStatus: document.querySelector("#stageStatus"),
  stageTitle: document.querySelector("#stageTitle"),
  openGridStageButton: document.querySelector("#openGridStageButton"),
  lowQualityButton: document.querySelector("#lowQualityButton"),
  openTwitchLink: document.querySelector("#openTwitchLink"),
  emptyState: document.querySelector("#emptyState"),
  streamGrid: document.querySelector("#streamGrid"),
  toast: document.querySelector("#toast")
};

const state = {
  trackedChannels: loadTrackedChannels(),
  twitchClientId: localStorage.getItem(STORAGE.twitchClientId) || "",
  twitchToken: localStorage.getItem(STORAGE.twitchToken) || "",
  tokenInfo: null,
  metadataRefreshTimer: 0,
  metadataRefreshing: false,
  lastMetadataError: "",
  lowQuality: localStorage.getItem(STORAGE.lowQuality) === "true",
  viewerVolume: loadViewerVolume(),
  viewerMuted: localStorage.getItem(STORAGE.viewerMuted) === "true",
  navigationLock: localStorage.getItem(STORAGE.navigationLock) === "true",
  sidebarHidden: localStorage.getItem(STORAGE.sidebarHidden) === "true",
  channelStates: new Map(),
  players: new Map(),
  playerRetryTimer: 0,
  playbackKeepaliveTimer: 0,
  offscreenPlaybackKeepaliveTimer: 0,
  playbackResumeFrame: 0,
  playbackResumeTimer: 0,
  hourlyReloadTimer: 0,
  playerVisibilityObserver: null,
  offscreenPlayerLogins: new Set()
};

bindEvents();
boot();

function bindEvents() {
  refs.hideSidebarButton?.addEventListener("click", () => {
    setSidebarHidden(true);
  });

  refs.showSidebarButton?.addEventListener("click", () => {
    setSidebarHidden(false);
  });

  refs.trackForm.addEventListener("submit", (event) => {
    event.preventDefault();
    addChannels(refs.channelInput.value);
  });

  refs.refreshButton.addEventListener("click", () => {
    reloadNonLivePlayers();
  });

  refs.clearAllButton.addEventListener("click", () => {
    if (!state.trackedChannels.length) {
      return;
    }

    if (window.confirm("確定要清空所有追蹤台主？")) {
      clearAllChannels();
      render();
      toast("已清空追蹤清單。");
    }
  });

  refs.clientIdInput.addEventListener("input", () => {
    renderAccount();
  });

  refs.saveClientIdButton.addEventListener("click", () => {
    saveClientIdFromInput();
  });

  refs.loginWithTwitchButton.addEventListener("click", () => {
    startTwitchOAuth();
  });

  refs.saveTokenButton.addEventListener("click", () => {
    saveTokenFromInput();
  });

  refs.refreshInfoButton.addEventListener("click", () => {
    refreshChannelInfo({ silent: false, force: true });
  });

  refs.clearTokenButton.addEventListener("click", () => {
    clearToken();
  });

  refs.navigationLockToggle?.addEventListener("change", () => {
    setNavigationLock(refs.navigationLockToggle.checked);
  });

  refs.openExtensionsFolderButton?.addEventListener("click", () => {
    postBrowserExtensionAction("open-folder");
  });

  refs.reloadExtensionsButton?.addEventListener("click", () => {
    postBrowserExtensionAction("reload");
  });

  refs.extensionsStatusButton?.addEventListener("click", () => {
    postBrowserExtensionAction("status");
  });

  refs.openTwitchLoginButton?.addEventListener("click", () => {
    window.open("https://www.twitch.tv/login", "_blank", "noreferrer");
  });

  refs.openPointsModeButton?.addEventListener("click", () => {
    openPointAccumulationMode();
  });

  refs.openOriginalGridButton?.addEventListener("click", () => {
    openOriginalGrid();
  });

  refs.openGridStageButton?.addEventListener("click", () => {
    openOriginalGrid();
  });

  refs.openClaimChannelButton?.addEventListener("click", () => {
    openClaimChannel();
  });

  refs.openLiveChannelsButton?.addEventListener("click", () => {
    openLiveTrackedChannels();
  });

  refs.openDropsButton?.addEventListener("click", () => {
    window.open("https://www.twitch.tv/drops/inventory", "_blank", "noreferrer");
  });

  refs.openAllChannelsButton?.addEventListener("click", () => {
    openAllTrackedChannels();
  });

  refs.lowQualityButton?.addEventListener("click", () => {
    state.lowQuality = !state.lowQuality;
    localStorage.setItem(STORAGE.lowQuality, String(state.lowQuality));
    applyQualityToAllPlayers();
    keepPlaybackActive();
    render();
    toast(state.lowQuality ? "已嘗試將所有播放器切到最低可用解析度。" : "已嘗試恢復所有播放器為自動畫質。");
  });

  refs.volumeSlider?.addEventListener("input", () => {
    setViewerVolume(Number(refs.volumeSlider.value) / 100);
  });

  refs.muteButton?.addEventListener("click", () => {
    setViewerMuted(!state.viewerMuted);
  });

  refs.trackedList.addEventListener("click", handleChannelAction);
  refs.streamGrid?.addEventListener("click", handleChannelAction);
  document.addEventListener("visibilitychange", keepPlaybackActiveWithRetry);
  window.addEventListener("focus", keepPlaybackActiveWithRetry);
  window.addEventListener("pageshow", keepPlaybackActiveWithRetry);
  window.addEventListener("resize", schedulePlaybackResume);
  window.addEventListener("scroll", schedulePlaybackResume, { passive: true });
  document.addEventListener("scroll", schedulePlaybackResume, true);
  document.addEventListener("wheel", schedulePlaybackResume, { passive: true, capture: true });
  document.addEventListener("touchmove", schedulePlaybackResume, { passive: true, capture: true });
  document.addEventListener("pointerdown", keepPlaybackActiveWithRetry);
  document.addEventListener("keydown", keepPlaybackActiveWithRetry);
}

function boot() {
  handleOAuthRedirect();
  ensureInitialChannelStates();
  refs.clientIdInput.value = state.twitchClientId;
  refs.tokenInput.value = state.twitchToken;
  updateOAuthRedirectUrl();
  startMetadataRefresh();
  startPlaybackKeepalive();
  scheduleNextHourlyReload();
  startPlayerVisibilityObserver();
  applySidebarVisibility();
  render();
  postAudioSettings();
  postNavigationLockSettings();
  refreshChannelInfo({ silent: true });
}

function addChannels(input) {
  const requestedLogins = parseChannelInput(input);
  if (!requestedLogins.length) {
    toast("請輸入至少一個有效的 Twitch 台主帳號。");
    refs.channelInput.focus();
    return;
  }

  const existing = new Set(state.trackedChannels.map((channel) => channel.login));
  const added = [];

  for (const login of requestedLogins) {
    if (existing.has(login)) {
      continue;
    }

    const channel = {
      id: login,
      login,
      display_name: login,
      profile_image_url: "",
      added_at: new Date().toISOString()
    };
    state.trackedChannels.push(channel);
    state.channelStates.set(login, "unknown");
    existing.add(login);
    added.push(login);
  }

  refs.channelInput.value = "";
  saveTrackedChannels();
  render();

  if (added.length) {
    toast(`已加入 ${added.length} 個台主。`);
    refreshChannelInfo({ silent: true });
  } else {
    toast("這些台主已經在追蹤清單中。");
  }
}

function reloadNonLivePlayers(options = {}) {
  if (!state.trackedChannels.length) {
    return;
  }

  for (const channel of state.trackedChannels) {
    if (!isChannelKnownOnline(channel.login)) {
      destroyPlayer(channel.login);
    }
  }

  renderChannelCards();
  if (!options.silent) {
    toast("已重新載入非直播中的播放器。直播中的播放器不會被動。");
  }
}

function scheduleNextHourlyReload() {
  window.clearTimeout(state.hourlyReloadTimer);
  state.hourlyReloadTimer = window.setTimeout(() => {
    reloadNonLivePlayers({ silent: true });
    scheduleNextHourlyReload();
  }, getDelayUntilNextHour());
}

function getDelayUntilNextHour(now = new Date()) {
  const nextHour = new Date(now);
  nextHour.setHours(now.getHours() + 1, 0, 0, 0);
  return Math.max(HOURLY_RELOAD_GRACE_MS, nextHour.getTime() - now.getTime());
}

function handleChannelAction(event) {
  const target = event.target.closest("[data-action]");
  if (!target) {
    return;
  }

  const holder = target.closest("[data-login]");
  const login = holder?.dataset.login;
  if (!login) {
    return;
  }

  const action = target.dataset.action;
  if (action === "remove") {
    removeChannel(login);
    render();
    toast(`已移除 ${login}。`);
  }
}

function removeChannel(login) {
  state.trackedChannels = state.trackedChannels.filter((channel) => channel.login !== login);
  state.channelStates.delete(login);
  removeOffscreenPlayer(login);
  destroyPlayer(login);

  saveTrackedChannels();
}

function clearAllChannels() {
  for (const channel of state.trackedChannels) {
    destroyPlayer(channel.login);
  }

  state.trackedChannels = [];
  state.channelStates.clear();
  clearOffscreenPlayers();
  saveTrackedChannels();
}

function saveClientIdFromInput() {
  const clientId = normalizeClientId(refs.clientIdInput.value);
  if (!clientId) {
    toast("請先貼上 Twitch Client ID。");
    refs.clientIdInput.focus();
    return "";
  }

  state.twitchClientId = clientId;
  localStorage.setItem(STORAGE.twitchClientId, clientId);
  refs.clientIdInput.value = clientId;
  render();
  toast("已儲存 Client ID。");
  return clientId;
}

function startTwitchOAuth() {
  const clientId = normalizeClientId(refs.clientIdInput.value) || state.twitchClientId;
  if (!clientId) {
    toast("請先輸入 Client ID。");
    refs.clientIdInput.focus();
    return;
  }

  state.twitchClientId = clientId;
  localStorage.setItem(STORAGE.twitchClientId, clientId);
  const oauthState = createOAuthState();
  localStorage.setItem(STORAGE.oauthState, oauthState);
  const redirectUri = oauthRedirectUri();
  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    response_type: "token",
    scope: "",
    state: oauthState
  });

  window.location.href = `https://id.twitch.tv/oauth2/authorize?${params.toString()}`;
}

function handleOAuthRedirect() {
  if (!location.hash || !location.hash.includes("access_token")) {
    return;
  }

  const params = new URLSearchParams(location.hash.slice(1));
  const error = params.get("error_description") || params.get("error");
  if (error) {
    state.lastMetadataError = error;
    cleanOAuthUrl();
    toast(`Twitch 登入失敗：${error}`);
    return;
  }

  const expectedState = localStorage.getItem(STORAGE.oauthState) || "";
  const returnedState = params.get("state") || "";
  if (expectedState && expectedState !== returnedState) {
    state.lastMetadataError = "OAuth state 不一致，已拒絕這次登入結果。";
    cleanOAuthUrl();
    toast(state.lastMetadataError);
    return;
  }

  const token = normalizeToken(params.get("access_token") || "");
  if (!token) {
    cleanOAuthUrl();
    toast("Twitch 沒有回傳 Access Token。");
    return;
  }

  state.twitchToken = token;
  state.tokenInfo = null;
  state.lastMetadataError = "";
  localStorage.setItem(STORAGE.twitchToken, token);
  localStorage.removeItem(STORAGE.oauthState);
  cleanOAuthUrl();
  toast("已取得 Twitch Access Token。");
}

function cleanOAuthUrl() {
  const cleanPath = `${location.origin}/`;
  history.replaceState(null, "", cleanPath);
}

function normalizeClientId(value) {
  return String(value || "").trim().replace(/[^a-z0-9]/gi, "");
}

function oauthRedirectUri() {
  return `${location.origin}/auth-callback`;
}

function updateOAuthRedirectUrl() {
  refs.oauthRedirectUrl.textContent = oauthRedirectUri();
}

function createOAuthState() {
  const bytes = new Uint8Array(16);
  if (crypto?.getRandomValues) {
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  }

  return `${Date.now().toString(16)}${Math.random().toString(16).slice(2)}`;
}

function saveTokenFromInput() {
  const token = normalizeToken(refs.tokenInput.value);
  if (!token) {
    toast("請先貼上 Twitch Access Token。");
    refs.tokenInput.focus();
    return;
  }

  state.twitchToken = token;
  localStorage.setItem(STORAGE.twitchToken, token);
  startMetadataRefresh();
  render();
  refreshChannelInfo({ silent: false, force: true });
}

function clearToken() {
  state.twitchToken = "";
  state.tokenInfo = null;
  state.lastMetadataError = "";
  refs.tokenInput.value = "";
  localStorage.removeItem(STORAGE.twitchToken);
  stopMetadataRefresh();
  for (const channel of state.trackedChannels) {
    state.channelStates.set(channel.login, "unknown");
    channel.stream = null;
  }
  saveTrackedChannels();
  render();
  toast("已清除 Token。");
}

function normalizeToken(value) {
  return String(value || "")
    .trim()
    .replace(/^oauth:/i, "")
    .replace(/^bearer\s+/i, "")
    .replace(/^oauth\s+/i, "");
}

function startMetadataRefresh() {
  stopMetadataRefresh();
  if (!state.twitchToken) {
    return;
  }

  state.metadataRefreshTimer = window.setInterval(() => {
    refreshChannelInfo({ silent: true });
  }, METADATA_REFRESH_MS);
}

function stopMetadataRefresh() {
  if (!state.metadataRefreshTimer) {
    return;
  }

  window.clearInterval(state.metadataRefreshTimer);
  state.metadataRefreshTimer = 0;
}

async function refreshChannelInfo(options = {}) {
  if (!state.twitchToken || !state.trackedChannels.length || state.metadataRefreshing) {
    render();
    return;
  }

  state.metadataRefreshing = true;
  state.lastMetadataError = "";
  render();

  try {
    const chunks = chunk(state.trackedChannels.map((channel) => channel.login), TWITCH_BATCH_SIZE);
    for (const logins of chunks) {
      const payload = await fetchTwitchChannelInfo(logins);
      applyTwitchChannelInfo(payload);
    }

    saveTrackedChannels();
    render();
    if (!options.silent) {
      toast("已更新台主資訊。");
    }
  } catch (error) {
    state.lastMetadataError = error?.message || "更新台主資訊失敗。";
    render();
    if (!options.silent || options.force) {
      toast(state.lastMetadataError);
    }
  } finally {
    state.metadataRefreshing = false;
    render();
  }
}

async function fetchTwitchChannelInfo(logins) {
  const query = encodeURIComponent(logins.join(","));
  try {
    const response = await fetch(`/twitch/channel-info?logins=${query}`, {
      headers: {
        Authorization: `Bearer ${state.twitchToken}`
      },
      cache: "no-store"
    });

    const payload = await response.json().catch(() => null);
    if (response.ok && payload?.ok) {
      return payload;
    }

    if (!shouldFallbackToDirectTwitch(payload)) {
      throw new Error(payload?.message || "Token 無效或 Twitch API 暫時無法連線。");
    }
  } catch (error) {
    if (!shouldFallbackToDirectTwitch({ message: error?.message || "" })) {
      throw error;
    }
  }

  return fetchTwitchChannelInfoDirect(logins);
}

function shouldFallbackToDirectTwitch(payload) {
  const message = String(payload?.message || "").toLowerCase();
  return message.includes("基礎連接")
    || message.includes("unexpected")
    || message.includes("receive")
    || message.includes("tls")
    || message.includes("ssl");
}

async function fetchTwitchChannelInfoDirect(logins) {
  const tokenInfo = await validateTokenDirect();
  const usersUrl = `https://api.twitch.tv/helix/users${buildRepeatedQuery("login", logins)}`;
  const streamsUrl = `https://api.twitch.tv/helix/streams${buildRepeatedQuery("user_login", logins)}`;
  const headers = {
    Authorization: `Bearer ${state.twitchToken}`,
    "Client-Id": tokenInfo.client_id
  };

  const [usersResponse, streamsResponse] = await Promise.all([
    fetch(usersUrl, { headers, cache: "no-store" }),
    fetch(streamsUrl, { headers, cache: "no-store" })
  ]);

  const users = await usersResponse.json().catch(() => null);
  const streams = await streamsResponse.json().catch(() => null);
  if (!usersResponse.ok || !streamsResponse.ok) {
    throw new Error(users?.message || streams?.message || "Twitch API 回應失敗。");
  }

  return {
    ok: true,
    client_id: tokenInfo.client_id,
    token_login: tokenInfo.login || "",
    expires_in: Number(tokenInfo.expires_in || 0),
    fetched_at: new Date().toISOString(),
    users,
    streams
  };
}

async function validateTokenDirect() {
  const response = await fetch("https://id.twitch.tv/oauth2/validate", {
    headers: {
      Authorization: `Bearer ${state.twitchToken}`
    },
    cache: "no-store"
  });

  const payload = await response.json().catch(() => null);
  if (!response.ok || !payload?.client_id) {
    throw new Error(payload?.message || "Token 無效，請重新產生 Twitch Access Token。");
  }

  return payload;
}

function buildRepeatedQuery(name, values) {
  return `?${values.map((value) => `${encodeURIComponent(name)}=${encodeURIComponent(value)}`).join("&")}`;
}

function applyTwitchChannelInfo(payload) {
  state.tokenInfo = {
    login: payload.token_login || "",
    expires_in: Number(payload.expires_in || 0),
    fetched_at: payload.fetched_at || new Date().toISOString()
  };

  const users = new Map();
  for (const user of payload.users?.data || []) {
    users.set(String(user.login || "").toLowerCase(), user);
  }

  const streams = new Map();
  for (const stream of payload.streams?.data || []) {
    streams.set(String(stream.user_login || "").toLowerCase(), stream);
  }

  for (const channel of state.trackedChannels) {
    const login = channel.login.toLowerCase();
    const user = users.get(login);
    const stream = streams.get(login);

    if (user) {
      channel.id = String(user.id || channel.id || login);
      channel.login = String(user.login || channel.login).toLowerCase();
      channel.display_name = String(user.display_name || channel.display_name || channel.login);
      channel.profile_image_url = String(user.profile_image_url || channel.profile_image_url || "");
      channel.description = String(user.description || "");
    }

    if (stream) {
      state.channelStates.set(login, "online");
      channel.stream = {
        id: String(stream.id || ""),
        title: String(stream.title || ""),
        game_name: String(stream.game_name || ""),
        viewer_count: Number(stream.viewer_count || 0),
        started_at: String(stream.started_at || ""),
        language: String(stream.language || "")
      };
    } else if (user) {
      state.channelStates.set(login, "offline");
      channel.stream = null;
    } else {
      state.channelStates.set(login, "missing");
      channel.stream = null;
    }
  }
}

function chunk(items, size) {
  const chunks = [];
  for (let index = 0; index < items.length; index += size) {
    chunks.push(items.slice(index, index + size));
  }
  return chunks;
}

function render() {
  renderLayoutControls();
  renderAccount();
  renderNavigationControls();
  renderSummary();
  renderAudioControls();
  renderTrackedList();
  renderChannelCards();
}

function renderLayoutControls() {
  if (refs.lowQualityButton) {
    refs.lowQualityButton.textContent = state.lowQuality ? "自動畫質" : "低解析度";
    refs.lowQualityButton.setAttribute("aria-pressed", String(state.lowQuality));
    refs.lowQualityButton.className = `button ${state.lowQuality ? "button-primary" : "button-secondary"}`;
  }
  applySidebarVisibility();
}

function setSidebarHidden(hidden) {
  state.sidebarHidden = hidden;
  localStorage.setItem(STORAGE.sidebarHidden, String(hidden));
  applySidebarVisibility();
  window.requestAnimationFrame(() => {
    updateAllPlayerVisibility();
    keepPlaybackActiveWithRetry();
  });
}

function applySidebarVisibility() {
  if (!refs.appShell || !refs.hideSidebarButton || !refs.showSidebarButton) {
    return;
  }

  refs.appShell.classList.toggle("sidebar-hidden", state.sidebarHidden);
  refs.hideSidebarButton.hidden = state.sidebarHidden;
  refs.showSidebarButton.hidden = !state.sidebarHidden;
  refs.showSidebarButton.setAttribute("aria-expanded", String(!state.sidebarHidden));
  refs.hideSidebarButton.setAttribute("aria-expanded", String(!state.sidebarHidden));
}

function renderAccount() {
  refs.addButton.disabled = false;
  refs.refreshButton.disabled = !state.trackedChannels.length;
  refs.clearAllButton.disabled = !state.trackedChannels.length;
  refs.loginWithTwitchButton.disabled = !normalizeClientId(refs.clientIdInput.value || state.twitchClientId);
  refs.refreshInfoButton.disabled = !state.twitchToken || !state.trackedChannels.length || state.metadataRefreshing;
  refs.clearTokenButton.disabled = !state.twitchToken;
  if (refs.openPointsModeButton) refs.openPointsModeButton.disabled = !state.trackedChannels.length;
  if (refs.openOriginalGridButton) refs.openOriginalGridButton.disabled = !state.trackedChannels.length;
  if (refs.openGridStageButton) refs.openGridStageButton.disabled = !state.trackedChannels.length;
  if (refs.openClaimChannelButton) refs.openClaimChannelButton.disabled = !state.trackedChannels.length;
  if (refs.openLiveChannelsButton) refs.openLiveChannelsButton.disabled = !state.trackedChannels.length;
  if (refs.openAllChannelsButton) refs.openAllChannelsButton.disabled = !state.trackedChannels.length;

  if (!state.twitchToken) {
    refs.tokenStatus.textContent = "未設定";
    refs.tokenStatus.className = "badge badge-muted";
    refs.tokenHelper.textContent = state.twitchClientId
      ? "已儲存 Client ID。請按「登入取得 Token」完成 Twitch 授權，Token 只用來查台主資訊。"
      : "請先填 Twitch Client ID。播放與忠誠點仍使用 Twitch 原站頁，不用內嵌播放器。";
  } else if (state.metadataRefreshing) {
    refs.tokenStatus.textContent = "更新中";
    refs.tokenStatus.className = "badge badge-offline";
    refs.tokenHelper.textContent = "正在用 Token 更新台主資訊。播放頁面不會被重新整理。";
  } else if (state.lastMetadataError) {
    refs.tokenStatus.textContent = "錯誤";
    refs.tokenStatus.className = "badge badge-error";
    refs.tokenHelper.textContent = state.lastMetadataError;
  } else if (state.tokenInfo) {
    refs.tokenStatus.textContent = "已連線";
    refs.tokenStatus.className = "badge badge-online";
    refs.tokenHelper.textContent = `Token 已驗證${state.tokenInfo.login ? `：${state.tokenInfo.login}` : ""}。台主資訊每 60 秒更新一次，不會影響原站播放。`;
  } else {
    refs.tokenStatus.textContent = "已儲存";
    refs.tokenStatus.className = "badge badge-muted";
    refs.tokenHelper.textContent = "Token 已存在，加入台主或按「更新台主資訊」後會查 Twitch API。";
  }
}

function renderNavigationControls() {
  if (refs.navigationLockToggle) {
    refs.navigationLockToggle.checked = state.navigationLock;
  }
}

function renderSummary() {
  const total = state.trackedChannels.length;
  const online = countChannelsByState("online");
  const offline = countChannelsByState("offline");
  const hidden = countAutoHiddenChannels();

  refs.trackBadge.textContent = `${total} 台`;
  refs.trackBadge.className = `badge ${total ? "badge-online" : "badge-muted"}`;
  refs.onlineCount.textContent = String(online);
  refs.offlineCount.textContent = state.twitchToken ? String(hidden || offline) : "原站";

  if (!refs.stageStatus || !refs.stageTitle || !refs.openTwitchLink) {
    return;
  }

  if (!total) {
    refs.stageStatus.textContent = "待機";
    refs.stageStatus.className = "badge badge-muted";
    refs.stageTitle.textContent = "尚未加入追蹤台主";
    refs.openTwitchLink.href = "https://www.twitch.tv";
    return;
  }

  refs.stageStatus.textContent = state.twitchToken ? "Token 資訊" : "原站模式";
  refs.stageStatus.className = state.twitchToken ? "badge badge-online" : "badge badge-muted";
  refs.stageTitle.textContent = state.twitchToken
    ? `${online} 台直播中，${hidden} 台未開台已隱藏；播放仍使用 Twitch 原站頁`
    : `${total} 台追蹤中，使用 Twitch 原站頁`;

  const focusLogin = state.trackedChannels[0]?.login;
  refs.openTwitchLink.href = focusLogin ? `https://www.twitch.tv/${focusLogin}` : "https://www.twitch.tv";
}

function renderAudioControls() {
  if (refs.volumeSlider) {
    refs.volumeSlider.value = String(Math.round(state.viewerVolume * 100));
  }

  if (refs.volumeValue) {
    refs.volumeValue.textContent = state.viewerMuted ? "靜音" : `${Math.round(state.viewerVolume * 100)}%`;
    refs.volumeValue.className = `badge ${state.viewerMuted ? "badge-offline" : "badge-muted"}`;
  }

  if (refs.muteButton) {
    refs.muteButton.textContent = state.viewerMuted ? "解除靜音" : "一鍵靜音";
    refs.muteButton.setAttribute("aria-pressed", String(state.viewerMuted));
    refs.muteButton.className = `button ${state.viewerMuted ? "button-primary" : "button-secondary"}`;
  }
}

function countChannelsByState(status) {
  return state.trackedChannels.filter((channel) => state.channelStates.get(channel.login) === status).length;
}

function countAutoHiddenChannels() {
  return state.trackedChannels.filter((channel) => isAutoHiddenChannel(channel)).length;
}

function renderTrackedList() {
  refs.trackedList.replaceChildren();

  for (const channel of state.trackedChannels) {
    const row = document.createElement("div");
    row.className = "tracked-row";
    row.dataset.login = channel.login;

    const image = document.createElement("img");
    image.src = channel.profile_image_url || makeAvatarDataUrl(channel.login);
    image.alt = channel.display_name || channel.login;

    const text = document.createElement("div");
    const name = document.createElement("strong");
    name.textContent = channel.display_name || channel.login;
    const login = document.createElement("span");
    login.textContent = `@${channel.login}`;
    text.append(name, login);

    const status = document.createElement("span");
    const statusInfo = getStatusInfo(channel.login);
    status.className = `badge ${statusInfo.badgeClass}`;
    status.textContent = statusInfo.shortLabel;

    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "icon-button icon-button-danger";
    remove.dataset.action = "remove";
    remove.title = "移除";
    remove.setAttribute("aria-label", `移除 ${channel.login}`);
    remove.textContent = "X";

    row.append(image, text, status, remove);
    refs.trackedList.append(row);
  }
}

function renderChannelCards() {
  const visibleChannels = getVisibleChannels();
  postViewerChannels(visibleChannels);
  if (!refs.streamGrid || !refs.emptyState) {
    return;
  }

  renderEmptyState(visibleChannels.length);
  refs.emptyState.hidden = visibleChannels.length > 0;
  refs.streamGrid.dataset.layout = getGridLayout(visibleChannels.length);

  const active = new Set(visibleChannels.map((channel) => channel.login));
  for (const card of Array.from(refs.streamGrid.querySelectorAll("[data-card-login]"))) {
    const login = card.dataset.cardLogin;
    if (!active.has(login)) {
      removeOffscreenPlayer(login);
      destroyPlayer(login);
      card.remove();
    }
  }

  for (const channel of visibleChannels) {
    let card = refs.streamGrid.querySelector(`[data-card-login="${cssEscape(channel.login)}"]`);
    if (!card) {
      card = createChannelCard(channel);
      refs.streamGrid.append(card);
    }

    updateChannelCard(card, channel);
  }
}

function getVisibleChannels(channels = state.trackedChannels) {
  return channels.filter((channel) => !isAutoHiddenChannel(channel));
}

function postViewerChannels(channels = getVisibleChannels()) {
  if (!window.chrome?.webview?.postMessage) {
    return false;
  }

  window.chrome.webview.postMessage({
    type: "viewer-channels",
    channels: channels
      .map((channel) => channel.login)
      .filter(Boolean),
  });
  return true;
}

function postAudioSettings() {
  if (!window.chrome?.webview?.postMessage) {
    return false;
  }

  window.chrome.webview.postMessage({
    type: "audio-settings",
    volume: state.viewerVolume,
    muted: state.viewerMuted,
  });
  return true;
}

function postNavigationLockSettings() {
  if (!window.chrome?.webview?.postMessage) {
    return false;
  }

  window.chrome.webview.postMessage({
    type: "navigation-lock-settings",
    enabled: state.navigationLock,
  });
  return true;
}

function postBrowserExtensionAction(action) {
  if (!window.chrome?.webview?.postMessage) {
    toast("目前不是 WebView2 程式環境，無法操作擴充功能。");
    return false;
  }

  window.chrome.webview.postMessage({
    type: "browser-extension",
    action,
  });
  return true;
}

function setViewerVolume(volume) {
  state.viewerVolume = normalizeVolume(volume);
  if (state.viewerVolume > 0 && state.viewerMuted) {
    state.viewerMuted = false;
  }

  saveAudioSettings();
  renderAudioControls();
  postAudioSettings();
}

function setViewerMuted(muted) {
  state.viewerMuted = Boolean(muted);
  saveAudioSettings();
  renderAudioControls();
  postAudioSettings();
}

function setNavigationLock(enabled) {
  state.navigationLock = Boolean(enabled);
  localStorage.setItem(STORAGE.navigationLock, String(state.navigationLock));
  renderNavigationControls();
  postNavigationLockSettings();
  toast(state.navigationLock ? "已啟用固定台主頁。" : "已停用固定台主頁。");
}

function saveAudioSettings() {
  localStorage.setItem(STORAGE.viewerVolume, String(state.viewerVolume));
  localStorage.setItem(STORAGE.viewerMuted, String(state.viewerMuted));
}

function isAutoHiddenChannel(channel) {
  if (!state.twitchToken) {
    return false;
  }

  const status = state.channelStates.get(channel.login);
  return status === "offline" || status === "missing";
}

function renderEmptyState(visibleCount) {
  const title = refs.emptyState.querySelector("h3");
  const body = refs.emptyState.querySelector("p");

  if (!state.trackedChannels.length) {
    title.textContent = "登入後加入要追的台主";
    body.textContent = "可以一次加入多個 Twitch 帳號。此工具會用 Twitch 原站頁開啟追蹤台主，不再使用內嵌播放器。";
    return;
  }

  if (!visibleCount && state.twitchToken) {
    title.textContent = "目前沒有直播中的台";
    body.textContent = "未開台的台主已自動隱藏，但仍保留在左側追蹤清單；下次回報直播中會再顯示。";
    return;
  }

  title.textContent = "等待台主資訊";
  body.textContent = "Token 更新完成後，未開台台主會自動隱藏，直播中的台會顯示在這裡。";
}

function getGridLayout(count) {
  if (count <= 1) {
    return "single";
  }

  if (count <= 2) {
    return "dual";
  }

  if (count <= 4) {
    return "quad";
  }

  return "nine";
}

function createChannelCard(channel) {
  const card = document.createElement("article");
  card.className = "stream-card";
  card.dataset.cardLogin = channel.login;
  card.dataset.login = channel.login;

  card.innerHTML = `
    <div class="stream-card-head">
      <div class="stream-card-identity">
        <img class="stream-card-avatar" data-field="avatar" alt="">
        <div>
          <h3 data-field="name"></h3>
          <span class="stream-meta" data-field="login"></span>
        </div>
      </div>
      <div class="stream-actions">
        <span class="badge badge-muted" data-field="status">待機</span>
        <span class="badge badge-muted" data-field="quality" hidden>低畫質</span>
        <a class="button button-secondary" data-field="openLink" target="_blank" rel="noreferrer">原站</a>
        <button class="icon-button icon-button-danger" type="button" data-action="remove" title="移除" aria-label="移除">X</button>
      </div>
    </div>
    <div class="original-shell">
      <div>
        <span class="badge badge-online">非內嵌</span>
        <h4 data-field="title">Twitch 原站模式</h4>
        <div class="stream-facts">
          <span data-field="game">分類：未查詢</span>
          <span data-field="viewers">觀眾：未查詢</span>
          <span data-field="started">狀態來源：原站</span>
        </div>
        <p data-field="note">此工具不在 localhost 內嵌 Twitch。請用原站九宮格開啟 twitch.tv 頁面，讓忠誠點以原站登入狀態累積。</p>
      </div>
      <div class="original-actions">
        <a class="button button-primary" data-field="openBigLink" target="_blank" rel="noreferrer">開啟原站</a>
      </div>
    </div>
  `;

  return card;
}

function updateChannelCard(card, channel) {
  const statusInfo = getStatusInfo(channel.login);
  const displayName = channel.display_name || channel.login;
  const stream = channel.stream || null;

  setText(card, "name", displayName);
  setText(card, "login", `@${channel.login}`);
  setText(card, "status", statusInfo.shortLabel);
  setText(card, "title", stream?.title || statusInfo.title);
  setText(card, "game", stream?.game_name ? `分類：${stream.game_name}` : "分類：未查詢");
  setText(card, "viewers", stream ? `觀眾：${formatNumber(stream.viewer_count)}` : "觀眾：未查詢");
  setText(card, "started", stream?.started_at ? `開台：${formatStartedAt(stream.started_at)}` : statusInfo.source);
  setText(card, "note", statusInfo.note);

  const avatar = card.querySelector('[data-field="avatar"]');
  avatar.src = channel.profile_image_url || makeAvatarDataUrl(channel.login);
  avatar.alt = displayName;

  const badge = card.querySelector('[data-field="status"]');
  badge.className = `badge ${statusInfo.badgeClass}`;

  const qualityBadge = card.querySelector('[data-field="quality"]');
  qualityBadge.hidden = !state.lowQuality;

  const openLink = card.querySelector('[data-field="openLink"]');
  openLink.href = `https://www.twitch.tv/${channel.login}`;

  const openBigLink = card.querySelector('[data-field="openBigLink"]');
  openBigLink.href = `https://www.twitch.tv/${channel.login}`;

  const offlineOverlay = card.querySelector('[data-role="offline"]');
  if (offlineOverlay) {
    offlineOverlay.hidden = !statusInfo.overlay;
  }
  if (offlineOverlay && statusInfo.overlay) {
    setText(card, "overlayBadge", statusInfo.overlayBadge);
    setText(card, "overlayTitle", statusInfo.overlayTitle);
    setText(card, "overlayText", statusInfo.overlayText);
    const overlayBadge = card.querySelector('[data-field="overlayBadge"]');
    overlayBadge.className = `badge ${statusInfo.badgeClass}`;
  }
}

function startPlaybackKeepalive() {
  if (state.playbackKeepaliveTimer) {
    return;
  }

  state.playbackKeepaliveTimer = window.setInterval(keepPlaybackActive, PLAYBACK_KEEPALIVE_MS);
}

function startPlayerVisibilityObserver() {
  if (!("IntersectionObserver" in window) || state.playerVisibilityObserver) {
    return;
  }

  state.playerVisibilityObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      const login = entry.target.closest("[data-card-login]")?.dataset.cardLogin;
      if (!login) {
        continue;
      }

      if (entry.isIntersecting) {
        removeOffscreenPlayer(login);
        configurePlayerPlayback(login, true);
      } else {
        addOffscreenPlayer(login);
      }
    }
  }, { threshold: [0, 0.1, 0.5, 1] });
}

function observeCardPlayback(card) {
  if (!state.playerVisibilityObserver || card.dataset.playbackObserved === "true") {
    return;
  }

  const shell = card.querySelector(".player-shell");
  if (!shell) {
    return;
  }

  state.playerVisibilityObserver.observe(shell);
  card.dataset.playbackObserved = "true";
}

function unobserveCardPlayback(card) {
  const login = card.dataset.cardLogin;
  const shell = card.querySelector(".player-shell");
  if (shell && state.playerVisibilityObserver) {
    state.playerVisibilityObserver.unobserve(shell);
  }
  if (login) {
    removeOffscreenPlayer(login);
  }
  card.dataset.playbackObserved = "";
}

function schedulePlaybackResume() {
  if (!state.trackedChannels.length) {
    return;
  }

  if (!state.playbackResumeFrame) {
    state.playbackResumeFrame = window.requestAnimationFrame(() => {
      state.playbackResumeFrame = 0;
      updateAllPlayerVisibility();
      keepPlaybackActiveWithRetry();
    });
  }

  window.clearTimeout(state.playbackResumeTimer);
  state.playbackResumeTimer = window.setTimeout(() => {
    updateAllPlayerVisibility();
    keepPlaybackActiveWithRetry();
  }, PLAYBACK_SCROLL_SETTLE_MS);
}

function updateAllPlayerVisibility() {
  if (!refs.streamGrid) {
    return;
  }

  for (const card of refs.streamGrid.querySelectorAll("[data-card-login]")) {
    updateCardPlaybackVisibility(card);
  }
}

function updateCardPlaybackVisibility(card) {
  const login = card.dataset.cardLogin;
  const shell = card.querySelector(".player-shell");
  if (!login || !shell) {
    return;
  }

  if (isElementInViewport(shell)) {
    removeOffscreenPlayer(login);
  } else {
    addOffscreenPlayer(login);
  }
}

function isElementInViewport(element) {
  const rect = element.getBoundingClientRect();
  const width = window.innerWidth || document.documentElement.clientWidth || 0;
  const height = window.innerHeight || document.documentElement.clientHeight || 0;
  return rect.bottom > 0 && rect.right > 0 && rect.top < height && rect.left < width;
}

function addOffscreenPlayer(login) {
  if (!state.trackedChannels.some((channel) => channel.login === login)) {
    return;
  }

  state.offscreenPlayerLogins.add(login);
  syncOffscreenPlaybackKeepalive();
}

function removeOffscreenPlayer(login) {
  state.offscreenPlayerLogins.delete(login);
  syncOffscreenPlaybackKeepalive();
}

function clearOffscreenPlayers() {
  state.offscreenPlayerLogins.clear();
  syncOffscreenPlaybackKeepalive();
}

function syncOffscreenPlaybackKeepalive() {
  if (state.offscreenPlayerLogins.size) {
    startOffscreenPlaybackKeepalive();
  } else {
    stopOffscreenPlaybackKeepalive();
  }
}

function startOffscreenPlaybackKeepalive() {
  if (state.offscreenPlaybackKeepaliveTimer) {
    return;
  }

  keepOffscreenPlaybackActive();
  state.offscreenPlaybackKeepaliveTimer = window.setInterval(keepOffscreenPlaybackActive, OFFSCREEN_PLAYBACK_KEEPALIVE_MS);
}

function stopOffscreenPlaybackKeepalive() {
  if (!state.offscreenPlaybackKeepaliveTimer) {
    return;
  }

  window.clearInterval(state.offscreenPlaybackKeepaliveTimer);
  state.offscreenPlaybackKeepaliveTimer = 0;
}

function keepOffscreenPlaybackActive() {
  const activeLogins = new Set(state.trackedChannels.map((channel) => channel.login));
  for (const login of Array.from(state.offscreenPlayerLogins)) {
    if (!activeLogins.has(login) || !state.players.has(login)) {
      removeOffscreenPlayer(login);
      continue;
    }

    configurePlayerPlayback(login, false, 0, true);
  }
}

function keepPlaybackActive() {
  for (const channel of state.trackedChannels) {
    configurePlayerPlayback(channel.login);
  }
}

function keepPlaybackActiveWithRetry() {
  for (const channel of state.trackedChannels) {
    configurePlayerPlayback(channel.login, true);
  }
}

function configurePlayerPlayback(login, retry = false, attempt = 0, forcePlay = false) {
  const player = state.players.get(login);
  if (!player) {
    return;
  }

  setPlayerDefaultVolume(player);
  resumePlayer(player, forcePlay);

  if (retry && attempt < 6) {
    window.setTimeout(() => {
      if (state.players.get(login) === player) {
        configurePlayerPlayback(login, true, attempt + 1, forcePlay);
      }
    }, 1000);
  }
}

function setPlayerDefaultVolume(player) {
  // Program volume is handled by the Windows audio session in the native host.
  void player;
}

function resumePlayer(player, forcePlay = false) {
  if (typeof player.play !== "function") {
    return;
  }

  let shouldPlay = true;
  if (!forcePlay && typeof player.isPaused === "function") {
    try {
      shouldPlay = player.isPaused();
    } catch {
      shouldPlay = true;
    }
  }

  if (!shouldPlay) {
    return;
  }

  try {
    player.play();
  } catch {
    // Browser autoplay policy may require the app window to be clicked once.
  }
}

function applyQualityToAllPlayers() {
  for (const channel of state.trackedChannels) {
    applyQualityToPlayer(channel.login);
  }
}

function applyQualityToPlayer(login, attempt = 0) {
  const player = state.players.get(login);
  if (!player || typeof player.setQuality !== "function") {
    return;
  }

  let qualities = [];
  if (typeof player.getQualities === "function") {
    try {
      qualities = player.getQualities() || [];
    } catch {
      qualities = [];
    }
  }

  const target = state.lowQuality ? chooseLowestQuality(qualities) : chooseAutoQuality(qualities);
  if (!target && attempt < 8) {
    window.setTimeout(() => applyQualityToPlayer(login, attempt + 1), 1000);
    return;
  }

  if (!target) {
    return;
  }

  try {
    player.setQuality(target);
  } catch {
    if (attempt < 8) {
      window.setTimeout(() => applyQualityToPlayer(login, attempt + 1), 1000);
    }
  }
}

function chooseLowestQuality(qualities) {
  const options = qualities
    .map(normalizeQuality)
    .filter((quality) => quality.value && quality.value !== "auto" && !quality.isAudioOnly);

  if (!options.length) {
    return "";
  }

  options.sort((a, b) => {
    if (a.pixels !== b.pixels) {
      return a.pixels - b.pixels;
    }
    return a.fps - b.fps;
  });

  return options[0].value;
}

function chooseAutoQuality(qualities) {
  const auto = qualities
    .map(normalizeQuality)
    .find((quality) => quality.value === "auto");
  return auto?.value || "auto";
}

function normalizeQuality(quality) {
  const value = String(quality?.group || quality?.name || quality || "").trim();
  const label = String(quality?.name || quality?.group || quality || "").trim();
  const text = `${value} ${label}`.toLowerCase();
  const pixelsMatch = text.match(/(\d{3,4})p/);
  const fpsMatch = text.match(/(\d{2,3})fps|p(\d{2,3})/);

  return {
    value,
    pixels: pixelsMatch ? Number(pixelsMatch[1]) : Number.MAX_SAFE_INTEGER,
    fps: fpsMatch ? Number(fpsMatch[1] || fpsMatch[2]) : 0,
    isAudioOnly: text.includes("audio")
  };
}

function destroyPlayer(login) {
  state.players.delete(login);
  const card = refs.streamGrid?.querySelector(`[data-card-login="${cssEscape(login)}"]`);
  const mount = card?.querySelector(".player-mount");
  if (mount) {
    mount.innerHTML = "";
  }
}

function getStatusInfo(login) {
  const status = state.channelStates.get(login) || "unknown";
  const labels = {
    online: {
      shortLabel: "直播中",
      badgeClass: "badge-online",
      title: "直播中",
      source: "狀態來源：Token",
      note: "Token 回報直播中。播放和忠誠點仍請使用 Twitch 原站頁。"
    },
    offline: {
      shortLabel: "離線",
      badgeClass: "badge-offline",
      title: "目前離線",
      source: "狀態來源：Token",
      note: "Token 回報目前沒有直播。原站九宮格仍會保留台頁，等下一次開台。",
      overlay: true,
      overlayBadge: "Token 回報離線",
      overlayTitle: "台主目前離線或無法播放",
      overlayText: "Token 只負責資訊；實際觀看請以 Twitch 原站頁為準。"
    },
    missing: {
      shortLabel: "查無",
      badgeClass: "badge-error",
      title: "查無此台主",
      source: "狀態來源：Token",
      note: "Twitch API 找不到這個登入名稱，請確認帳號拼字。",
      overlay: true,
      overlayBadge: "查無台主",
      overlayTitle: "Twitch API 找不到此帳號",
      overlayText: "請確認 channel 名稱或 Twitch 原站網址。"
    },
    error: {
      shortLabel: "錯誤",
      badgeClass: "badge-error",
      title: "資訊更新失敗",
      source: "狀態來源：Token",
      note: "Token 或 Twitch API 連線失敗，請檢查 Token 是否有效。",
      overlay: true,
      overlayBadge: "資訊失敗",
      overlayTitle: "台主資訊更新失敗",
      overlayText: "請檢查 Token 或稍後再試。"
    },
    unknown: {
      shortLabel: "原站",
      badgeClass: "badge-muted",
      title: "Twitch 原站模式",
      source: "狀態來源：未查詢",
      note: "不使用內嵌播放器。請用原站九宮格開啟 twitch.tv 頁面監看與累積忠誠點。"
    }
  };

  return labels[status] || labels.unknown;
}

function ensureInitialChannelStates() {
  for (const channel of state.trackedChannels) {
    if (!state.channelStates.has(channel.login)) {
      state.channelStates.set(channel.login, channel.stream ? "online" : "unknown");
    }
  }
}

function isChannelKnownOnline(login) {
  return state.channelStates.get(login) === "online";
}

function openClaimChannel() {
  const channel = getClaimChannel();
  if (!channel) {
    toast("請先加入至少一個追蹤台主。");
    return;
  }

  window.open(`https://www.twitch.tv/${channel.login}`, "_blank", "noreferrer");
}

function openPointAccumulationMode() {
  if (!state.trackedChannels.length) {
    toast("請先加入至少一個追蹤台主。");
    return;
  }

  openOriginalGrid(state.trackedChannels, "已用 Twitch 原站頁開啟九宮格。未開台台主會自動隱藏；請確認原站已登入並保持播放。");
}

function openOriginalGrid(channels = state.trackedChannels, successMessage = "已開啟 Twitch 原站九宮格。") {
  const visibleChannels = getVisibleChannels(channels);
  if (!channels.length) {
    toast("請先加入至少一個追蹤台主。");
    return;
  }

  if (!visibleChannels.length) {
    toast(state.twitchToken ? "目前沒有直播中的台；未開台台主已自動隱藏。" : "目前沒有可開啟的台主。");
    return;
  }

  const hiddenCount = channels.length - visibleChannels.length;
  const logins = visibleChannels.map((channel) => channel.login).join(",");
  let message = visibleChannels.length > 9 ? `${successMessage} 已載入 ${visibleChannels.length} 台，可在主視窗往下捲動。` : successMessage;
  if (hiddenCount > 0) {
    message += ` 已隱藏 ${hiddenCount} 台未開台。`;
  }

  if (postViewerChannels(visibleChannels)) {
    toast(`${message} 已顯示在主視窗右側。`);
    return;
  }

  fetch(`/open-original-grid?channels=${encodeURIComponent(logins)}`)
    .then((response) => {
      if (!response.ok) {
        throw new Error("open-original-grid failed");
      }
      toast(message);
    })
    .catch(() => {
      openChannels(visibleChannels, `${message} 若未自動排列，請手動排列 Twitch 原站視窗。`);
    });
}

function openAllTrackedChannels() {
  if (!state.trackedChannels.length) {
    toast("請先加入至少一個追蹤台主。");
    return;
  }

  openChannels(state.trackedChannels, "已開啟全部 Twitch 原站頁。");
}

function openLiveTrackedChannels() {
  if (!state.trackedChannels.length) {
    toast("請先加入至少一個追蹤台主。");
    return;
  }

  const liveChannels = state.trackedChannels.filter((channel) => state.channelStates.get(channel.login) === "online");
  if (!liveChannels.length) {
    toast(state.twitchToken ? "目前沒有 Token 回報直播中的台。" : "尚未設定 Token，無法判斷哪些台正在直播。");
    return;
  }

  openOriginalGrid(liveChannels, "已用 Twitch 原站頁開啟直播中台主。");
}

function openChannels(channels, successMessage) {
  let blocked = 0;
  for (const channel of channels) {
    const handle = window.open(`https://www.twitch.tv/${channel.login}`, "_blank", "noreferrer");
    if (!handle) {
      blocked++;
    }
  }

  if (blocked) {
    toast("瀏覽器阻擋了部分 Twitch 原站頁。請允許此工具開啟彈出視窗後再試，否則忠誠點頁可能沒開完整。");
  } else {
    toast(successMessage);
  }
}

function getClaimChannel() {
  return state.trackedChannels.find((channel) => state.channelStates.get(channel.login) === "online")
    || state.trackedChannels[0]
    || null;
}

function parseChannelInput(input) {
  const seen = new Set();
  return String(input || "")
    .split(/[\s,，;；]+/)
    .map(normalizeChannel)
    .filter(Boolean)
    .filter((login) => {
      if (seen.has(login)) {
        return false;
      }
      seen.add(login);
      return true;
    });
}

function normalizeChannel(input) {
  let value = String(input || "").trim();
  value = value.replace(/^https?:\/\/(www\.)?twitch\.tv\//i, "");
  value = value.split(/[/?#]/)[0];
  value = value.replace(/^@/, "").toLowerCase();
  value = value.replace(/[^a-z0-9_]/g, "");

  if (value.length < 3 || value.length > 25) {
    return "";
  }

  return value;
}

function loadViewerVolume() {
  return normalizeVolume(localStorage.getItem(STORAGE.viewerVolume) ?? DEFAULT_VOLUME);
}

function normalizeVolume(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) {
    return DEFAULT_VOLUME;
  }

  return Math.min(1, Math.max(0, number));
}

function loadTrackedChannels() {
  const stored = readJson(STORAGE.trackedChannels);
  if (Array.isArray(stored)) {
    return stored
      .map(normalizeStoredChannel)
      .filter(Boolean)
      .filter(uniqueChannelFilter());
  }

  const oldLogin = localStorage.getItem(STORAGE.oldPinnedChannel);
  const oldUser = readJson(STORAGE.oldPinnedUser);
  if (!oldLogin) {
    return [];
  }

  return [{
    id: oldUser?.id || oldLogin,
    login: oldLogin.toLowerCase(),
    display_name: oldUser?.display_name || oldLogin,
    profile_image_url: oldUser?.profile_image_url || "",
    added_at: new Date().toISOString()
  }];
}

function normalizeStoredChannel(channel) {
  const login = normalizeChannel(channel?.login);
  if (!login) {
    return null;
  }

  return {
    id: String(channel.id || login),
    login,
    display_name: String(channel.display_name || login),
    profile_image_url: String(channel.profile_image_url || ""),
    description: String(channel.description || ""),
    stream: normalizeStoredStream(channel.stream),
    added_at: channel.added_at || new Date().toISOString()
  };
}

function normalizeStoredStream(stream) {
  if (!stream || typeof stream !== "object") {
    return null;
  }

  return {
    id: String(stream.id || ""),
    title: String(stream.title || ""),
    game_name: String(stream.game_name || ""),
    viewer_count: Number(stream.viewer_count || 0),
    started_at: String(stream.started_at || ""),
    language: String(stream.language || "")
  };
}

function uniqueChannelFilter() {
  const seen = new Set();
  return (channel) => {
    if (seen.has(channel.login)) {
      return false;
    }
    seen.add(channel.login);
    return true;
  };
}

function saveTrackedChannels() {
  localStorage.setItem(STORAGE.trackedChannels, JSON.stringify(state.trackedChannels));
}

function readJson(key) {
  try {
    const value = localStorage.getItem(key);
    return value ? JSON.parse(value) : null;
  } catch {
    return null;
  }
}

function setText(scope, field, text) {
  const node = scope.querySelector(`[data-field="${field}"]`);
  if (node) {
    node.textContent = text;
  }
}

function formatNumber(value) {
  return new Intl.NumberFormat("zh-Hant").format(Number(value || 0));
}

function formatStartedAt(value) {
  const started = new Date(value);
  if (Number.isNaN(started.getTime())) {
    return "未知";
  }

  const minutes = Math.max(0, Math.floor((Date.now() - started.getTime()) / 60000));
  if (minutes < 60) {
    return `${minutes} 分鐘前`;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest ? `${hours} 小時 ${rest} 分鐘前` : `${hours} 小時前`;
}

function safeId(value) {
  return String(value).replace(/[^a-z0-9_-]/gi, "-");
}

function cssEscape(value) {
  if (window.CSS?.escape) {
    return window.CSS.escape(value);
  }
  return String(value).replace(/"/g, '\\"');
}

function makeAvatarDataUrl(login) {
  const initial = String(login || "T").slice(0, 1).toUpperCase();
  const hue = Math.abs(hashString(login)) % 360;
  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 96 96">
      <rect width="96" height="96" rx="48" fill="hsl(${hue} 70% 42%)"/>
      <text x="48" y="58" text-anchor="middle" font-size="38" font-family="Segoe UI, Arial" font-weight="700" fill="white">${initial}</text>
    </svg>
  `;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

function hashString(value) {
  let hash = 0;
  for (let index = 0; index < String(value).length; index++) {
    hash = ((hash << 5) - hash) + String(value).charCodeAt(index);
    hash |= 0;
  }
  return hash;
}

function toast(message) {
  refs.toast.textContent = message;
  refs.toast.hidden = false;
  window.clearTimeout(toast.timer);
  toast.timer = window.setTimeout(() => {
    refs.toast.hidden = true;
  }, 4200);
}
