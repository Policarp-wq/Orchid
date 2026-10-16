// App-agnostic modal on the native <dialog>: focus trap, Esc, top layer and the backdrop come from the browser.
// Nothing here knows what it hosts, so the same content can move to another screen. Contract: docs/orchid-design/UI-SPEC.md, "Shared components".
//
//   const modal = OrchidUi.openModal({ title: 'Strong beats', content: someElement, onClose: () => {} });
//   modal.close();
(() => {
  const ui = window.OrchidUi = window.OrchidUi ?? {};

  ui.openModal = ({ title, content, onClose }) => {
    const dialog = document.createElement('dialog');
    dialog.className = 'modal';

    const heading = Object.assign(document.createElement('h2'), { className: 'modal-title', textContent: title });
    const close = Object.assign(document.createElement('button'), { type: 'button', className: 'modal-close', textContent: '×' });
    close.setAttribute('aria-label', 'Close');
    close.addEventListener('click', () => dialog.close());

    const header = Object.assign(document.createElement('div'), { className: 'modal-header' });
    header.append(heading, close);
    const card = Object.assign(document.createElement('div'), { className: 'modal-card' });
    card.append(header, content);
    dialog.append(card);
    dialog.setAttribute('aria-labelledby', 'modal-title');
    heading.id = 'modal-title';

    // A click on the backdrop lands on the dialog element itself; clicks inside the card never do.
    dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });
    dialog.addEventListener('close', () => { dialog.remove(); onClose?.(); });

    document.body.append(dialog);
    dialog.showModal();
    return { element: dialog, close: () => dialog.close() };
  };
})();
