// Stand-in for the PC-side "Remote access" panel (the real one lives in the MAUI window).
// Lets you drive the whole flow from one browser tab: approve or deny, revoke, drop the network, unplug MIDI.
(() => {
  const pc = window.MockPc;
  const style = document.createElement('style');
  style.textContent = `
    .mockpc { position: fixed; right: 12px; bottom: 12px; width: min(340px, calc(100vw - 24px)); max-height: 80dvh; overflow: auto;
      background: var(--surface); color: var(--text); border: 1px solid var(--border); border-radius: 10px; font-size: 13px; z-index: 10; }
    .mockpc summary { padding: 10px 14px; font-weight: 600; cursor: pointer; }
    .mockpc .row { padding: 10px 14px; border-top: 1px solid var(--border); display: grid; gap: 6px; }
    .mockpc .label { color: var(--muted); font-size: 11px; letter-spacing: .08em; text-transform: uppercase; }
    .mockpc .code { font: 600 20px ui-monospace, "Cascadia Mono", Consolas, monospace; letter-spacing: .1em; }
    .mockpc .line { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .mockpc .line .grow { flex: 1; }
    .mockpc button { min-height: 32px; padding: 0 12px; border-radius: 6px; border: 1px solid var(--border); background: var(--subtle); }
    .mockpc button.primary { background: var(--accent); color: var(--on-accent); border-color: transparent; }
    .mockpc .pending { background: var(--warn-bg); color: var(--warn); }
    .mockpc .pending .label { color: inherit; }
    .mockpc .warn { color: var(--warn); }
  `;
  document.head.append(style);

  const panel = document.createElement('details');
  panel.className = 'mockpc';
  panel.open = matchMedia('(min-width: 900px)').matches;
  document.body.append(panel);
  document.body.style.paddingBottom = '56px'; // keep the collapsed panel off the page's own bottom button

  const el = (tag, props = {}, ...children) => {
    const node = Object.assign(document.createElement(tag), props);
    node.append(...children);
    return node;
  };
  const row = (cls, ...children) => el('div', { className: `row ${cls}`.trim() }, ...children);
  const label = text => el('div', { className: 'label', textContent: text });
  const line = (...children) => el('div', { className: 'line' }, ...children);
  const button = (text, onclick, primary = false) => el('button', { type: 'button', textContent: text, onclick, className: primary ? 'primary' : '' });
  const toggle = (text, get, set) => {
    const box = el('input', { type: 'checkbox', checked: get(), onchange: () => { set(box.checked); pc.changed(); } });
    return el('label', { className: 'line' }, box, text);
  };
  const mmss = ms => { const s = Math.max(0, Math.ceil(ms / 1000)); return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };

  function render() {
    const now = Date.now();
    const rows = [el('summary', { textContent: 'Mock PC · Remote access' })];

    rows.push(row('', label('Simulate'),
      toggle('Share on LAN', () => pc.sharing, v => { pc.sharing = v; }),
      toggle('Network reachable', () => !pc.offline, v => { pc.offline = !v; }),
      toggle('MIDI input connected', () => pc.midiAvailable, v => pc.setMidiAvailable(v))));

    const codeRow = row('', label('Pairing code'));
    if (now < pc.lockedUntil) {
      codeRow.append(el('div', { className: 'warn', textContent: `Locked after too many wrong codes · ${mmss(pc.lockedUntil - now)}` }));
    } else if (pc.code && now < pc.code.expiresAt) {
      codeRow.append(el('div', { className: 'code', textContent: `${pc.code.value.slice(0, 4)} ${pc.code.value.slice(4)}` }),
        el('div', { textContent: `expires in ${mmss(pc.code.expiresAt - now)} · one use` }));
    } else {
      codeRow.append(el('div', { textContent: 'No active code' }));
    }
    codeRow.append(line(button('New code', () => pc.newCode())));
    rows.push(codeRow);

    const p = pc.activePending();
    if (p) {
      rows.push(row('pending', label('Request'),
        el('div', {}, el('b', { textContent: p.device }), ` · ${p.ip}`),
        el('div', {}, 'Verification word: ', el('b', { textContent: p.verification })),
        line(button('Approve', () => pc.approve(), true), button('Deny', () => pc.deny()))));
    }

    const devices = row('', label('Devices'));
    if (!pc.devices.length) devices.append(el('div', { textContent: 'None' }));
    pc.devices.forEach(d => devices.append(line(
      el('span', { className: 'grow' }, el('b', { textContent: d.name }), ` · ${mmss(now - d.connectedAt)} · ${d.lastCommand ?? 'no commands yet'}`),
      button('Revoke', () => pc.revoke(d.id)))));
    rows.push(devices);

    panel.replaceChildren(...rows);
  }

  pc.listeners.add(render);
  // Countdowns tick once a second, but not under the pointer: replacing a button between mousedown and mouseup drops the click.
  setInterval(() => { if (!panel.matches(':hover')) render(); }, 1000);
  render();
})();
