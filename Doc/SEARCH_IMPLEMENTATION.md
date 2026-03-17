# MarkdownViewer 原生搜索功能实现方案

## 概述

结合 Total Commander .NET 插件接口和 WebView2 的 `CoreWebView2.Find` 类，实现 MarkdownViewer 的原生搜索功能。

---

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Total Commander                          │
│  (Ctrl+F 打开搜索对话框)                                     │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ SearchText()
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   MarkdownViewer Plugin                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  MarkdownViewer.SearchText()                         │   │
│  │  - 接收搜索文本和参数                                  │   │
│  │  - 调用 ViewerControl.SearchTextInWebView2Async()    │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ ExecuteScriptAsync()
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      WebView2 Core                           │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  CoreWebView2.FindTextAsync()                        │   │
│  │  - 执行文本搜索                                        │   │
│  │  - 返回匹配结果和位置                                  │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

---

## 核心接口

### 1. TC Lister 插件接口

```csharp
public override ListerResult SearchText(
    object control, 
    string searchString, 
    SearchParameter searchParameter)
{
    ViewerControl viewerControl = (ViewerControl)control;
    
    if (!String.IsNullOrEmpty(searchString))
    {
        viewerControl.SearchTextInWebView2Async(
            searchString, 
            searchParameter);
    }
    
    return ListerResult.OK;
}
```

**SearchParameter 参数：**
- `MatchCase` - 区分大小写
- `WholeWords` - 全字匹配
- `Backward` - 向后搜索

---

### 2. WebView2 FindText API

**CoreWebView2.FindTextAsync 方法：**

```csharp
public IAsyncOperation<FindTextResult> FindTextAsync(
    string searchText,
    FindTextOptions options = null)
```

**FindTextOptions 属性：**

| 属性 | 类型 | 说明 |
|------|------|------|
| `CaseSensitive` | bool | 区分大小写 |
| `MatchWordStartsWith` | bool | 匹配单词开头 |
| `SearchDirection` | enum | 搜索方向（Forward/Backward） |
| `SearchFilter` | enum | 搜索过滤器（All/PlainTextOnly/HtmlOnly） |

**FindTextResult 属性：**

| 属性 | 类型 | 说明 |
|------|------|------|
| `SearchText` | string | 搜索的文本 |
| `TotalMatches` | uint | 总匹配数 |
| `CurrentMatchIndex` | uint | 当前匹配索引 |
| `SelectionRects` | IReadOnlyList<Rect> | 匹配项的屏幕坐标 |

---

## 实现步骤

### 步骤 1：ViewerControl 添加搜索方法

```csharp
public class ViewerControl : UserControl
{
    private CoreWebView2FindTextOptions findOptions;
    private CoreWebView2FindTextResult lastFindResult;
    private int currentMatchIndex = -1;

    public async Task SearchTextInWebView2Async(
        string searchText, 
        SearchParameter searchParameter)
    {
        if (webView2.CoreWebView2 == null) return;

        // 1. 配置搜索选项
        findOptions = new CoreWebView2FindTextOptions
        {
            CaseSensitive = searchParameter.HasFlag(SearchParameter.MatchCase),
            MatchWordStartsWith = searchParameter.HasFlag(SearchParameter.WholeWords),
            SearchDirection = searchParameter.HasFlag(SearchParameter.Backward) 
                ? CoreWebView2FindTextSearchDirection.Backward 
                : CoreWebView2FindTextSearchDirection.Forward,
            SearchFilter = CoreWebView2FindTextSearchFilter.PlainTextOnly
        };

        // 2. 执行搜索
        lastFindResult = await webView2.CoreWebView2.FindTextAsync(
            searchText, 
            findOptions);

        // 3. 高亮并滚动到匹配项
        await HighlightAndScrollToMatch(searchText);

        // 4. 记录日志
        TraceLog($"Search: '{searchText}' - Found {lastFindResult.TotalMatches} matches");
    }

    private async Task HighlightAndScrollToMatch(string searchText)
    {
        if (lastFindResult.TotalMatches == 0) return;

        // 使用 WebView2 的 SelectionRects 获取匹配位置
        var rects = lastFindResult.SelectionRects;
        
        if (rects.Count > 0)
        {
            // 滚动到第一个匹配项
            var firstRect = rects[0];
            
            string scrollScript = $@"
                window.scrollTo({{
                    top: {firstRect.Y - 100},
                    behavior: 'smooth'
                }});
            ";
            
            await webView2.CoreWebView2.ExecuteScriptAsync(scrollScript);
        }
    }
}
```

---

### 步骤 2：支持查找下一个/上一个

```csharp
public async Task FindNextAsync()
{
    if (webView2.CoreWebView2 == null || lastFindResult == null) return;

    // 继续查找下一个
    var nextResult = await webView2.CoreWebView2.FindTextAsync(
        lastFindResult.SearchText, 
        findOptions);

    lastFindResult = nextResult;
    await HighlightAndScrollToMatch(lastFindResult.SearchText);
}

public async Task FindPreviousAsync()
{
    if (webView2.CoreWebView2 == null || lastFindResult == null) return;

    // 反向查找
    findOptions.SearchDirection = CoreWebView2FindTextSearchDirection.Backward;
    var prevResult = await webView2.CoreWebView2.FindTextAsync(
        lastFindResult.SearchText, 
        findOptions);
    
    findOptions.SearchDirection = CoreWebView2FindTextSearchDirection.Forward;
    lastFindResult = prevResult;
    await HighlightAndScrollToMatch(lastFindResult.SearchText);
}
```

---

### 步骤 3：MarkdownViewer.cs 集成

```csharp
public override ListerResult SearchText(
    object control, 
    string searchString, 
    SearchParameter searchParameter)
{
    ViewerControl viewerControl = (ViewerControl)control;
    
    if (!String.IsNullOrEmpty(searchString))
    {
        // 异步执行搜索，不阻塞 TC UI
        Task.Run(async () => 
        {
            await viewerControl.SearchTextInWebView2Async(
                searchString, 
                searchParameter);
        });
    }
    
    return ListerResult.OK;
}

public override ListerResult SendCommand(
    object control, 
    ListerCommand command, 
    ShowFlags parameter)
{
    ViewerControl viewerControl = (ViewerControl)control;
    
    switch (command)
    {
        case ListerCommand.Copy:
            viewerControl.ExecuteScriptAsync("document.execCommand('copy')");
            break;
        case ListerCommand.SelectAll:
            viewerControl.ExecuteScriptAsync("document.execCommand('selectAll')");
            break;
        case ListerCommand.FindNext:  // 查找下一个
            viewerControl.FindNextAsync();
            break;
        case ListerCommand.FindPrev:  // 查找上一个
            viewerControl.FindPreviousAsync();
            break;
    }
    
    return ListerResult.OK;
}
```

---

## 用户体验流程

```
1. 用户在 TC 中按 Ctrl+F
   ↓
2. TC 显示原生搜索对话框
   ↓
3. 用户输入搜索内容，选择选项（区分大小写/全字匹配）
   ↓
4. 用户点击"确定"或按 Enter
   ↓
5. TC 调用 MarkdownViewer.SearchText()
   ↓
6. MarkdownViewer 调用 WebView2.FindTextAsync()
   ↓
7. WebView2 执行搜索，返回匹配结果
   ↓
8. MarkdownViewer 高亮匹配项并滚动到第一个结果
   ↓
9. 用户按 F3 查找下一个
   ↓
10. TC 调用 MarkdownViewer.SendCommand(FindNext)
   ↓
11. WebView2 继续查找下一个匹配项
```

---

## 优势

| 特性 | 原生 JS 实现 | WebView2 Find API |
|------|-------------|-------------------|
| **性能** | 需要遍历 DOM，慢 | 浏览器原生优化，快 |
| **准确性** | 可能受 HTML 结构影响 | 准确识别可见文本 |
| **高亮** | 需要手动修改 DOM | 浏览器自动处理 |
| **滚动** | 需要手动计算位置 | 提供精确坐标 |
| **维护成本** | 高 | 低 |
| **支持方向** | 需自行实现 | 原生支持前后向 |

---

## 注意事项

1. **WebView2 版本要求**
   - FindTextAsync API 需要 WebView2 SDK 1.0.1150+
   - 确保用户安装了兼容的 WebView2 Runtime

2. **异步处理**
   - 所有 WebView2 API 都是异步的
   - 避免阻塞 TC 主线程

3. **内存管理**
   - 及时释放 FindTextResult 对象
   - 避免内存泄漏

4. **搜索状态保持**
   - 需要保存上次搜索条件
   - 支持 F3 重复搜索

---

## 代码文件清单

需要修改的文件：

```
MarkdownViewer/
├── MarkdownViewer.cs          // 添加 SearchText 和 SendCommand 实现
├── ViewerControl.cs           // 添加 SearchTextInWebView2Async 等方法
├── markdown_css.txt           // 添加搜索高亮样式（可选）
└── MarkdownViewer.csproj      // 更新 WebView2 SDK 版本
```

---

## 测试场景

1. **基本搜索**
   - 搜索普通文本
   - 搜索中文内容
   - 搜索特殊字符

2. **选项测试**
   - 区分大小写
   - 全字匹配
   - 向后搜索

3. **边界情况**
   - 空搜索文本
   - 未找到匹配项
   - 大量匹配项

4. **性能测试**
   - 大文件搜索（10MB+）
   - 快速重复搜索

---

## 参考资料

- [TC Lister Plugin Interface](https://github.com/X-Storm/TcPluginInterface)
- [WebView2 FindTextAsync API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2_findtextasync)
- [CoreWebView2FindTextOptions Class](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2findtextoptions)

---

*文档版本：1.0*  
*创建日期：2026-03-10*  
*作者：十一*
