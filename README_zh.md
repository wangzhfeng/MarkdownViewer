# MarkdownViewer

MarkdownViewer是一款Total Commander的插件，用于浏览markdown文件，支持后缀为md和markdown的文件。

![](./Doc/viewer.png)

# 功能

- 支持预览markdown文件
- 支持文件跳转
- 支持`Latex`和`mermaid`流程图

# 安装说明

本插件基于.net平台，所以需要安装对应接口，安装文件为[TcPluginSetup.msi](./Doc/TcPluginSetup.msi)，直接双击运行即可。

然后在TotalCommander中双击打开`MarkdownViewer.zip`，按照提示安装插件。

最新版本使用了webview2，如果是老版本的操作系统，需要安装webview2的运行时。

# 使用说明

暂时无法使用ESC关闭预览窗口，点击预览窗口的关闭按钮来关闭窗口。

# 版本历史

## v0.1

- 支持查看markdown

## v0.2

- fixed: 不支持Esc键关闭窗口的问题 [\#4](https://github.com/wangzhfeng/MarkdownViewer/issues/4)
- 使用NuGet安装markdig库，方便本地开发

以上，多谢[thorn0](https://github.com/thorn0)提供的Commit。

## v0.3

- feature: 支持打印文件，可使用本地打印机将预览结果打印为pdf
- fixed: 无法预览本地文件
- fixed: 无法选中并复制内容 [\#7](https://github.com/wangzhfeng/MarkdownViewer/issues/7)
- fixed: 无法将依赖dll文件打包到最终结果包

## v0.4

### 修复问题

- fixed Issue #15: 中文路径图片预览问题
- fixed Issue #6 & #13: Total Commander 失焦问题

### 其他

针对v0.2中修复的Esc关闭窗口问题，发现经常性功能失灵，尝试了几种其他方案，暂时还没修复。

更新了配置文件中的相关工具地址。

## v0.5

- Issue #11: 支持文件跳转
- 升级mermaid和katex库
- 将渲染引擎修改为webview2，提升兼容性
- fixed: ESC无法关闭问题



