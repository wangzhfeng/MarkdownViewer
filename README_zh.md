# MarkdownViewer

MarkdownViewer是一款Total Commander的插件，用于浏览markdown文件，支持后缀为md和markdown的文件。

![](./Doc/viewer.png)

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
