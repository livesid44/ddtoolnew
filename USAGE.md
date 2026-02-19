# Usage Guide - HTML Wireframe Base

## Quick Start

1. **Open the wireframe**
   ```bash
   # Simply open index.html in your browser
   open index.html
   # or
   python3 -m http.server 8000
   # then visit http://localhost:8000
   ```

2. **Customize the content**
   - Edit `index.html` to change text and structure
   - Modify `styles.css` to adjust colors and layout
   - Extend `script.js` to add new interactivity

## Customization Examples

### Change Colors

To change from grayscale to your brand colors, edit `styles.css`:

```css
/* Find and replace these color values */
#333 → Your primary text color
#666 → Your secondary text color
#ddd → Your border color
#f5f5f5 → Your background color
```

### Add a New Section

1. Add HTML in `index.html`:
```html
<section class="my-section">
    <div class="container">
        <h2>My New Section</h2>
        <p>Content goes here</p>
    </div>
</section>
```

2. Add styles in `styles.css`:
```css
.my-section {
    padding: 3rem 0;
    background-color: #f9f9f9;
}
```

### Modify the Grid Layout

The content area uses CSS Grid. To change columns:

```css
/* In styles.css, find .content-grid */
.content-grid {
    display: grid;
    grid-template-columns: 2fr 1fr; /* Wider content area */
    /* or */
    grid-template-columns: 1fr; /* Full width, no sidebar */
}
```

### Add New JavaScript Features

Example: Add a scroll-to-top button

1. Add HTML:
```html
<button id="scrollTop" class="scroll-top-btn">↑</button>
```

2. Add CSS:
```css
.scroll-top-btn {
    position: fixed;
    bottom: 2rem;
    right: 2rem;
    display: none;
}
```

3. Add JavaScript to `script.js`:
```javascript
const scrollTopBtn = document.getElementById('scrollTop');
window.addEventListener('scroll', () => {
    if (window.scrollY > 300) {
        scrollTopBtn.style.display = 'block';
    } else {
        scrollTopBtn.style.display = 'none';
    }
});
scrollTopBtn.addEventListener('click', () => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
});
```

## Responsive Breakpoints

Modify breakpoints in `styles.css`:

```css
/* Tablet and below */
@media (max-width: 768px) {
    /* Your tablet styles */
}

/* Mobile */
@media (max-width: 480px) {
    /* Your mobile styles */
}
```

## Component Reference

### Header
- Fixed/sticky positioning
- Logo placeholder
- Responsive navigation

### Hero Section
- Large call-to-action area
- Centered content
- Dashed border for wireframe style

### Feature Boxes
- Responsive grid (auto-fit)
- Equal-height columns
- Centered content

### Sidebar
- Stacked widgets
- Responsive (moves above content on mobile)
- List-based navigation

### Footer
- 3-column grid
- Auto-responsive
- Bottom copyright area

### Modal
- Overlay backdrop
- Centered content
- Multiple close methods
- Smooth animations

## Tips

1. **Keep it simple**: This is a wireframe - focus on structure over style
2. **Test responsive**: Always check on different screen sizes
3. **Use semantic HTML**: Maintain proper heading hierarchy
4. **Accessibility**: Keep ARIA labels and keyboard support
5. **Comments**: Add comments to your custom code for maintainability

## Common Tasks

### Remove the Sidebar
1. In `index.html`, delete the `<aside class="sidebar">` section
2. In `styles.css`, change `.content-grid` to single column:
   ```css
   .content-grid {
       grid-template-columns: 1fr;
   }
   ```

### Add More Navigation Links
1. In `index.html`, add to the `<ul class="nav-list">`:
   ```html
   <li><a href="#newpage">New Page</a></li>
   ```

### Change Layout Width
In `styles.css`, modify `.container`:
```css
.container {
    max-width: 1400px; /* Default is 1200px */
}
```

## Best Practices

- Keep the wireframe style (grayscale) until you're ready to add brand colors
- Test all interactive features after making changes
- Use the browser's developer tools to debug
- Keep backup copies before major changes
- Follow the existing code style and structure

## Need Help?

- Check the README.md for feature documentation
- Review the code comments in each file
- Use browser DevTools to inspect elements
- Test in multiple browsers for compatibility

Happy wireframing! 🎨
