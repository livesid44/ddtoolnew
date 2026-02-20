// Intake Chatbot - Guided process intake with unique intake ID

class IntakeChatbot {
    constructor() {
        this.intakeId = null;
        this.currentStep = 0;
        this.intakeData = {};
        this.uploadedFiles = [];
        this.totalSteps = 6;

        this.questions = [
            {
                id: 'processName',
                question: "Hi! I'm your AURA intake assistant. I'll guide you through capturing all the details for a new process. Let's start — what is the <strong>name</strong> of this process?",
                type: 'text',
                placeholder: 'e.g., Customer Service Call Handling',
                label: 'Process Name'
            },
            {
                id: 'processDescription',
                question: "Great! Please provide a brief <strong>description</strong> of this process — what does it do and what is its purpose?",
                type: 'textarea',
                placeholder: 'Describe the process and its goal...',
                label: 'Process Description'
            },
            {
                id: 'processOwner',
                question: "Who is the <strong>process owner</strong>? Please provide their name and role.",
                type: 'text',
                placeholder: 'e.g., Jane Smith – Operations Manager',
                label: 'Process Owner'
            },
            {
                id: 'businessUnit',
                question: "Which <strong>business unit</strong> or department does this process belong to?",
                type: 'select',
                options: ['Customer Service', 'Operations', 'Finance', 'HR', 'IT', 'Sales', 'Marketing', 'Other'],
                label: 'Business Unit'
            },
            {
                id: 'processType',
                question: "How would you classify this process?",
                type: 'select',
                options: ['Manual', 'Semi-Automated', 'Fully Automated', 'Unknown'],
                label: 'Process Type'
            },
            {
                id: 'documents',
                question: "Almost done! Please <strong>upload any relevant documents</strong> for this process — PDFs, training videos, call recordings or transcriptions. You can skip this and upload later from the Upload Files page.",
                type: 'upload',
                label: 'Documents'
            }
        ];

        this.init();
    }

    generateIntakeId() {
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const ms = String(now.getMilliseconds()).padStart(3, '0');
        const random = Math.floor(Math.random() * 1000).toString().padStart(3, '0');
        return `INT-${year}${month}${day}-${ms}${random}`;
    }

    init() {
        this.messagesContainer = document.getElementById('chatMessages');
        this.inputArea = document.getElementById('inputArea');
        const startBtn = document.getElementById('startIntakeBtn');

        if (startBtn) {
            startBtn.addEventListener('click', () => this.startIntake());
        }

        this.loadExistingIntakes();
    }

    startIntake() {
        this.intakeId = this.generateIntakeId();
        this.currentStep = 0;
        this.intakeData = {
            id: this.intakeId,
            createdAt: new Date().toISOString(),
            status: 'in-progress'
        };

        document.getElementById('intakeIdDisplay').textContent = this.intakeId;
        document.getElementById('intakeIdBanner').style.display = 'flex';
        document.getElementById('startScreen').style.display = 'none';
        document.getElementById('chatArea').style.display = 'flex';

        this.updateProgress();
        this.askQuestion(0);
    }

    askQuestion(index) {
        if (index >= this.questions.length) {
            this.completeIntake();
            return;
        }
        const q = this.questions[index];
        this.updateProgress();
        this.addBotMessage(q.question);
        this.showInput(q);
    }

    updateProgress() {
        const step = Math.min(this.currentStep + 1, this.totalSteps);
        const pct = Math.round((this.currentStep / this.totalSteps) * 100);
        const fill = document.getElementById('chatProgress');
        const label = document.getElementById('progressLabel');
        if (fill) fill.style.width = pct + '%';
        if (label) label.textContent = `Step ${step} of ${this.totalSteps}`;
    }

    showInput(question) {
        let html = '';

        if (question.type === 'text') {
            html = `
                <input type="text" id="chatInput" placeholder="${question.placeholder}" class="chat-input-field" autocomplete="off">
                <button onclick="intakeChatbot.submitAnswer()" class="btn btn-primary chat-send-btn" aria-label="Send">
                    <i class="bi bi-send-fill"></i>
                </button>`;
        } else if (question.type === 'textarea') {
            html = `
                <textarea id="chatInput" placeholder="${question.placeholder}" class="chat-input-field chat-textarea" rows="3"></textarea>
                <button onclick="intakeChatbot.submitAnswer()" class="btn btn-primary chat-send-btn" aria-label="Send">
                    <i class="bi bi-send-fill"></i>
                </button>`;
        } else if (question.type === 'select') {
            const btns = question.options.map(opt =>
                `<button class="quick-option-btn" onclick="intakeChatbot.submitOption('${opt}')">${opt}</button>`
            ).join('');
            html = `<div class="quick-options">${btns}</div>`;
        } else if (question.type === 'upload') {
            html = `
                <div class="chat-upload-area">
                    <input type="file" id="chatFileInput" multiple hidden>
                    <div class="upload-drop-zone" id="chatDropZone">
                        <i class="bi bi-cloud-upload"></i>
                        <p>Drag &amp; drop files or <span class="upload-link">click to browse</span></p>
                        <p class="upload-hint">PDF, MP4, MP3, WAV, TXT, DOCX (max 500 MB each)</p>
                    </div>
                    <div id="chatFileList" class="chat-file-list"></div>
                </div>
                <div class="upload-actions">
                    <button onclick="intakeChatbot.submitFiles()" class="btn btn-primary">
                        <i class="bi bi-check-lg"></i> Continue
                    </button>
                    <button onclick="intakeChatbot.skipUpload()" class="btn btn-secondary">
                        Skip for now
                    </button>
                </div>`;
        }

        this.inputArea.innerHTML = html;

        if (question.type === 'text' || question.type === 'textarea') {
            const input = document.getElementById('chatInput');
            if (input) {
                input.focus();
                input.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' && !e.shiftKey) {
                        e.preventDefault();
                        this.submitAnswer();
                    }
                });
            }
        }

        if (question.type === 'upload') {
            this.setupUploadListeners();
        }
    }

    submitAnswer() {
        const input = document.getElementById('chatInput');
        if (!input || !input.value.trim()) return;

        const value = input.value.trim();
        const q = this.questions[this.currentStep];

        this.addUserMessage(value);
        this.intakeData[q.id] = value;
        this.currentStep++;
        this.inputArea.innerHTML = '';

        setTimeout(() => this.askQuestion(this.currentStep), 600);
    }

    submitOption(value) {
        const q = this.questions[this.currentStep];
        this.addUserMessage(value);
        this.intakeData[q.id] = value;
        this.currentStep++;
        this.inputArea.innerHTML = '';
        setTimeout(() => this.askQuestion(this.currentStep), 600);
    }

    setupUploadListeners() {
        const dropZone = document.getElementById('chatDropZone');
        const fileInput = document.getElementById('chatFileInput');

        if (dropZone) {
            dropZone.addEventListener('click', () => fileInput.click());

            dropZone.addEventListener('dragover', (e) => {
                e.preventDefault();
                dropZone.classList.add('drag-over');
            });

            dropZone.addEventListener('dragleave', () => dropZone.classList.remove('drag-over'));

            dropZone.addEventListener('drop', (e) => {
                e.preventDefault();
                dropZone.classList.remove('drag-over');
                this.handleFileAdd(e.dataTransfer.files);
            });
        }

        if (fileInput) {
            fileInput.addEventListener('change', () => this.handleFileAdd(fileInput.files));
        }
    }

    handleFileAdd(files) {
        Array.from(files).forEach(file => {
            const isDuplicate = this.uploadedFiles.some(
                f => f.name === file.name && f.size === file.size && f.lastModified === file.lastModified
            );
            if (!isDuplicate) {
                this.uploadedFiles.push(file);
            }
        });
        this.updateFileList();
    }

    updateFileList() {
        const list = document.getElementById('chatFileList');
        if (!list) return;
        list.innerHTML = this.uploadedFiles.map((f, i) => `
            <div class="chat-file-item">
                <span class="file-type-icon">${this.getFileIcon(f.type)}</span>
                <span class="file-name">${f.name}</span>
                <span class="file-size">${this.formatSize(f.size)}</span>
                <button onclick="intakeChatbot.removeFile(${i})" class="file-remove-btn" aria-label="Remove file">
                    <i class="bi bi-x-circle"></i>
                </button>
            </div>`).join('');
    }

    removeFile(index) {
        this.uploadedFiles.splice(index, 1);
        this.updateFileList();
    }

    getFileIcon(mimeType) {
        if (mimeType.startsWith('video/')) return '🎬';
        if (mimeType.startsWith('audio/')) return '🎵';
        if (mimeType === 'application/pdf') return '📄';
        if (mimeType.includes('word') || mimeType.includes('document')) return '📝';
        return '📁';
    }

    formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    submitFiles() {
        const count = this.uploadedFiles.length;
        let fileLabel;
        if (count === 0) {
            fileLabel = 'No files added yet — will upload later.';
        } else if (count <= 3) {
            fileLabel = `Uploaded ${count} file${count > 1 ? 's' : ''}: ${this.uploadedFiles.map(f => f.name).join(', ')}`;
        } else {
            const first3 = this.uploadedFiles.slice(0, 3).map(f => f.name).join(', ');
            fileLabel = `Uploaded ${count} files: ${first3} and ${count - 3} more`;
        }

        this.addUserMessage(fileLabel);
        this.intakeData['documents'] = this.uploadedFiles.map(f => ({
            name: f.name,
            size: f.size,
            type: f.type
        }));
        this.currentStep++;
        this.inputArea.innerHTML = '';
        setTimeout(() => this.askQuestion(this.currentStep), 600);
    }

    skipUpload() {
        this.addUserMessage('Skipping document upload — will upload later.');
        this.intakeData['documents'] = [];
        this.currentStep++;
        this.inputArea.innerHTML = '';
        setTimeout(() => this.askQuestion(this.currentStep), 600);
    }

    completeIntake() {
        this.intakeData.status = 'active';
        this.intakeData.completedAt = new Date().toISOString();

        // Persist intake
        const intakes = JSON.parse(localStorage.getItem('bpo_intakes') || '[]');
        intakes.push(this.intakeData);
        localStorage.setItem('bpo_intakes', JSON.stringify(intakes));
        localStorage.setItem('bpo_active_intake', this.intakeId);

        const docCount = (this.intakeData.documents || []).length;

        this.updateProgress();

        this.addBotMessage(`
            🎉 <strong>Intake created successfully!</strong><br><br>
            <div class="completion-summary">
                <div class="summary-id">
                    <i class="bi bi-hash"></i> <strong>${this.intakeId}</strong>
                </div>
                <ul class="summary-list">
                    <li><i class="bi bi-check-circle-fill"></i> <strong>Process:</strong> ${this.intakeData.processName}</li>
                    <li><i class="bi bi-check-circle-fill"></i> <strong>Owner:</strong> ${this.intakeData.processOwner}</li>
                    <li><i class="bi bi-check-circle-fill"></i> <strong>Business Unit:</strong> ${this.intakeData.businessUnit}</li>
                    <li><i class="bi bi-check-circle-fill"></i> <strong>Type:</strong> ${this.intakeData.processType}</li>
                    <li><i class="bi bi-check-circle-fill"></i> <strong>Documents:</strong> ${docCount} file${docCount !== 1 ? 's' : ''} uploaded</li>
                </ul>
            </div>
            All subsequent activities will be tracked under intake ID <strong>${this.intakeId}</strong>.
        `);

        this.inputArea.innerHTML = `
            <div class="completion-actions">
                <a href="workflow.html?intake=${this.intakeId}" class="btn btn-primary">
                    <i class="bi bi-diagram-3"></i> Continue to Workflow
                </a>
                <a href="upload.html?intake=${this.intakeId}" class="btn btn-secondary">
                    <i class="bi bi-cloud-upload"></i> Upload More Files
                </a>
                <button onclick="intakeChatbot.startNewIntake()" class="btn btn-secondary">
                    <i class="bi bi-plus-circle"></i> New Intake
                </button>
            </div>`;
    }

    startNewIntake() {
        this.intakeId = null;
        this.currentStep = 0;
        this.intakeData = {};
        this.uploadedFiles = [];

        document.getElementById('intakeIdBanner').style.display = 'none';
        document.getElementById('chatArea').style.display = 'none';
        document.getElementById('startScreen').style.display = 'flex';
        document.getElementById('chatMessages').innerHTML = '';
        this.inputArea.innerHTML = '';

        this.loadExistingIntakes();
    }

    addBotMessage(html) {
        const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const div = document.createElement('div');
        div.className = 'message bot-message';
        div.innerHTML = `
            <div class="message-avatar"><i class="bi bi-robot"></i></div>
            <div class="message-bubble">
                <div class="message-content">${html}</div>
                <span class="message-time">${time}</span>
            </div>`;
        this.messagesContainer.appendChild(div);
        this.scrollToBottom();
    }

    addUserMessage(text) {
        const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const div = document.createElement('div');
        div.className = 'message user-message';
        div.innerHTML = `
            <div class="message-bubble">
                <div class="message-content">${this.escapeHtml(text)}</div>
                <span class="message-time">${time}</span>
            </div>
            <div class="message-avatar"><i class="bi bi-person-circle"></i></div>`;
        this.messagesContainer.appendChild(div);
        this.scrollToBottom();
    }

    escapeHtml(str) {
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    scrollToBottom() {
        if (this.messagesContainer) {
            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }
    }

    loadExistingIntakes() {
        const listEl = document.getElementById('existingIntakesList');
        if (!listEl) return;

        const intakes = JSON.parse(localStorage.getItem('bpo_intakes') || '[]');
        if (intakes.length === 0) {
            listEl.innerHTML = '<p class="no-intakes-msg">No previous intakes found. Start your first intake above.</p>';
            return;
        }

        const recent = intakes.slice(-5).reverse();
        listEl.innerHTML = recent.map(intake => `
            <div class="intake-list-item" onclick="intakeChatbot.resumeIntake('${intake.id}')">
                <div class="intake-list-info">
                    <span class="intake-list-id">${intake.id}</span>
                    <span class="intake-list-name">${intake.processName || 'Unnamed Process'}</span>
                </div>
                <div class="intake-list-meta">
                    <span class="intake-status-badge status-${intake.status}">${intake.status}</span>
                    <span class="intake-list-date">${new Date(intake.createdAt).toLocaleDateString()}</span>
                </div>
            </div>`).join('');
    }

    resumeIntake(intakeId) {
        localStorage.setItem('bpo_active_intake', intakeId);
        window.location.href = `workflow.html?intake=${encodeURIComponent(intakeId)}`;
    }
}

// Initialise when DOM is ready
let intakeChatbot;
document.addEventListener('DOMContentLoaded', () => {
    intakeChatbot = new IntakeChatbot();
});
