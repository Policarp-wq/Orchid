// One vertical bar per beat of the measure. A tall bar (two filled blocks) is a strong beat, a short bar (one block) is weak.
// The bar count follows the length of the array, so any meter (3, 4, 6, 8 ...) lays itself out. The component keeps only the
// current array and the disabled flag: feed it with setBeats, listen to onChange. Contract: docs/orchid-design/UI-SPEC.md, "Shared components".
//
//   const bars = OrchidUi.createBeatBars({ beats: [true, false, false, false], onChange: next => {} });
//   container.append(bars.element);  bars.setBeats(next);  bars.setDisabled(true);
(() => {
  const ui = window.OrchidUi = window.OrchidUi ?? {};

  ui.createBeatBars = ({ beats, onChange, disabled = false }) => {
    const root = document.createElement('div');
    root.className = 'beat-bars';
    root.setAttribute('role', 'group');
    root.setAttribute('aria-label', 'Strong beats');
    let current = [];
    let bars = [];
    let isDisabled = disabled;

    const block = () => Object.assign(document.createElement('button'), { type: 'button', className: 'block' });

    function build(count) {
      bars = Array.from({ length: count }, (_, index) => {
        const top = block(), bottom = block();
        const number = Object.assign(document.createElement('span'), { className: 'number', textContent: String(index + 1) });
        const column = Object.assign(document.createElement('div'), { className: 'column' });
        column.append(top, bottom, number);
        // The top block toggles the beat; the bottom block only ever sets it to weak.
        top.addEventListener('click', () => set(index, !current[index]));
        bottom.addEventListener('click', () => set(index, false));
        return { column, top, bottom };
      });
      root.replaceChildren(...bars.map(b => b.column));
    }

    function paint(index, strong) {
      const { top, bottom } = bars[index];
      top.className = 'block ' + (strong ? 'on' : 'off');
      bottom.className = 'block ' + (strong ? 'on' : 'weak');
      top.disabled = bottom.disabled = isDisabled;
      top.setAttribute('aria-label', `Beat ${index + 1}: ${strong ? 'strong' : 'weak'}, activate to make it ${strong ? 'weak' : 'strong'}`);
      bottom.setAttribute('aria-label', `Beat ${index + 1}: make weak`);
    }

    function setBeats(next) {
      current = [...next];
      if (bars.length !== current.length) build(current.length);
      current.forEach((strong, index) => paint(index, strong));
    }

    function set(index, strong) {
      if (current[index] === strong) return;
      const next = [...current];
      next[index] = strong;
      setBeats(next);
      onChange?.(next);
    }

    setBeats(beats);
    return {
      element: root,
      setBeats,
      setDisabled(value) { isDisabled = value; setBeats(current); },
    };
  };
})();
