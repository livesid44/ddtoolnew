/**
 * kanban-api.js – Wires the Kanban page to the REST API.
 *
 * What it does:
 *  1. Loads the Kanban board for the current process from
 *     GET /api/v1/processes/{id}/kanban.
 *  2. Renders columns dynamically from API response.
 *  3. Drag-and-drop card moves call PATCH /api/v1/kanban/cards/{id}/move.
 *  4. "+ Add Task" modal creates cards via POST /api/v1/processes/{id}/kanban/cards.
 *  5. Delete icon on each card calls DELETE /api/v1/kanban/cards/{id}.
 *
 * Depends on: app-config.js, auth.js, api-client.js, notifications.js
 */

(function () {
  'use strict';

  let _processId   = null;
  let _dragCardId  = null;

  /* ── Resolve process ID ───────────────────────────────────────────────── */

  async function resolveProcessId() {
    const stored = localStorage.getItem('bpo_current_process_id') ||
                   window.BpoConfig.defaultProcessId;
    if (stored) { _processId = stored; return; }

    // Create demo process so the kanban page works standalone
    try {
      const proc = await window.BpoApi.processes.create({
        name:        'Demo Process',
        description: 'Auto-created for kanban demo.',
        department:  'General',
        owner:       'dev@localhost',
      });
      _processId = proc.id;
      localStorage.setItem('bpo_current_process_id', _processId);
    } catch (err) {
      console.error('[kanban-api] Could not resolve process ID:', err);
    }
  }

  /* ── Priority badge ──────────────────────────────────────────────────── */

  function priorityClass(p) {
    return { Critical: 'high', High: 'high', Medium: 'medium', Low: 'low' }[p] || 'low';
  }

  /* ── Render a single card ────────────────────────────────────────────── */

  function renderCard(card) {
    const initials = (card.assignedTo || '??')
      .split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
    return `
      <div class="kanban-card" draggable="true"
           data-card-id="${card.id}" data-column="${card.column}"
           ondragstart="bpoKanbanDragStart(event)"
           ondragend="bpoKanbanDragEnd(event)">
        <div class="card-header">
          <span class="card-priority ${priorityClass(card.priority)}">${card.priority}</span>
          <div style="display:flex;gap:4px">
            <button class="btn btn-sm" style="padding:2px 6px;font-size:.75rem"
              onclick="bpoEditCard('${card.id}')">✏️</button>
            <button class="btn btn-sm" style="padding:2px 6px;font-size:.75rem"
              onclick="bpoDeleteCard('${card.id}', this)">🗑️</button>
          </div>
        </div>
        <h4>${card.title}</h4>
        ${card.description ? `<p>${card.description}</p>` : ''}
        <div class="card-footer">
          <div class="card-assignee">
            <span class="avatar">${initials}</span>
            <span>${card.assignedTo || 'Unassigned'}</span>
          </div>
        </div>
      </div>`;
  }

  /* ── Render the full board ───────────────────────────────────────────── */

  function renderBoard(board) {
    const kanbanBoard = document.querySelector('.kanban-board');
    if (!kanbanBoard) return;

    kanbanBoard.innerHTML = Object.entries(board.columns).map(([col, cards]) => {
      const colIcons = { ToDo: '📝', InProgress: '🔄', Review: '👀', Done: '✅' };
      const colLabel = { ToDo: 'To Do', InProgress: 'In Progress', Review: 'Review', Done: 'Completed' };
      return `
        <div class="kanban-column" data-column="${col}"
             ondragover="bpoKanbanDragOver(event)"
             ondrop="bpoKanbanDrop(event)">
          <div class="column-header">
            <h3>${colIcons[col] || '📌'} ${colLabel[col] || col}</h3>
            <span class="task-count">${cards.length}</span>
          </div>
          <div class="column-body">
            ${cards.map(renderCard).join('')}
          </div>
        </div>`;
    }).join('');

    updateBoardStats(board);
  }

  /* ── Board statistics ────────────────────────────────────────────────── */

  function updateBoardStats(board) {
    const allCards   = Object.values(board.columns).flat();
    const total      = allCards.length;
    const done       = board.columns.Done?.length || 0;
    const highCount  = allCards.filter(c => c.priority === 'High' || c.priority === 'Critical').length;
    const pct        = total ? Math.round(done / total * 100) : 0;

    const setText = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    setText('stat-total-tasks',    total);
    setText('stat-completion-rate', `${pct}%`);
    setText('stat-high-priority',  `${highCount} task${highCount !== 1 ? 's' : ''}`);
  }

  /* ── Drag & Drop ─────────────────────────────────────────────────────── */

  window.bpoKanbanDragStart = function (e) {
    _dragCardId = e.currentTarget.dataset.cardId;
    e.currentTarget.style.opacity = '0.5';
    e.dataTransfer.effectAllowed = 'move';
  };

  window.bpoKanbanDragEnd = function (e) {
    e.currentTarget.style.opacity = '';
    _dragCardId = null;
  };

  window.bpoKanbanDragOver = function (e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    e.currentTarget.classList.add('drag-over');
  };

  window.bpoKanbanDrop = async function (e) {
    e.preventDefault();
    const column = e.currentTarget.dataset.column;
    e.currentTarget.classList.remove('drag-over');

    if (!_dragCardId || !column) return;
    const cardId = _dragCardId;

    // Optimistic UI: move card DOM immediately
    const cardEl = document.querySelector(`[data-card-id="${cardId}"]`);
    const targetBody = e.currentTarget.querySelector('.column-body');
    if (cardEl && targetBody) {
      cardEl.dataset.column = column;
      targetBody.appendChild(cardEl);
    }

    // Persist via API
    try {
      await window.BpoApi.kanban.moveCard(cardId, column, 0);
    } catch (err) {
      window.BpoNotifications?.showToast('Move Failed', err.message, 'error');
      await loadBoard(); // revert
    }
  };

  /* ── Delete card ─────────────────────────────────────────────────────── */

  window.bpoDeleteCard = async function (cardId, btn) {
    if (!confirm('Delete this card?')) return;
    btn.disabled = true;
    try {
      await window.BpoApi.kanban.deleteCard(cardId);
      document.querySelector(`[data-card-id="${cardId}"]`)?.remove();
      window.BpoNotifications?.showToast('Deleted', 'Card removed.', 'info');
    } catch (err) {
      window.BpoNotifications?.showToast('Error', err.message, 'error');
      btn.disabled = false;
    }
  };

  /* ── Edit card (simple prompt fallback; replace with modal in prod) ──── */

  window.bpoEditCard = async function (cardId) {
    const newTitle = prompt('Edit card title:');
    if (!newTitle?.trim()) return;
    try {
      await window.BpoApi.kanban.updateCard(cardId, {
        title: newTitle.trim(), description: '', priority: 'Medium', assignedTo: '',
      });
      await loadBoard();
    } catch (err) {
      window.BpoNotifications?.showToast('Error', err.message, 'error');
    }
  };

  /* ── Add Task modal / inline ─────────────────────────────────────────── */

  function wireAddTaskButton() {
    const addBtn = document.querySelector('[data-action="add-task"]') ||
                   [...document.querySelectorAll('.btn')].find(b => b.textContent.includes('Add Task'));
    if (!addBtn) return;

    addBtn.addEventListener('click', () => {
      const modal = document.getElementById('addCardModal');
      if (modal) { modal.style.display = 'flex'; return; }

      // Inline prompt fallback
      const title = prompt('Card title:');
      if (!title?.trim()) return;
      createCard({ title: title.trim(), description: '', column: 'ToDo', priority: 'Medium', assignedTo: '' });
    });
  }

  function wireAddCardModal() {
    const form = document.getElementById('addCardForm');
    if (!form) return;
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const data = Object.fromEntries(new FormData(form));
      await createCard(data);
      document.getElementById('addCardModal').style.display = 'none';
      form.reset();
    });
    document.getElementById('cancelAddCard')?.addEventListener('click', () => {
      document.getElementById('addCardModal').style.display = 'none';
    });
  }

  async function createCard(data) {
    if (!_processId) return;
    try {
      await window.BpoApi.kanban.createCard(_processId, {
        title:       data.title,
        description: data.description || '',
        column:      data.column || 'ToDo',
        priority:    data.priority || 'Medium',
        assignedTo:  data.assignedTo || '',
      });
      window.BpoNotifications?.showToast('Card Added', data.title, 'success');
      await loadBoard();
    } catch (err) {
      window.BpoNotifications?.showToast('Error', err.message, 'error');
    }
  }

  /* ── Load board ──────────────────────────────────────────────────────── */

  async function loadBoard() {
    if (!_processId) return;
    try {
      const board = await window.BpoApi.kanban.getBoard(_processId);
      renderBoard(board);
    } catch (err) {
      console.error('[kanban-api] Failed to load board:', err);
      window.BpoNotifications?.showToast('Error', 'Could not load Kanban board.', 'error');
    }
  }

  /* ── Bootstrap ───────────────────────────────────────────────────────── */

  async function init() {
    await resolveProcessId();
    await loadBoard();
    wireAddTaskButton();
    wireAddCardModal();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
