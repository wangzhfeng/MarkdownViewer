# CI/CD 配置说明

## GitHub Actions 工作流

### 触发条件

- **Push 到 master/main 分支** - 自动构建 Debug 和 Release 版本
- **创建 v* 标签** - 自动构建并发布 Release
- **Pull Request** - 自动构建验证

### 构建流程

```yaml
1. Checkout code          # 检出代码
2. Setup MSBuild          # 配置 MSBuild
3. Setup NuGet            # 配置 NuGet
4. Restore packages       # 恢复 NuGet 包
5. Build Debug            # 构建 Debug 版本
6. Build Release          # 构建 Release 版本
7. Upload artifacts       # 上传构建产物
8. Create Release         # 创建 GitHub Release（仅 tag 触发）
9. Update Pages           # 更新 GitHub Pages（仅 master 触发）
```

### 构建产物

**Debug 版本（保留 7 天）：**
- `MarkdownViewer.dll`
- `markdown_tmpl.txt`
- `markdown_css.txt`

**Release 版本（保留 30 天，tag 触发）：**
- `MarkdownViewer.dll`
- `markdown_tmpl.txt`
- `markdown_css.txt`
- `Pek.Markdig.HighlightJs.dll`
- `Markdig.dll`
- `Jurassic.dll`

**Release ZIP（tag 触发）：**
- 包含所有必需文件
- 自动附加到 GitHub Release

---

## 使用方式

### 1. 日常开发

```bash
# 推送到 master 分支，自动构建
git push origin master

# 查看构建状态
https://github.com/wangzhfeng/MarkdownViewer/actions
```

### 2. 发布新版本

```bash
# 创建并推送标签
git tag v1.3.0
git push origin v1.3.0

# GitHub Actions 会自动：
# 1. 构建 Release 版本
# 2. 创建 GitHub Release
# 3. 上传 ZIP 包
# 4. 附加 Release Notes
```

### 3. 下载构建产物

**从 Actions 下载：**
1. 进入 https://github.com/wangzhfeng/MarkdownViewer/actions
2. 点击对应的工作流
3. 在页面底部找到 "Artifacts"
4. 点击下载

**从 Release 下载：**
1. 进入 https://github.com/wangzhfeng/MarkdownViewer/releases
2. 选择对应版本
3. 下载 `MarkdownViewer.zip`

---

## 环境要求

- **操作系统：** Windows-latest (GitHub Actions)
- **.NET Framework：** 4.5.2
- **MSBuild：** Visual Studio 2017+
- **NuGet：** 最新稳定版

---

## 本地构建

如果需要本地构建测试：

```bash
# Windows (需要 Visual Studio)
msbuild MarkdownViewer.sln /p:Configuration=Release

# 或使用 Visual Studio
# 打开 MarkdownViewer.sln
# 选择 Release 配置
# 生成 → 生成解决方案
```

---

## 故障排查

### 构建失败

1. **检查 NuGet 包**
   ```bash
   nuget restore MarkdownViewer.sln
   ```

2. **检查 .NET Framework 版本**
   - 确保安装了 .NET Framework 4.5.2+

3. **检查依赖项**
   - TcPluginInterface.dll (在 TcPluginCore 目录)
   - Pek.Markdig.HighlightJs.dll (在 Lib 目录)

### Release 创建失败

1. **检查标签格式**
   - 必须是 `v*` 格式（如 `v1.3.0`）

2. **检查 RELEASE_*.md 文件**
   - 确保存在对应的 Release Notes 文件

3. **检查 GITHUB_TOKEN 权限**
   - 确保有写入 Release 的权限

---

## 自动化优势

✅ **无需手动构建** - AI 助手可以直接推送代码，自动构建  
✅ **版本管理清晰** - 每次 Release 都有对应的标签和产物  
✅ **测试验证** - 每次提交都自动构建，及早发现问题  
✅ **文档同步** - GitHub Pages 自动更新  

---

## 后续优化

- [ ] 添加单元测试
- [ ] 添加代码质量检查（CodeQL）
- [ ] 添加自动版本号生成
- [ ] 添加 Changelog 自动生成
