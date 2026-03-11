[中文](./README_zh.md)

# MarkdownViewer

MarkdownViewer is a Total Commander plugin, using preview markdown file which suffixed with md markdown and mk.

![](./Doc/viewer.png)

# About Installation 

This plugin is based on the .NET platform, so the corresponding interface needs to be installed. The installation file is [TcPluginSetup.msi](./Doc/TcPluginSetup.msi) — simply double-click to run it.

Then, double-click to open `MarkdownViewer.zip` in Total Commander and follow the prompts to install the plugin.

# About Usage

Currently, the preview window cannot be closed using the ESC key. Please click the close button on the preview window to close it.


# Version

## v0.1

- support preview markdown file

## v0.2

- fixed: cannot close window with Esc [\#4](https://github.com/wangzhfeng/MarkdownViewer/issues/4)
- using Nuget to manage dependency

Thanks to [thorn0](https://github.com/thorn0) for your commit.

## v0.3

- feature: support print, can print to pdf through local printer
- fixed: cannot preview local image
- fixed: cannot select and copy [\#7](https://github.com/wangzhfeng/MarkdownViewer/issues/7)
- fixed: dependented dll not package in output zip

## v0.4

- Fixed Issue #15: Image preview issue with Chinese file paths
- Fixed Issue #6 & #13: Total Commander losing focus issue

Regarding the fixed Esc key closing window issue in v0.2, it was found that the function frequently malfunctions. Several other solutions have been attempted, but the issue has not been resolved yet.
