// Mobile Menu Toggle
const menuToggle = document.getElementById('menuToggle');
const navList = document.getElementById('navList');

if (menuToggle && navList) {
    menuToggle.addEventListener('click', function() {
        navList.classList.toggle('active');
        
        // Animate hamburger menu
        this.classList.toggle('active');
    });

    // Close menu when clicking outside
    document.addEventListener('click', function(event) {
        if (!menuToggle.contains(event.target) && !navList.contains(event.target)) {
            navList.classList.remove('active');
            menuToggle.classList.remove('active');
        }
    });

    // Close menu when clicking on a link
    const navLinks = navList.querySelectorAll('a');
    navLinks.forEach(link => {
        link.addEventListener('click', function() {
            navList.classList.remove('active');
            menuToggle.classList.remove('active');
        });
    });
}

// Modal Functionality
const modal = document.getElementById('modal');
const openModalBtn = document.getElementById('openModal');
const closeModalBtn = document.getElementById('closeModal');
const closeModalBtnSecondary = document.getElementById('closeModalBtn');

// Open modal
if (openModalBtn && modal) {
    openModalBtn.addEventListener('click', function() {
        modal.classList.add('active');
        document.body.style.overflow = 'hidden'; // Prevent scrolling when modal is open
    });
}

// Close modal function
function closeModal() {
    if (modal) {
        modal.classList.remove('active');
        document.body.style.overflow = ''; // Re-enable scrolling
    }
}

// Close modal with close button
if (closeModalBtn) {
    closeModalBtn.addEventListener('click', closeModal);
}

// Close modal with secondary button
if (closeModalBtnSecondary) {
    closeModalBtnSecondary.addEventListener('click', closeModal);
}

// Close modal when clicking outside the modal content
if (modal) {
    modal.addEventListener('click', function(event) {
        if (event.target === modal) {
            closeModal();
        }
    });
}

// Close modal with Escape key
document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        closeModal();
    }
});

// Smooth scrolling for anchor links
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
        const href = this.getAttribute('href');
        // Only handle links with valid targets (not just '#')
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

// Add active state to current section in navigation
window.addEventListener('scroll', function() {
    const sections = document.querySelectorAll('section[id]');
    const navLinks = document.querySelectorAll('.nav-list a');
    
    let current = '';
    
    sections.forEach(section => {
        const sectionTop = section.offsetTop;
        const sectionHeight = section.clientHeight;
        if (window.scrollY >= (sectionTop - 100)) {
            current = section.getAttribute('id');
        }
    });
    
    navLinks.forEach(link => {
        link.classList.remove('active');
        if (link.getAttribute('href') === '#' + current) {
            link.classList.add('active');
        }
    });
});

// Console log to confirm script is loaded
console.log('HTML Wireframe Base - JavaScript loaded successfully');
