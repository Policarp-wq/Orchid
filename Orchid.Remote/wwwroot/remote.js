// Remote control page. All data comes through window.OrchidApi; the contract is in docs/orchid-design/REMOTE-DOD.md.
// The page is served over plain HTTP on the LAN, so it must not use secure-context-only APIs (crypto.randomUUID, wake lock, clipboard).
(() => {
  const api = window.OrchidApi;
  const $ = id => document.getElementById(id);
  // The field takes any letter or digit; whether it belongs to the code alphabet is the server's call (400 invalid_format).
  const CODE_CHAR = /[A-Z0-9]/g;
  const CODE_FORMAT = /^[A-Z0-9]{8}$/;
  const PULSES = [1, 2, 4, 8, 16];
  const BPM_MIN = 30, BPM_MAX = 240;
  const POLL_MS = 1000, LOST_AFTER_FAILURES = 3, BPM_DEBOUNCE_MS = 300, HOLD_DELAY_MS = 400, HOLD_REPEAT_MS = 80;

  const MESSAGES = {
    invalid_format: 'Codes never contain 0, 1, I or O. Check what you typed.',
    invalid_code: 'This code is wrong, expired or already used.',
    too_many_attempts: e => `Too many attempts. Try again in ${e.retryAfterSec} s.`,
    request_pending: 'Another device is already waiting for approval on the PC.',
    locked_while_recording: 'BPM and pulse cannot change while recording.',
    no_midi_input: 'Select a MIDI input in Orchid on the PC first.',
    already_recording: 'Already recording.',
    not_recording: 'Not recording.',
    bpm_out_of_range: `BPM must be between ${BPM_MIN} and ${BPM_MAX}.`,
    pulse_invalid: 'Unsupported pulse.',
    strong_beats_invalid: 'The strong beats no longer match the pulse. Try again.',
    network: 'Cannot reach Orchid. Check Wi-Fi and that sharing is on.',
  };
  const explain = e => {
    const m = MESSAGES[e.code];
    return typeof m === 'function' ? m(e) : m ?? e.message ?? 'Something went wrong.';
  };
  const say = (el, text) => { el.textContent = text ?? ''; el.hidden = !text; };
  const show = name => document.querySelectorAll('[data-view]').forEach(el => { el.hidden = el.dataset.view !== name; });
  const randomId = () => Array.from(crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2, '0')).join('');

  function deviceLabel() {
    const ua = navigator.userAgent;
    const os = /Android/.test(ua) ? 'Android' : /iPhone|iPad/.test(ua) ? 'iOS' : /Windows/.test(ua) ? 'Windows' : /Mac/.test(ua) ? 'macOS' : 'Device';
    const browser = /Edg\//.test(ua) ? 'Edge' : /Firefox\//.test(ua) ? 'Firefox' : /Chrome\//.test(ua) ? 'Chrome' : /Safari\//.test(ua) ? 'Safari' : 'Browser';
    return `${os} · ${browser}`;
  }

  // ---- Pairing ----
  const cells = Array.from({ length: 8 }, (_, i) => {
    const cell = document.createElement('input');
    Object.assign(cell, { maxLength: 1, autocomplete: 'off', autocapitalize: 'characters', inputMode: 'text' });
    cell.setAttribute('aria-label', `Character ${i + 1}`);
    $('cells').append(cell);
    return cell;
  });
  const enteredCode = () => cells.map(c => c.value).join('');
  const clean = text => (text.toUpperCase().match(CODE_CHAR) ?? []).join('');
  const syncConnect = () => { $('connect').disabled = !CODE_FORMAT.test(enteredCode()); };

  cells.forEach((cell, i) => {
    cell.addEventListener('input', () => {
      cell.value = clean(cell.value).slice(-1);
      if (cell.value) cells[i + 1]?.focus();
      syncConnect();
    });
    cell.addEventListener('keydown', e => {
      if (e.key === 'Backspace' && !cell.value) cells[i - 1]?.focus();
      if (e.key === 'Enter' && !$('connect').disabled) connect();
    });
    cell.addEventListener('paste', e => {
      const text = clean(e.clipboardData.getData('text')).slice(0, 8);
      if (!text) return;
      e.preventDefault();
      cells.forEach((c, j) => { c.value = text[j] ?? ''; });
      cells[Math.min(text.length, 7)].focus();
      syncConnect();
    });
  });

  let pairing = null;
  function showPair(message) {
    pairing = null; // after cancel() has already read it
    cells.forEach(c => { c.value = ''; });
    syncConnect();
    say($('pair-error'), message);
    show('pair');
    cells[0].focus();
  }

  async function connect() {
    say($('pair-error'), '');
    $('connect').disabled = true;
    const label = deviceLabel();
    let request;
    try {
      request = await api.requestPairing(enteredCode(), label);
    } catch (e) {
      say($('pair-error'), explain(e));
      syncConnect();
      return;
    }
    pairing = { abort: new AbortController(), requestId: request.requestId };
    $('verification').textContent = request.verification;
    $('device-name').textContent = label;
    say($('wait-error'), '');
    show('waiting');
    try {
      const outcome = await api.waitForApproval(request.requestId, pairing.abort.signal);
      if (outcome === 'approved') return startControl();
      showPair(outcome === 'denied' ? 'The PC denied this device.' : 'The request expired. Enter a new code.');
    } catch (e) {
      if (e.name === 'AbortError') return;
      say($('wait-error'), explain(e));
    }
  }
  $('connect').addEventListener('click', connect);
  $('cancel').addEventListener('click', () => {
    pairing.abort.abort();
    api.cancelPairing(pairing.requestId).catch(() => {});
    showPair();
  });

  // ---- Control ----
  let state = null, receivedAt = 0, failures = 0, busy = false;
  let pendingBpm = null, bpmTimer = 0, pollTimer = 0, tickTimer = 0;
  let pendingBeats = null, beatsTimer = 0, beatBars = null, beatsModal = null;

  const pulseButtons = PULSES.map(n => {
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = `1/${n}`;
    b.addEventListener('click', () => { pendingBeats = null; send({ type: 'setPulse', pulseDenominator: n }); });
    $('pulse').append(b);
    return b;
  });

  const formatElapsed = ms => {
    const t = Math.floor(ms / 100);
    const m = Math.floor(t / 600), s = Math.floor(t / 10) % 60;
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}.${t % 10}`;
  };
  // Display only: interpolates from the last snapshot, never used to timestamp notes.
  const elapsedNow = () => state.status === 'recording' ? state.elapsedMs + (performance.now() - receivedAt) : state.elapsedMs;
  const tick = () => { if (state) $('elapsed').textContent = formatElapsed(elapsedNow()); };

  const describeBeats = beats => {
    const strong = beats.flatMap((flag, i) => flag ? [i + 1] : []);
    if (!strong.length) return 'No strong beats';
    if (strong.length === beats.length) return `Every beat is strong (${beats.length})`;
    return strong.length === 1 ? `Beat ${strong[0]} of ${beats.length}` : `Beats ${strong.join(', ')} of ${beats.length}`;
  };

  // The pattern is edited in a modal; the control screen only shows a summary so it stays uncluttered.
  function renderBeats() {
    const beats = pendingBeats ?? state.strongBeats;
    $('beats-summary').textContent = describeBeats(beats);
    $('beats-edit').disabled = state.locked || busy;
    beatBars?.setBeats(beats);
    beatBars?.setDisabled(state.locked || busy);
  }

  function changeBeats(next) {
    if (!state || state.locked) return;
    pendingBeats = next;
    renderBeats();
    clearTimeout(beatsTimer);
    beatsTimer = setTimeout(async () => {
      await send({ type: 'setStrongBeats', strongBeats: next });
      if (pendingBeats === next) pendingBeats = null;
      render();
    }, BPM_DEBOUNCE_MS);
  }

  $('beats-edit').addEventListener('click', () => {
    if (!state) return;
    beatBars = OrchidUi.createBeatBars({ beats: pendingBeats ?? state.strongBeats, onChange: changeBeats });
    const hint = Object.assign(document.createElement('p'), {
      className: 'muted',
      textContent: 'A tall bar is a strong beat, a short bar is weak. Tap the top of a bar to switch it; any beat can be strong.',
    });
    const content = document.createElement('div');
    content.style.display = 'grid';
    content.style.gap = '16px';
    content.append(beatBars.element, hint);
    beatsModal = OrchidUi.openModal({ title: 'Strong beats', content, onClose: () => { beatsModal = null; beatBars = null; } });
  });

  function render() {
    if (!state) return;
    const recording = state.status === 'recording', stopping = state.status === 'stopping';
    $('status').textContent = recording ? 'Recording' : stopping ? 'Stopping…'
      : state.elapsedMs > 0 ? 'Stopped · recording kept in memory' : `Ready · ${state.meter}`;
    $('bpm').textContent = pendingBpm ?? state.bpm;
    pulseButtons.forEach((b, i) => {
      b.setAttribute('aria-pressed', String(PULSES[i] === state.pulseDenominator));
      b.disabled = state.locked || busy;
    });
    renderBeats();
    $('minus').disabled = $('plus').disabled = state.locked;
    $('lock').hidden = !state.locked;
    $('midi').textContent = state.midiInput ?? 'None selected on the PC';
    $('dot').className = 'dot ' + (recording ? 'rec' : 'ok');
    $('go-label').textContent = recording ? 'Stop' : 'Start session';
    $('go-icon').innerHTML = recording ? '<rect x="5" y="5" width="14" height="14" rx="2"/>' : '<circle cx="12" cy="12" r="9"/>';
    if (state.error) say($('control-error'), state.error.message);
    $('go').disabled = busy || stopping || (!recording && !state.canStart);
    tick();
  }

  function applyState(next) {
    state = next;
    receivedAt = performance.now();
    failures = 0;
    $('conn').textContent = 'Connected';
    render();
  }

  async function refresh() {
    try {
      applyState(await api.getState());
    } catch (e) {
      if (e.code === 'unauthorized') return lost('removed');
      if (++failures >= LOST_AFTER_FAILURES) return lost('unreachable');
      $('conn').textContent = 'Reconnecting…';
      $('dot').className = 'dot bad';
    }
  }

  function schedulePoll(delay) {
    pollTimer = setTimeout(async () => { await refresh(); if (pollTimer) schedulePoll(POLL_MS); }, delay);
  }

  function startControl() {
    stopControl();
    state = null;
    failures = 0;
    show('control');
    schedulePoll(0);
    tickTimer = setInterval(tick, 100);
  }

  function stopControl() {
    clearTimeout(pollTimer); pollTimer = 0;
    clearInterval(tickTimer);
    clearTimeout(bpmTimer);
    clearTimeout(beatsTimer);
    beatsModal?.close();
    pendingBpm = null;
    pendingBeats = null;
  }

  async function send(command) {
    busy = true;
    say($('control-error'), '');
    render();
    try {
      applyState(await api.sendCommand({ ...command, commandId: randomId() }));
    } catch (e) {
      if (e.code === 'unauthorized') return lost('removed');
      say($('control-error'), explain(e));
      await refresh();
    } finally {
      busy = false;
      render();
    }
  }

  function nudgeBpm(delta) {
    if (!state || state.locked) return;
    pendingBpm = Math.min(BPM_MAX, Math.max(BPM_MIN, (pendingBpm ?? state.bpm) + delta));
    render();
    clearTimeout(bpmTimer);
    bpmTimer = setTimeout(async () => {
      const value = pendingBpm;
      await send({ type: 'setBpm', bpm: value });
      if (pendingBpm === value) pendingBpm = null;
      render();
    }, BPM_DEBOUNCE_MS);
  }

  // Touch and mouse: press and hold repeats. Keyboard activation arrives as a click with detail 0.
  function holdToRepeat(button, delta) {
    let delay = 0, repeat = 0;
    const stop = () => { clearTimeout(delay); clearInterval(repeat); };
    button.addEventListener('pointerdown', () => {
      nudgeBpm(delta);
      delay = setTimeout(() => { repeat = setInterval(() => nudgeBpm(delta), HOLD_REPEAT_MS); }, HOLD_DELAY_MS);
    });
    ['pointerup', 'pointerleave', 'pointercancel'].forEach(type => button.addEventListener(type, stop));
    button.addEventListener('click', e => { if (e.detail === 0) nudgeBpm(delta); });
  }
  holdToRepeat($('minus'), -1);
  holdToRepeat($('plus'), 1);

  $('go').addEventListener('click', () => { if (state) send({ type: state.status === 'recording' ? 'stop' : 'start' }); });
  $('disconnect').addEventListener('click', async () => {
    stopControl();
    await api.disconnect().catch(() => {});
    showPair();
  });

  // ---- Lost ----
  function lost(reason) {
    stopControl();
    const removed = reason === 'removed';
    $('lost-title').textContent = removed ? 'Device removed' : 'Connection lost';
    $('lost-text').textContent = removed
      ? 'This device was removed in Remote access on the PC, or Orchid was restarted. Enter a new code to connect again.'
      : 'Orchid on the PC is not answering. Check that both devices are on the same Wi-Fi and that sharing is on.';
    $('reconnect').hidden = removed;
    show('lost');
  }
  $('reconnect').addEventListener('click', startControl);
  $('new-code').addEventListener('click', async () => { await api.disconnect().catch(() => {}); showPair(); });

  document.addEventListener('visibilitychange', () => { if (!document.hidden && pollTimer) refresh(); });

  if (api.hasToken()) startControl(); else showPair();
})();
