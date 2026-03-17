[中文](./README_zh.md)

# MarkdownViewer

MarkdownViewer is a Total Commander plugin, using preview markdown file which suffixed with md markdown and mk.

![](./Doc/viewer.png)


# Features

## Core Features

- Preview Markdown files with syntax highlighting
- File navigation: click Markdown links to jump between files
- LaTeX math formulas support (KaTeX)
- Mermaid flowcharts and diagrams
- Code syntax highlighting (Highlight.js)

## Enhanced Features (v0.6)

### Document Outline

- Outline panel showing H1-H6 heading structure
- Click outline items to jump to headings with smooth scroll
- Auto-highlight current heading while scrolling
- Press `O` key to toggle outline view
- Press `1-6` keys to jump to first heading of corresponding level

### Themes & Layout

- Theme switching: light / dark mode
- Press `T` key to toggle theme
- Layout modes: centered narrow / full-width
- Press `M` key to toggle layout

### Keyboard Navigation

- Vim-style navigation: `j/k` (line), `d/u` (half page), `f/b` (full page)
- Quick jump: `gg` (top), `G` (bottom), `h/l` (horizontal scroll)
- Press `?` to show shortcut help panel

### Image & Media

- Click images to view in full screen
- Press `ESC` or click to close image viewer
- Image alt text shown as caption

### Reading Experience

- Reading progress bar at top of page
- Real-time scroll progress indication
- Gradient blue progress bar style

### Export & External Links

- Export to PDF: press `P` key or click PDF button
- External links open in default browser

# About Installation 

This plugin is based on the .NET platform, so the corresponding interface needs to be installed. The installation file is [TcPluginSetup.msi](./Doc/TcPluginSetup.msi) — simply double-click to run it.

Then, double-click to open `MarkdownViewer.zip` in Total Commander and follow the prompts to install the plugin.

The latest version uses WebView2. If you are using an older operating system, you will need to install the WebView2 runtime.

# About Usage

Currently, the preview window cannot be closed using the ESC key. Please click the close button on the preview window to close it.


# Version

## v0.6 (2026-03-17)

### Document Outline

- Outline panel showing H1-H6 heading structure
- Click outline items to jump to headings with smooth scroll
- Auto-highlight current heading while scrolling
- Press `O` key to toggle outline view
- Press `1-6` keys to jump to first heading of corresponding level

### Themes & Layout

- Theme switching: light / dark mode
- Press `T` key to toggle theme
- Layout modes: centered narrow / full-width
- Press `M` key to toggle layout

### Keyboard Navigation

- Vim-style navigation: `j/k/d/u/f/b/g/G/h/l`
- Press `?` to show shortcut help panel

### Export & External Links

- Export to PDF: press `P` key or click PDF button
- External links open in default browser

### Bug Fixes

- Fixed: ESC key not working reliably to close preview

## v0.5

- Issue #11: Added support for file jumping/navigation
- Upgraded Mermaid and KaTeX libraries
- Switched rendering engine to WebView2 to improve compatibility
- Fixed: Issue where the ESC key failed to close the window

## v0.4

- Fixed Issue #15: Image preview issue with Chinese file paths
- Fixed Issue #6 & #13: Total Commander losing focus issue

## v0.3

- Feature: Support print, can print to PDF through local printer
- Fixed: Cannot preview local images
- Fixed: Cannot select and copy content [\#7](https://github.com/wangzhfeng/MarkdownViewer/issues/7)
- Fixed: Dependent DLLs not packaged in output zip

## v0.2

- Fixed: Cannot close window with Esc [\#4](https://github.com/wangzhfeng/MarkdownViewer/issues/4)
- Using NuGet to manage dependencies

Thanks to [thorn0](https://github.com/thorn0) for your commit.

## v0.1

- Support previewing Markdown files
