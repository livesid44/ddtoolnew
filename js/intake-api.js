/**
 * intake-api.js
 * Intake Module – wires intake.html to the API.
 *
 * State machine:
 *   Step 1 (Draft)     → chat to collect meta → submit meta
 *   Step 2 (Submitted) → upload artifacts
 *   Step 3 (Submitted) → run AI analysis → Analysed
 *   Step 4 (Analysed)  → promote → Promoted (creates Process)
 */

// ── Module state ─────────────────────────────────────────────────────────────

let intakeState = {
    intakeId: null,      // current IntakeRequest GUID
    currentStep: 1,
    metaFields: {},      // collected fields from chat
    isComplete: false,   // chat isComplete flag
    uploadedFiles: [],   // { file, artifactId }
    analysisResult: null
};

// ── Initialise ────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    // Set username
    const userEl = document.getElementById('userName');
    const user = window.BpoAuth?.getUser?.() ?? null;
    if (userEl) userEl.textContent = user?.name ?? 'You';

    // Sidebar toggle
    const toggle = document.getElementById('menuToggle');
    const sidebar = document.getElementById('sidebar');
    if (toggle && sidebar) toggle.onclick = () => sidebar.classList.toggle('collapsed');

    // Chat input: send on Enter
    const chatInput = document.getElementById('chatInput');
    if (chatInput) chatInput.addEventListener('keydown', e => { if (e.key === 'Enter') intakeSendChat(); });

    // Drag-and-drop on upload zone
    const dz = document.getElementById('dropZone');
    if (dz) {
        dz.addEventListener('dragover',  e => { e.preventDefault(); dz.classList.add('over'); });
        dz.addEventListener('dragleave', () => dz.classList.remove('over'));
        dz.addEventListener('drop',      e => { e.preventDefault(); dz.classList.remove('over'); intakeFilePicked(e.dataTransfer.files); });
    }

    // Start a new intake automatically
    intakeStart();
});

// ── Step 1: Chat ──────────────────────────────────────────────────────────────

async function intakeStart() {
    intakeAddBubble('assistant', '👋 Starting your intake…', true);
    try {
        const body = await api.post('/intake/start', { queuePriority: 'Medium' });
        intakeState.intakeId = body.id;
        // Clear loading bubble and show first assistant message
        const chatWin = document.getElementById('chatWindow');
        chatWin.innerHTML = '';
        // Extract first assistant message from chatHistoryJson
        const history = JSON.parse(body.chatHistoryJson || '[]');
        history.forEach(m => intakeAddBubble(m.role, m.content));
    } catch (e) {
        intakeAddBubble('assistant', '⚠️ Could not start intake. Please refresh and try again.');
        console.error('intakeStart failed', e);
    }
}

async function intakeSendChat() {
    const input = document.getElementById('chatInput');
    const msg = input.value.trim();
    if (!msg || !intakeState.intakeId) return;
    input.value = '';

    intakeAddBubble('user', msg);
    intakeAddBubble('assistant', '…', false, 'typingBubble');

    try {
        const resp = await api.post(`/intake/${intakeState.intakeId}/chat`, { message: msg });
        document.getElementById('typingBubble')?.remove();
        intakeAddBubble('assistant', resp.assistantMessage);

        // Update collected meta fields
        if (resp.currentFields) {
            intakeState.metaFields = resp.currentFields;
            intakeState.isComplete = resp.isComplete;
            intakeRenderMetaPreview();
        }

        if (resp.isComplete) {
            document.getElementById('metaPreviewCard').style.display = 'block';
            document.getElementById('chatInput').disabled = true;
            document.getElementById('chatSend').disabled = true;
        }
    } catch (e) {
        document.getElementById('typingBubble')?.remove();
        intakeAddBubble('assistant', '⚠️ Something went wrong. Please try again.');
        console.error('intakeSendChat failed', e);
    }
}

function intakeAddBubble(role, content, isTyping = false, id = null) {
    const win = document.getElementById('chatWindow');
    if (!win) return;
    const div = document.createElement('div');
    div.className = `chat-bubble ${role}${isTyping ? ' typing' : ''}`;
    if (id) div.id = id;
    // Simple markdown-like: **bold**
    div.innerHTML = content.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>').replace(/\n/g, '<br>');
    win.appendChild(div);
    win.scrollTop = win.scrollHeight;
}

function intakeRenderMetaPreview() {
    const f = intakeState.metaFields;
    const container = document.getElementById('metaFields');
    if (!container) return;
    const entries = [
        ['Process Name', f.title],
        ['Department', f.department],
        ['Location', f.location],
        ['Business Unit', f.businessUnit],
        ['Contact Email', f.contactEmail],
        ['Priority', f.queuePriority],
    ];
    container.innerHTML = entries
        .filter(([, v]) => v)
        .map(([k, v]) => `
            <div style="background:#f8fafc;border-radius:8px;padding:10px 14px;">
                <div style="font-size:11px;color:#9ca3af;font-weight:600;text-transform:uppercase;margin-bottom:2px;">${k}</div>
                <div style="font-size:14px;color:#1e293b;font-weight:500;">${v}</div>
            </div>`)
        .join('');
}

function intakeEditMeta() {
    document.getElementById('metaPreviewCard').style.display = 'none';
    document.getElementById('chatInput').disabled = false;
    document.getElementById('chatSend').disabled = false;
    intakeState.isComplete = false;
}

async function intakeSubmitMeta() {
    const f = intakeState.metaFields;
    if (!f.title || !f.department) {
        showToast('Please complete process name and department first.', 'warning');
        return;
    }
    try {
        await api.post(`/intake/${intakeState.intakeId}/submit`, {
            title: f.title,
            description: f.description ?? null,
            department: f.department,
            location: f.location ?? null,
            businessUnit: f.businessUnit ?? null,
            contactEmail: f.contactEmail ?? null,
            queuePriority: f.queuePriority ?? 'Medium'
        });
        intakeGoToStep(2);
    } catch (e) {
        showToast('Failed to submit meta. Please try again.', 'error');
        console.error('intakeSubmitMeta failed', e);
    }
}

// ── Step 2: Upload ────────────────────────────────────────────────────────────

function intakeFilePicked(files) {
    if (!files?.length) return;
    Array.from(files).forEach(file => intakeUploadFile(file));
}

async function intakeUploadFile(file) {
    const listEl = document.getElementById('uploadedFilesList');
    const itemId = `file_${Date.now()}_${Math.random().toString(36).slice(2)}`;
    const icon = fileIcon(file.name);

    // Add row with progress indicator
    const row = document.createElement('div');
    row.className = 'uploaded-file';
    row.id = itemId;
    row.innerHTML = `
        <span class="file-icon">${icon}</span>
        <span class="file-name">${escHtml(file.name)}</span>
        <span class="file-size">${formatBytes(file.size)}</span>
        <span style="color:#94a3b8;font-size:12px;" id="${itemId}_status">Uploading…</span>`;
    listEl.appendChild(row);

    try {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('artifactType', inferArtifactType(file.name));

        const token = await window.BpoAuth?.getToken?.() ?? null;
        const headers = {};
        if (token) headers['Authorization'] = `Bearer ${token}`;

        const resp = await fetch(`${AppConfig.apiBaseUrl}/intake/${intakeState.intakeId}/artifacts`, {
            method: 'POST',
            headers,
            body: formData
        });

        if (!resp.ok) throw new Error(`Upload failed: ${resp.status}`);
        const artifact = await resp.json();

        document.getElementById(`${itemId}_status`).textContent = '✅ Uploaded';
        intakeState.uploadedFiles.push({ file, artifactId: artifact.id });
    } catch (e) {
        document.getElementById(`${itemId}_status`).textContent = '❌ Failed';
        console.error('intakeUploadFile failed', e);
    }
}

// ── Step 3: Analysis ──────────────────────────────────────────────────────────

async function intakeRunAnalysis() {
    document.getElementById('analysisLoadingCard').style.display = 'block';
    document.getElementById('analysisResultsPanel').style.display = 'none';
    document.getElementById('btnRunAnalysis').disabled = true;

    // Animate progress bar
    const bar = document.getElementById('analysisProgress');
    if (bar) { setTimeout(() => { bar.style.width = '80%'; }, 100); }

    try {
        const result = await api.post(`/intake/${intakeState.intakeId}/analyse`, {});
        intakeState.analysisResult = result;

        if (bar) bar.style.width = '100%';
        setTimeout(() => {
            document.getElementById('analysisLoadingCard').style.display = 'none';
            renderAnalysisResult(result);
            document.getElementById('analysisResultsPanel').style.display = 'block';
            document.getElementById('btnRunAnalysis').textContent = '🔄 Re-analyse';
            document.getElementById('btnRunAnalysis').disabled = false;

            // Auto-advance to Step 4 after showing results
            setTimeout(() => intakeGoToStep(4), 600);
        }, 500);
    } catch (e) {
        document.getElementById('analysisLoadingCard').style.display = 'none';
        document.getElementById('btnRunAnalysis').disabled = false;
        showToast('AI analysis failed. Please try again.', 'error');
        console.error('intakeRunAnalysis failed', e);
    }
}

function renderAnalysisResult(result) {
    const brief = document.getElementById('analysisBrief');
    if (brief) brief.textContent = result.brief;

    const cps = document.getElementById('checkpointsList');
    if (cps) {
        cps.innerHTML = (result.checkpoints || []).map(cp =>
            `<div class="analysis-item"><span class="icon">✅</span>${escHtml(cp)}</div>`).join('');
    }

    const acts = document.getElementById('actionablesList');
    if (acts) {
        acts.innerHTML = (result.actionables || []).map(a =>
            `<div class="analysis-item"><span class="icon">🎯</span>${escHtml(a)}</div>`).join('');
    }
}

// ── Step 4: Promote ───────────────────────────────────────────────────────────

async function intakePromote() {
    try {
        const process = await api.post(`/intake/${intakeState.intakeId}/promote`, {});
        document.getElementById('promoteBanner').style.display = 'none';
        document.getElementById('step4BackRow').style.display = 'none';
        document.getElementById('promoteSuccess').style.display = 'block';
        document.getElementById('promoteSuccessMsg').textContent =
            `Process "${process.name}" has been created in ${process.department}. ` +
            'You can now track it in the Workflow and Kanban views.';
        showToast(`Project "${process.name}" created successfully!`, 'success');
    } catch (e) {
        showToast('Failed to create project. Please try again.', 'error');
        console.error('intakePromote failed', e);
    }
}

function intakeStartNew() {
    intakeState = { intakeId: null, currentStep: 1, metaFields: {}, isComplete: false, uploadedFiles: [], analysisResult: null };
    document.getElementById('chatWindow').innerHTML = '';
    document.getElementById('uploadedFilesList').innerHTML = '';
    document.getElementById('metaPreviewCard').style.display = 'none';
    document.getElementById('promoteSuccess').style.display = 'none';
    document.getElementById('promoteBanner').style.display = 'block';
    document.getElementById('step4BackRow').style.display = 'flex';
    document.getElementById('chatInput').disabled = false;
    document.getElementById('chatSend').disabled = false;
    intakeGoToStep(1);
    intakeStart();
}

// ── Stepper navigation ────────────────────────────────────────────────────────

function intakeGoToStep(step) {
    intakeState.currentStep = step;
    [1, 2, 3, 4].forEach(n => {
        const panel = document.getElementById(`panelStep${n}`);
        if (panel) panel.style.display = n === step ? 'block' : 'none';

        const circle = document.getElementById(`step${n}Circle`);
        const label = document.getElementById(`step${n}Label`);
        if (circle && label) {
            if (n < step) {
                circle.className = 'step-circle done';
                circle.textContent = '✓';
                label.className = 'step-label done';
            } else if (n === step) {
                circle.className = 'step-circle active';
                circle.textContent = String(n);
                label.className = 'step-label active';
            } else {
                circle.className = 'step-circle';
                circle.textContent = String(n);
                label.className = 'step-label';
            }
        }
    });

    // Update connectors
    [1, 2, 3].forEach(n => {
        const conn = document.getElementById(`conn${n}`);
        if (conn) conn.className = `step-connector${n < step ? ' done' : ''}`;
    });

    // Auto-trigger analysis when entering step 3
    if (step === 3 && intakeState.analysisResult === null) {
        setTimeout(() => intakeRunAnalysis(), 300);
    }
}

// ── Utility ───────────────────────────────────────────────────────────────────

function showToast(msg, type = 'info') {
    if (window.BpoNotifications?.show) { window.BpoNotifications.show(msg, type); return; }
    console.log(`[${type}] ${msg}`);
}

function inferArtifactType(filename) {
    const ext = filename.split('.').pop()?.toLowerCase();
    if (['pdf'].includes(ext)) return 'Pdf';
    if (['mp4', 'avi', 'mov', 'mkv', 'webm'].includes(ext)) return 'Video';
    if (['mp3', 'wav', 'm4a', 'ogg', 'flac'].includes(ext)) return 'Audio';
    if (['txt', 'vtt', 'srt'].includes(ext)) return 'Transcription';
    if (['xlsx', 'xls', 'csv'].includes(ext)) return 'Spreadsheet';
    return 'Other';
}

function fileIcon(name) {
    const ext = name.split('.').pop()?.toLowerCase();
    if (ext === 'pdf') return '📄';
    if (['mp4', 'avi', 'mov'].includes(ext)) return '🎬';
    if (['mp3', 'wav', 'm4a'].includes(ext)) return '🎤';
    if (['xlsx', 'xls', 'csv'].includes(ext)) return '📊';
    if (ext === 'docx') return '📝';
    return '📎';
}

function formatBytes(bytes) {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function escHtml(str) {
    return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
