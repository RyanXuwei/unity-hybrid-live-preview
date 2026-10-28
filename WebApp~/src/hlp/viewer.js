// Hybrid Live Preview viewer (package-owned; refreshed from the Unity package, do not edit).
// Receives the Unity camera over WebRTC into a <video> and forwards pointer, wheel and keyboard
// input back to Unity, so the video behaves like a WebGL build's canvas. window.HybridBridge messages
// travel on their own data channel.

import { bridge } from './bridge.js';

export function startViewer(video, { onChange } = {}) {
  const state = {
    signaling: 'connecting',
    unity: false,
    peer: 'idle',
    codec: '-',
    width: 0,
    height: 0,
    fps: 0,
    framesDecoded: 0,
  };

  const set = (patch) => {
    Object.assign(state, patch);
    onChange?.(state);
  };

  let ws = null;
  let pc = null;
  let channel = null;
  let bridgeTransport = null;
  let retry = 0;
  let pendingCandidates = [];

  function send(msg) {
    if (ws?.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
  }

  function connect() {
    ws = new WebSocket(`ws://${location.host}/signaling`);
    ws.onopen = () => {
      retry = 0;
      set({ signaling: 'open' });
      send({ type: 'hello', role: 'viewer' });
    };
    ws.onmessage = (event) => handle(JSON.parse(event.data));
    ws.onclose = () => {
      closePeer();
      set({ signaling: 'closed', unity: false });
      setTimeout(connect, Math.min(5000, 500 * 2 ** retry++));
    };
  }

  async function handle(msg) {
    switch (msg.type) {
      case 'welcome':
      case 'status':
        set({ unity: msg.unity });
        if (msg.unity) negotiate();
        else closePeer();
        break;
      case 'answer':
        if (!pc) return;
        await pc.setRemoteDescription({ type: 'answer', sdp: msg.sdp });
        for (const c of pendingCandidates) await addCandidate(c);
        pendingCandidates = [];
        break;
      case 'candidate':
        if (!pc || !msg.candidate) return;
        if (pc.remoteDescription) await addCandidate(msg);
        else pendingCandidates.push(msg);
        break;
    }
  }

  function addCandidate(msg) {
    return pc
      .addIceCandidate({
        candidate: msg.candidate,
        sdpMid: msg.sdpMid || null,
        sdpMLineIndex: msg.sdpMLineIndex >= 0 ? msg.sdpMLineIndex : null,
      })
      .catch((e) => console.warn('[hlp] addIceCandidate', e));
  }

  async function negotiate() {
    closePeer();
    const peer = new RTCPeerConnection();
    pc = peer;
    peer.addTransceiver('video', { direction: 'recvonly' });
    openInputChannel(peer.createDataChannel('input', { ordered: true }));
    openBridgeChannel(peer.createDataChannel('bridge', { ordered: true }));
    peer.ontrack = (event) => {
      video.srcObject = event.streams[0] ?? new MediaStream([event.track]);
    };
    peer.onicecandidate = (event) => {
      if (!event.candidate) return;
      send({
        type: 'candidate',
        candidate: event.candidate.candidate,
        sdpMid: event.candidate.sdpMid ?? '',
        sdpMLineIndex: event.candidate.sdpMLineIndex ?? -1,
      });
    };
    peer.onconnectionstatechange = () => {
      if (pc === peer) set({ peer: peer.connectionState });
    };

    const offer = await peer.createOffer();
    await peer.setLocalDescription(offer);
    send({ type: 'offer', sdp: offer.sdp, ...viewport() });
    set({ peer: 'offering' });
  }

  // Like a WebGL build's canvas, the stream follows the window in device pixels; Unity caps it at the
  // Player Settings default canvas size and keeps the aspect.
  function viewport() {
    const dpr = window.devicePixelRatio || 1;
    return { width: Math.round(innerWidth * dpr), height: Math.round(innerHeight * dpr) };
  }

  let resizeTimer = 0;
  window.addEventListener('resize', () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => {
      if (pc) send({ type: 'viewport', ...viewport() });
    }, 250);
  });

  function closePeer() {
    pendingCandidates = [];
    if (!pc) return;
    releaseAll();
    closeBridge();
    channel = null;
    pc.close();
    pc = null;
    video.srcObject = null;
    set({ peer: 'idle', codec: '-', width: 0, height: 0, fps: 0, framesDecoded: 0 });
  }

  async function pollStats() {
    if (pc) {
      const report = await pc.getStats();
      report.forEach((s) => {
        if (s.type !== 'inbound-rtp' || s.kind !== 'video') return;
        const codec = s.codecId ? report.get(s.codecId)?.mimeType?.replace('video/', '') : '-';
        set({
          codec: codec ?? '-',
          width: s.frameWidth ?? 0,
          height: s.frameHeight ?? 0,
          fps: s.framesPerSecond ?? 0,
          framesDecoded: s.framesDecoded ?? 0,
        });
      });
    }
    setTimeout(pollStats, 1000);
  }

  // ---------------------------------------------------------------------------------------------
  // Input forwarding: the video behaves like a WebGL canvas. Pointer (mouse, pen, touch), wheel and
  // keyboard events go to Unity over the "input" data channel; Unity feeds them to Input System devices.
  // Positions are normalized to the video box, origin top-left.

  const pointers = new Map();

  function openInputChannel(dc) {
    channel = dc;
    dc.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      if (msg.t === 'cursor') applyCursor(msg);
    };
    dc.onclose = () => {
      if (channel === dc) channel = null;
    };
  }

  function sendInput(msg) {
    if (channel?.readyState === 'open') channel.send(JSON.stringify(msg));
  }

  function pointerMessage(t, e) {
    const r = video.getBoundingClientRect();
    return {
      t,
      id: e.pointerId,
      k: e.pointerType || 'mouse',
      bs: e.buttons,
      x: (e.clientX - r.left) / r.width,
      y: (e.clientY - r.top) / r.height,
      mx: e.movementX / r.width,
      my: e.movementY / r.height,
      p: e.pressure,
    };
  }

  video.addEventListener('pointerdown', (e) => {
    video.focus({ preventScroll: true });
    video.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, e.pointerType);
    sendInput(pointerMessage('pd', e));
    e.preventDefault();
  });
  video.addEventListener('pointermove', (e) => {
    // Coalesced moves are merged; Unity only needs the latest position and the summed movement.
    sendInput(pointerMessage('pm', e));
  });
  for (const type of ['pointerup', 'pointercancel']) {
    video.addEventListener(type, (e) => {
      pointers.delete(e.pointerId);
      sendInput(pointerMessage(type === 'pointerup' ? 'pu' : 'pc', e));
    });
  }
  video.addEventListener('wheel', (e) => {
    const scale = e.deltaMode === 1 ? 40 : e.deltaMode === 2 ? innerHeight : 1;
    sendInput({ t: 'wh', dx: e.deltaX * scale, dy: e.deltaY * scale });
    e.preventDefault();
  }, { passive: false });
  video.addEventListener('contextmenu', (e) => e.preventDefault());

  // Like a canvas, the video only takes keys while it has focus; overlay inputs keep theirs.
  function keyMessage(t, e) {
    return { t, c: e.code, key: e.key.length === 1 ? e.key : '' };
  }
  video.addEventListener('keydown', (e) => {
    sendInput(keyMessage('kd', e));
    if (!e.ctrlKey && !e.metaKey) e.preventDefault();
  });
  video.addEventListener('keyup', (e) => {
    sendInput(keyMessage('ku', e));
    e.preventDefault();
  });
  video.addEventListener('blur', () => sendInput({ t: 'blur' }));
  window.addEventListener('blur', () => sendInput({ t: 'blur' }));

  function releaseAll() {
    sendInput({ t: 'blur' });
    pointers.clear();
  }

  // Unity asks for pointer lock when a script sets Cursor.lockState, as a WebGL build does.
  function applyCursor(msg) {
    video.style.cursor = msg.visible === false ? 'none' : '';
    if (msg.lock && document.pointerLockElement !== video) {
      video.requestPointerLock?.()?.catch?.(() => {});
    } else if (!msg.lock && document.pointerLockElement === video) {
      document.exitPointerLock();
    }
  }

  // ---------------------------------------------------------------------------------------------
  // HybridBridge: window.HybridBridge messages go to Unity over the "bridge" data channel. The bridge
  // queues while no channel is open, so the page can send before Unity is ready.

  function openBridgeChannel(dc) {
    const transport = {
      post: (type, data) => {
        if (dc.readyState === 'open') dc.send(JSON.stringify({ type, data }));
      },
    };
    dc.onopen = () => {
      bridgeTransport = transport;
      bridge._attach(transport);
    };
    dc.onclose = () => {
      if (bridgeTransport === transport) closeBridge();
    };
    dc.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      bridge._deliver(msg.type, msg.data);
    };
  }

  function closeBridge() {
    if (!bridgeTransport) return;
    bridge._detach(bridgeTransport);
    bridgeTransport = null;
  }

  connect();
  pollStats();
  onChange?.(state);
  return state;
}
