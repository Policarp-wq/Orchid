// Mock of the real client. Same surface and same errors as docs/orchid-design/REMOTE-DOD.md, with a fake "PC" kept in memory.
// Delete this folder and point index.html at the real client when the server exists.
(() => {
  const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // 32 symbols, no I O 0 1; 256 % 32 == 0 so random bytes map without bias
  const WORDS = ['Fern', 'Moss', 'Iris', 'Lily', 'Reed', 'Sage', 'Pine', 'Dune'];
  const CODE_TTL_MS = 5 * 60_000, REQUEST_TTL_MS = 2 * 60_000;
  const MAX_FAILED = 5, FAIL_WINDOW_MS = 60_000, LOCKOUT_MS = 5 * 60_000;
  const BPM_MIN = 30, BPM_MAX = 240, PULSES = [1, 2, 4, 8, 16];
  const STOP_SETTLE_MS = 400, SEEN_COMMANDS = 50;

  class ApiError extends Error {
    constructor(status, code, extra = {}) { super(code); Object.assign(this, { status, code }, extra); }
  }
  const pick = (n, set) => Array.from(crypto.getRandomValues(new Uint8Array(n)), b => set[b % set.length]).join('');
  const hex = n => Array.from(crypto.getRandomValues(new Uint8Array(n)), b => b.toString(16).padStart(2, '0')).join('');
  const delay = () => new Promise(r => setTimeout(r, 80 + Math.random() * 180));

  const pc = window.MockPc = {
    sharing: true, offline: false, midiAvailable: true,
    code: null, pending: null, devices: [], failures: [], lockedUntil: 0,
    session: { status: 'idle', startedAt: 0, elapsedMs: 0, bpm: 120, pulse: 4, strongBeats: [true, false, false, false], error: null },
    listeners: new Set(),
    changed() { this.listeners.forEach(f => f()); },
    newCode() { this.code = { value: pick(8, ALPHABET), expiresAt: Date.now() + CODE_TTL_MS }; this.changed(); },
    activePending() {
      const p = this.pending;
      if (p?.status === 'pending' && Date.now() > p.createdAt + REQUEST_TTL_MS) p.status = 'expired';
      return p?.status === 'pending' ? p : null;
    },
    approve() {
      const p = this.activePending();
      if (!p) return;
      p.status = 'approved';
      p.token = hex(32);
      this.devices.push({ id: hex(4), name: p.device, token: p.token, connectedAt: Date.now(), lastCommand: null, seen: new Map() });
      this.changed();
    },
    deny() { const p = this.activePending(); if (p) { p.status = 'denied'; this.changed(); } },
    revoke(id) { this.devices = this.devices.filter(d => d.id !== id); this.changed(); },
    setMidiAvailable(on) {
      this.midiAvailable = on;
      if (!on && this.session.status === 'recording') {
        finishRecording();
        this.session.error = { code: 'midi_disconnected', message: 'MIDI input disconnected. Recording stopped and kept.' };
      }
      this.changed();
    },
  };
  pc.newCode();

  function finishRecording() {
    const s = pc.session;
    s.elapsedMs = Date.now() - s.startedAt;
    s.status = 'stopping';
    setTimeout(() => { s.status = 'idle'; pc.changed(); }, STOP_SETTLE_MS);
  }

  function snapshot() {
    const s = pc.session;
    return {
      version: 1,
      status: s.status,
      elapsedMs: s.status === 'recording' ? Date.now() - s.startedAt : s.elapsedMs,
      serverTimeMs: Date.now(),
      bpm: s.bpm,
      pulseDenominator: s.pulse,
      strongBeats: [...s.strongBeats],
      meter: '4/4',
      midiInput: pc.midiAvailable ? 'Digital Piano · USB MIDI' : null,
      canStart: pc.midiAvailable && s.status === 'idle',
      locked: s.status !== 'idle',
      error: s.error,
    };
  }

  let token = null;
  const reach = async () => { await delay(); if (pc.offline || !pc.sharing) throw new ApiError(0, 'network'); };
  const authenticate = () => pc.devices.find(d => d.token === token) ?? (() => { throw new ApiError(401, 'unauthorized'); })();

  function failedAttempt(now) {
    pc.failures = pc.failures.filter(t => now - t < FAIL_WINDOW_MS).concat(now);
    if (pc.failures.length >= MAX_FAILED) { pc.lockedUntil = now + LOCKOUT_MS; pc.failures = []; pc.changed(); }
    return new ApiError(404, 'invalid_code');
  }

  window.OrchidApi = {
    ApiError,

    async requestPairing(code, deviceName) {
      await reach();
      const now = Date.now();
      if (now < pc.lockedUntil) throw new ApiError(429, 'too_many_attempts', { retryAfterSec: Math.ceil((pc.lockedUntil - now) / 1000) });
      const normalized = String(code).replace(/[\s-]/g, '').toUpperCase();
      if (!/^[A-HJ-NP-Z2-9]{8}$/.test(normalized)) throw new ApiError(400, 'invalid_format');
      if (!pc.code || now > pc.code.expiresAt || normalized !== pc.code.value) throw failedAttempt(now);
      if (pc.activePending()) throw new ApiError(409, 'request_pending');
      pc.code = null; // single use
      pc.pending = {
        id: hex(8), status: 'pending', createdAt: now, ip: '192.168.1.41',
        device: String(deviceName).replace(/[\u0000-\u001f<>]/g, '').slice(0, 40) || 'Unknown device',
        verification: `${WORDS[crypto.getRandomValues(new Uint8Array(1))[0] % WORDS.length]} ${10 + crypto.getRandomValues(new Uint8Array(1))[0] % 90}`,
      };
      pc.changed();
      return { requestId: pc.pending.id, verification: pc.pending.verification, expiresAt: now + REQUEST_TTL_MS };
    },

    waitForApproval(requestId, signal) {
      return new Promise((resolve, reject) => {
        const timer = setInterval(() => {
          if (pc.offline || !pc.sharing) { clearInterval(timer); return reject(new ApiError(0, 'network')); }
          const p = pc.pending?.id === requestId ? pc.pending : null;
          if (!p) { clearInterval(timer); return resolve('expired'); }
          pc.activePending(); // flips to 'expired' once the request TTL has passed
          if (p.status === 'pending') return;
          clearInterval(timer);
          if (p.status === 'approved') token = p.token;
          resolve(p.status);
        }, 500);
        signal.addEventListener('abort', () => { clearInterval(timer); reject(new DOMException('Aborted', 'AbortError')); });
      });
    },

    async cancelPairing(requestId) {
      await reach();
      if (pc.pending?.id === requestId && pc.pending.status === 'pending') { pc.pending = null; pc.changed(); }
    },

    hasToken: () => token !== null,

    async getState() { await reach(); authenticate(); return snapshot(); },

    async sendCommand(command) {
      await reach();
      const device = authenticate();
      const { type, commandId } = command;
      if (typeof commandId !== 'string' || commandId.length < 8 || commandId.length > 64) throw new ApiError(400, 'invalid_command');
      if (device.seen.has(commandId)) return snapshot(); // retry of a command that already applied
      const s = pc.session;
      const idle = s.status === 'idle';
      switch (type) {
        case 'start':
          if (s.status === 'recording') throw new ApiError(409, 'already_recording');
          if (!idle) throw new ApiError(409, 'locked_while_recording');
          if (!pc.midiAvailable) throw new ApiError(409, 'no_midi_input');
          Object.assign(s, { status: 'recording', startedAt: Date.now(), elapsedMs: 0, error: null });
          break;
        case 'stop':
          if (s.status !== 'recording') throw new ApiError(409, 'not_recording');
          finishRecording();
          break;
        case 'setBpm':
          if (!idle) throw new ApiError(409, 'locked_while_recording');
          if (!Number.isInteger(command.bpm) || command.bpm < BPM_MIN || command.bpm > BPM_MAX) throw new ApiError(422, 'bpm_out_of_range');
          s.bpm = command.bpm;
          break;
        case 'setPulse':
          if (!idle) throw new ApiError(409, 'locked_while_recording');
          if (!PULSES.includes(command.pulseDenominator)) throw new ApiError(422, 'pulse_invalid');
          s.pulse = command.pulseDenominator;
          s.strongBeats = Array.from({ length: s.pulse }, (_, beat) => beat === 0); // a new pulse resets the pattern
          break;
        case 'setStrongBeats':
          if (!idle) throw new ApiError(409, 'locked_while_recording');
          if (!Array.isArray(command.strongBeats) || command.strongBeats.length !== s.pulse
              || !command.strongBeats.every(flag => typeof flag === 'boolean')) throw new ApiError(422, 'strong_beats_invalid');
          s.strongBeats = [...command.strongBeats];
          break;
        default:
          throw new ApiError(400, 'unknown_command');
      }
      device.seen.set(commandId, true);
      if (device.seen.size > SEEN_COMMANDS) device.seen.delete(device.seen.keys().next().value);
      device.lastCommand = type;
      pc.changed();
      return snapshot();
    },

    async disconnect() {
      const had = token;
      token = null;
      if (!had) return;
      await reach();
      pc.devices = pc.devices.filter(d => d.token !== had);
      pc.changed();
    },
  };
})();
