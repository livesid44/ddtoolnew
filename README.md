# ddtoolnew - HTML Wireframe Base

A fully functional HTML wireframe base template with modern features and responsive design.

## Features

- ✅ **Semantic HTML5** structure
- ✅ **Responsive Design** - Mobile, tablet, and desktop layouts
- ✅ **CSS Grid & Flexbox** for modern layouts
- ✅ **Interactive Navigation** with mobile menu toggle
- ✅ **Modal Dialog** functionality
- ✅ **Smooth Scrolling** for anchor links
- ✅ **Accessibility** features (ARIA labels, semantic markup)
- ✅ **Clean Wireframe Styling** - grayscale design for prototyping
- ✅ **Well-commented Code** for easy customization

## File Structure

```
ddtoolnew/
├── index.html       # Main HTML structure
├── styles.css       # Complete styling and responsive design
├── script.js        # JavaScript functionality
└── README.md        # Documentation
```

## Components Included

### 1. Header
- Logo placeholder
- Responsive navigation menu
- Mobile hamburger menu toggle

### 2. Hero Section
- Large heading area
- Call-to-action button
- Modal trigger example

### 3. Main Content Area
- Article content section
- Feature boxes (3-column grid)
- Multiple content sections with headings

### 4. Sidebar
- Widget containers
- List-based navigation
- Stacked layout

### 5. Footer
- 3-column grid layout
- Contact information
- Links and copyright

### 6. Modal Dialog
- Overlay with backdrop
- Close button and escape key support
- Click-outside to close
- Smooth animations

## JavaScript Features

- **Mobile Menu Toggle**: Responsive hamburger menu for mobile devices
- **Modal Management**: Open/close modal with multiple methods
- **Smooth Scrolling**: Anchor links scroll smoothly to sections
- **Active Navigation**: Highlights current section in navigation
- **Accessibility**: Keyboard support (Escape key to close modal)
- **Click Outside**: Closes menu/modal when clicking outside

## How to Use

1. **Open the wireframe**: Simply open `index.html` in a web browser
2. **Customize**: Edit the HTML content to match your needs
3. **Style**: Modify `styles.css` to change colors, spacing, and layout
4. **Add Features**: Extend `script.js` with additional functionality

## Responsive Breakpoints

- **Desktop**: > 768px (default layout)
- **Tablet**: ≤ 768px (stacked sidebar)
- **Mobile**: ≤ 480px (optimized spacing)

## Browser Compatibility

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Customization Tips

### Changing Colors
The wireframe uses a grayscale palette. To add colors:
1. Open `styles.css`
2. Search for color values (e.g., `#333`, `#666`, `#ddd`)
3. Replace with your brand colors

### Adding Sections
1. Copy an existing section structure from `index.html`
2. Paste it in the desired location
3. Update the content and IDs
4. Add corresponding styles in `styles.css` if needed

### Modifying Layout
- The main content uses CSS Grid: `.content-grid`
- Feature boxes use responsive grid: `.feature-boxes`
- Footer uses auto-fit columns: `.footer-content`

## .NET + Azure Platform Development

This repository also contains a full-stack BPO AI Platform built with .NET 8 and Azure services.
Open `BPOPlatform.sln` (at the repository root) in Visual Studio 2022 or Rider to load all projects.

### Solution Projects

| Project | Type | Purpose |
|---------|------|---------|
| **`src/BPOPlatform.Web`** | ASP.NET Core (static files) | **Web startup** – serves the HTML/CSS/JS frontend locally |
| **`src/BPOPlatform.Api`** | ASP.NET Core Web API | **API startup** – REST API + SignalR hub (Swagger at `/swagger`) |
| `src/BPOPlatform.Functions` | Azure Functions v4 | Background blob-trigger AI processing |
| `src/BPOPlatform.Application` | Class Library | CQRS handlers, validators, DTOs |
| `src/BPOPlatform.Infrastructure` | Class Library | EF Core, Azure Blob, Azure OpenAI |
| `src/BPOPlatform.Domain` | Class Library | Entities, domain events, interfaces |
| `src/BPOPlatform.UnitTests` | xUnit | 59 unit tests |
| `src/BPOPlatform.IntegrationTests` | xUnit | 20 integration tests |

### Running Locally

#### Visual Studio 2022

1. Open `BPOPlatform.sln`
2. Right-click the solution → **Properties** → **Startup Project**
3. Select **Multiple startup projects** and set:
   - `BPOPlatform.Web` → **Start**
   - `BPOPlatform.Api` → **Start**
   - All other projects → *None*
4. Press **F5**

| URL | What opens |
|-----|-----------|
| `http://localhost:5500` | HTML frontend (index, dashboard, kanban …) |
| `http://localhost:5232/swagger` | REST API + Swagger UI |

#### VS Code

A compound launch configuration is included. Open the Run panel and select:

> **🚀 Full Platform (Web + API)**

This starts both projects simultaneously and opens the browser.

#### Command Line

```bash
# Terminal 1 – API
cd src/BPOPlatform.Api
dotnet run

# Terminal 2 – Web frontend
cd src/BPOPlatform.Web
dotnet run
```

### Running Tests

```bash
dotnet test BPOPlatform.sln
```

79 tests (59 unit + 20 integration) should all pass.

---

## Development (Static HTML)

The frontend is pure HTML/CSS/JavaScript — no build process required.

Edit any `.html`, `.css`, or `.js` file and refresh the browser to see changes instantly.
The `BPOPlatform.Web` project (above) serves these files when running the full .NET platform,
but you can also open `index.html` directly in a browser for quick static prototyping.

## License

Free to use for any purpose.

## Credits

Created as a base template for HTML wireframing and prototyping.