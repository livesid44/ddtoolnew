// BPO Platform JavaScript

// -------------------------------------------------------
// Active Intake Banner – injected on every platform page
// -------------------------------------------------------
function injectActiveIntakeBanner() {
    // Don't inject on login or intake pages (intake.html manages its own banner)
    const isLoginPage = document.querySelector('.login-page');
    const isIntakePage = document.querySelector('.intake-page');
    if (isLoginPage || isIntakePage) return;

    const activeIntakeId = localStorage.getItem('bpo_active_intake');
    if (!activeIntakeId) return;

    // Look up intake details
    const intakes = JSON.parse(localStorage.getItem('bpo_intakes') || '[]');
    const intake = intakes.find(i => i.id === activeIntakeId);
    const processName = intake ? intake.processName : '';

    // Build banner HTML
    const banner = document.createElement('div');
    banner.className = 'active-intake-banner';
    banner.id = 'globalIntakeBanner';
    banner.innerHTML = `
        <i class="bi bi-hash"></i>
        <span>Active Intake:</span>
        <strong>${activeIntakeId}</strong>
        ${processName ? `<span style="opacity:0.8;font-size:0.85rem;">— ${processName}</span>` : ''}
        <span class="intake-status-pill">${intake ? intake.status : 'active'}</span>
        <a class="banner-switch-link" href="intake.html" title="Start a new intake">+ New Intake</a>
    `;

    // Insert after the platform header
    const header = document.querySelector('.platform-header');
    if (header && header.nextSibling) {
        header.parentNode.insertBefore(banner, header.nextSibling);
    } else if (header) {
        header.parentNode.appendChild(banner);
    }
}

// Inject "New Intake" link into every platform sidebar (if not already present)
function injectIntakeSidebarLink() {
    const isLoginPage = document.querySelector('.login-page');
    const isIntakePage = document.querySelector('.intake-page');
    if (isLoginPage || isIntakePage) return;

    const sidebarNav = document.querySelector('.sidebar-nav');
    if (!sidebarNav) return;

    // Only add if the link doesn't already exist
    if (sidebarNav.querySelector('a[href="intake.html"]')) return;

    const link = document.createElement('a');
    link.href = 'intake.html';
    link.className = 'sidebar-link intake-link';
    link.innerHTML = `
        <span class="icon"><i class="bi bi-chat-dots"></i></span>
        <span>New Intake</span>
    `;
    sidebarNav.insertBefore(link, sidebarNav.firstChild);
}

// Also store intake ID from URL param for inter-page tracking
function trackIntakeFromUrl() {
    const params = new URLSearchParams(window.location.search);
    const intakeParam = params.get('intake');
    if (!intakeParam) return;

    // Validate that the intake ID matches expected format and exists in stored intakes
    const validFormat = /^INT-\d{8}-\d{6}$/.test(intakeParam);
    if (!validFormat) return;

    const intakes = JSON.parse(localStorage.getItem('bpo_intakes') || '[]');
    const exists = intakes.some(i => i.id === intakeParam);
    if (exists) {
        localStorage.setItem('bpo_active_intake', intakeParam);
    }
}

// Login Form Handling
document.addEventListener('DOMContentLoaded', function() {
    trackIntakeFromUrl();
    injectActiveIntakeBanner();
    injectIntakeSidebarLink();
    // Login form submission
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', function(e) {
            e.preventDefault();
            const username = document.getElementById('username').value;
            const password = document.getElementById('password').value;
            
            // Simulate login (in production, this would make an API call)
            if (username && password) {
                console.log('Login attempt:', username);
                // Redirect to dashboard
                window.location.href = 'dashboard.html';
            } else {
                alert('Please enter both username and password');
            }
        });
    }

    // Tab switching functionality
    const tabBtns = document.querySelectorAll('.tab-btn');
    const tabContents = document.querySelectorAll('.tab-content');
    
    tabBtns.forEach(btn => {
        btn.addEventListener('click', function() {
            const tabName = this.getAttribute('data-tab');
            
            // Remove active class from all tabs
            tabBtns.forEach(b => b.classList.remove('active'));
            tabContents.forEach(c => c.classList.remove('active'));
            
            // Add active class to clicked tab
            this.classList.add('active');
            const activeTab = document.getElementById(tabName + 'Tab');
            if (activeTab) {
                activeTab.classList.add('active');
            }
        });
    });

    // Add User Form Toggle
    const addUserBtn = document.getElementById('addUserBtn');
    const addUserForm = document.getElementById('addUserForm');
    const cancelAddUser = document.getElementById('cancelAddUser');
    
    if (addUserBtn && addUserForm) {
        addUserBtn.addEventListener('click', function() {
            addUserForm.style.display = 'block';
        });
    }
    
    if (cancelAddUser && addUserForm) {
        cancelAddUser.addEventListener('click', function() {
            addUserForm.style.display = 'none';
        });
    }

    // File Upload Zone
    const uploadZone = document.getElementById('uploadZone');
    const fileInput = document.getElementById('fileInput');
    const browseBtn = document.getElementById('browseBtn');
    
    if (uploadZone && fileInput) {
        // Click to browse
        uploadZone.addEventListener('click', function() {
            fileInput.click();
        });
        
        if (browseBtn) {
            browseBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                fileInput.click();
            });
        }
        
        // Drag and drop functionality
        uploadZone.addEventListener('dragover', function(e) {
            e.preventDefault();
            this.style.borderColor = '#667eea';
            this.style.background = '#f8f9ff';
        });
        
        uploadZone.addEventListener('dragleave', function() {
            this.style.borderColor = '#ddd';
            this.style.background = '#fff';
        });
        
        uploadZone.addEventListener('drop', function(e) {
            e.preventDefault();
            this.style.borderColor = '#ddd';
            this.style.background = '#fff';
            
            const files = e.dataTransfer.files;
            handleFiles(files);
        });
        
        // File input change
        fileInput.addEventListener('change', function() {
            handleFiles(this.files);
        });
    }

    // Category tabs functionality
    const categoryTabs = document.querySelectorAll('.category-tab');
    categoryTabs.forEach(tab => {
        tab.addEventListener('click', function() {
            categoryTabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');
            
            const category = this.getAttribute('data-category');
            console.log('Filter by category:', category);
            // In production, this would filter the file list
        });
    });

    // Chatbot minimize functionality
    const minimizeChatbot = document.getElementById('minimizeChatbot');
    const chatbotMessages = document.getElementById('chatbotMessages');
    
    if (minimizeChatbot && chatbotMessages) {
        minimizeChatbot.addEventListener('click', function() {
            if (chatbotMessages.style.display === 'none') {
                chatbotMessages.style.display = 'block';
                this.textContent = '−';
            } else {
                chatbotMessages.style.display = 'none';
                this.textContent = '+';
            }
        });
    }

    // Kanban drag and drop
    const kanbanCards = document.querySelectorAll('.kanban-card');
    const kanbanColumns = document.querySelectorAll('.column-body');
    
    kanbanCards.forEach(card => {
        card.addEventListener('dragstart', function(e) {
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/html', this.innerHTML);
            this.classList.add('dragging');
        });
        
        card.addEventListener('dragend', function() {
            this.classList.remove('dragging');
        });
    });
    
    kanbanColumns.forEach(column => {
        column.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            this.style.background = '#e3f2fd';
        });
        
        column.addEventListener('dragleave', function() {
            this.style.background = '';
        });
        
        column.addEventListener('drop', function(e) {
            e.preventDefault();
            this.style.background = '';
            
            const draggedCard = document.querySelector('.dragging');
            if (draggedCard) {
                this.appendChild(draggedCard);
                console.log('Card moved to:', this.parentElement.getAttribute('data-status'));
                
                // Update task count
                updateTaskCounts();
            }
        });
    });

    // Form validation for feedback
    const feedbackForm = document.querySelector('.feedback-form');
    if (feedbackForm) {
        feedbackForm.addEventListener('submit', function(e) {
            e.preventDefault();
            
            const formData = new FormData(this);
            console.log('Feedback submitted:', Object.fromEntries(formData));
            
            // Show success message
            alert('Feedback submitted successfully!');
            this.reset();
        });
    }

    // Configuration forms
    const configForms = document.querySelectorAll('.config-form');
    configForms.forEach(form => {
        form.addEventListener('submit', function(e) {
            e.preventDefault();
            console.log('Configuration saved');
            alert('Configuration saved successfully!');
        });
    });
});

// Handle file uploads
function handleFiles(files) {
    console.log('Files selected:', files.length);
    
    // In production, this would upload files to Azure Blob Storage
    Array.from(files).forEach(file => {
        console.log('File:', file.name, file.size, file.type);
        
        // Simulate upload progress
        simulateUpload(file);
    });
}

// Simulate file upload progress
function simulateUpload(file) {
    let progress = 0;
    const interval = setInterval(() => {
        progress += 10;
        console.log(`Uploading ${file.name}: ${progress}%`);
        
        if (progress >= 100) {
            clearInterval(interval);
            console.log(`Upload complete: ${file.name}`);
        }
    }, 200);
}

// Update task counts in Kanban board
function updateTaskCounts() {
    const columns = document.querySelectorAll('.kanban-column');
    columns.forEach(column => {
        const cards = column.querySelectorAll('.kanban-card');
        const countSpan = column.querySelector('.task-count');
        if (countSpan) {
            countSpan.textContent = cards.length;
        }
    });
}

// Chart filter functionality
const chartFilters = document.querySelectorAll('.chart-filter');
chartFilters.forEach(filter => {
    filter.addEventListener('change', function() {
        console.log('Chart filter changed to:', this.value);
        // In production, this would update the chart data
    });
});

// Notification handling
document.addEventListener('click', function(e) {
    if (e.target.closest('.notification-item')) {
        const notification = e.target.closest('.notification-item');
        notification.classList.remove('new');
        console.log('Notification clicked');
    }
});

// Add smooth scroll for in-page navigation
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
        const href = this.getAttribute('href');
        if (href && href !== '#') {
            const target = document.querySelector(href);
            if (target) {
                e.preventDefault();
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        }
    });
});

// Console log to confirm script loaded
console.log('BPO Platform JavaScript loaded successfully');
