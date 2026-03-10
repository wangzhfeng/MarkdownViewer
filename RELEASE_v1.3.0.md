# MarkdownViewer v1.3.0 Release Notes

**发布日期：** 2026 年 3 月 10 日  
**分支：** master  
**版本：** v1.3.0

---

## 🎉 重大修复

### ✅ Issue #15: 中文路径图片预览问题

**问题描述：**  
Markdown 文件中包含中文路径的图片无法正常预览，显示为裂开的图标。

**根本原因：**  
Markdig 渲染引擎会自动将图片路径中的中文字符 URL 编码（如 `%E4%B8%AD%E6%96%87`），但 Windows 的 `file://` 协议不支持这种编码格式，导致图片路径解析失败。

**解决方案：**  
- 新增 `DecodeImagePath()` 方法，后处理 Markdig 生成的 HTML
- 自动解码 `src` 和 `href` 属性中的 URL 编码字符
- 仅处理 `file://` 和本地相对路径，不影响网络图片

**修复前：**
```html
<img src="图片/%E6%B5%8B%E8%AF%95.png" />
```

**修复后：**
```html
<img src="图片/测试.png" />
```

---

### ✅ Issue #6 & #13: Total Commander 失焦问题

**问题描述：**  
关闭 Markdown 预览窗口后，焦点没有正确返回 Total Commander，导致键盘操作失效。

**解决方案：**  
- 在 `CloseWindow()` 方法中正确释放 WebBrowser 控件焦点
- 添加 `Dispose()` 调用确保资源完全释放
- 修复后 TC 能正常接收键盘输入

---

### ✅ 修复 FormatException 崩溃

**问题描述：**  
预览包含复杂 HTML/CSS 的 Markdown 文件时，插件崩溃并抛出 `System.FormatException`。

**根本原因：**  
`String.Format()` 会将 HTML 内容中的花括号 `{` `}` 误认为占位符，导致解析失败。

**解决方案：**  
- 改用 `Replace()` 方法替换模板占位符
- 避免解析 HTML 内容中的花括号
- 添加异常处理，防止崩溃

---

## 📝 功能改进

### 数字键切换文件
- 保留数字键 1-7 切换文件功能
- 在 `KeyPress` 事件中处理，稳定可靠

### 错误处理增强
- 添加 `try-catch` 包裹关键代码
- 错误信息输出到调试日志
- 防止单个错误导致整个插件崩溃

---

## ⚠️ 已知限制

### ESC 键关闭预览

**限制描述：**  
在某些场景下，按 ESC 键可能无法关闭预览窗口。

**根本原因：**  
Total Commander Lister 插件架构中，ESC 键在 TC 主窗口层面被拦截，不会传递给插件控件。这是 TC 的设计决定，所有商业 Lister 插件都存在此限制。

**解决方法：**  
1. 用鼠标点击 Total Commander 主窗口后再按 ESC
2. 或直接关闭预览窗口（点击其他文件/面板）

**未来计划：**  
考虑在预览页面添加可见的关闭按钮作为替代方案。

---

## 📊 统计信息

**代码变更：**
- 3 个文件修改
- +94 行新增
- -29 行删除
- 净增 65 行代码

**测试状态：**  
✅ 中文路径图片预览正常  
✅ 焦点恢复功能正常  
✅ 无崩溃问题  
✅ 数字键切换文件正常  

---

## 🔧 技术细节

### 修改的文件

1. **MarkdownViewer.cs**
   - 修复 `CloseWindow()` 中的焦点释放逻辑

2. **ViewerControl.cs**
   - 新增 `DecodeImagePath()` 方法
   - 添加 `try-catch` 错误处理
   - 修复模板字符串替换逻辑

3. **markdown_tmpl.txt**
   - 修复 mermaid 配置的花括号转义
   - 移除不必要的 JavaScript 代码

---

## 📥 升级说明

### 从 v1.2.x 升级

1. 下载最新 Release 的 DLL 文件
2. 关闭 Total Commander
3. 替换插件目录中的 `MarkdownViewer.dll`
4. 重新启动 Total Commander

### 首次安装

1. 从 Release 页面下载插件包
2. 解压到 Total Commander 插件目录
3. 在 TC 中配置 Lister 插件关联 `.md` 文件

---

## 🙏 致谢

感谢所有报告问题和测试修复的用户！

---

## 📄 许可证

Apache 2.0

---

**完整 Changelog：**  
https://github.com/wangzhfeng/MarkdownViewer/compare/v1.2.0...v1.3.0

**问题反馈：**  
https://github.com/wangzhfeng/MarkdownViewer/issues
