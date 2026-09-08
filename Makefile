# ============================================================================
#  MarkdownViewer 构建脚本（GNU Make）
#
#  本地构建入口，命令对齐 .github/workflows/build.yml 的 CI 流程：
#
#    CI 步骤                            本地 make 目标
#    ---------------------------------  ------------------------------
#    Download zip.exe + Add to PATH  ->  zip-setup（复制到 .build-tools 并前置 PATH）
#    nuget restore MarkdownViewer.sln->  restore
#    msbuild ... Configuration=Debug  ->  debug
#    msbuild ... Configuration=Release->  release
#
#  说明：
#    - 本脚本在 Git Bash（Windows）下运行，需要 GNU make。
#    - PostBuildEvent 会调用 zip 打包，故构建前把项目自带的
#      TcPluginCore/zip.exe 复制到 .build-tools 并前置到 PATH。
#    - MSBuild 开关用 "-p:" / "-t:" 而非 "/p:" / "/t:"，避免 Git Bash 的
#      MSYS 路径转换把开头的 "/" 吞掉（报 MSB1008）。
#    - MSBuild / NuGet 会自动探测，也可用环境变量覆盖（见 help）。
# ============================================================================

SHELL := /bin/bash

# ---------- 项目路径 ----------
SLN     := MarkdownViewer.sln
PROJ    := MarkdownViewer/MarkdownViewer.csproj
BIN_DIR := MarkdownViewer/bin
OBJ_DIR := MarkdownViewer/obj

# ---------- 默认配置 ----------
CONFIG ?= Debug

# ---------- 工具 ----------
ZIP_SRC   := TcPluginCore/zip.exe
TOOLS_DIR := .build-tools
ZIP_EXE   := $(TOOLS_DIR)/zip.exe

# ---------- MSBuild 探测 ----------
# 优先级：环境变量 MSBUILD > PATH 中的 msbuild > vswhere 定位 VS > .NET Framework 自带
MSBUILD ?= $(shell \
	if command -v msbuild >/dev/null 2>&1; then echo "msbuild"; \
	elif [ -x "/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" ]; then \
		win=$$("/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' 2>/dev/null | tr -d '\r' | head -1); \
		[ -z "$$win" ] || { echo "$$win" | cygpath -u -f - 2>/dev/null || echo "$$win" | sed 's|\\|/|g; s|^\([A-Za-z]\):/*|/\L\1/|'; }; \
	elif [ -x "/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe" ]; then \
		echo "/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe"; \
	fi)

# ---------- NuGet 探测 ----------
# 优先级：环境变量 NUGET > PATH 中的 nuget > .build-tools/nuget.exe
NUGET ?= $(shell \
	if command -v nuget >/dev/null 2>&1; then echo "nuget"; \
	elif [ -x "$(TOOLS_DIR)/nuget.exe" ]; then echo "$(TOOLS_DIR)/nuget.exe"; \
	fi)

.DEFAULT_GOAL := help

.PHONY: help debug release all restore clean zip-setup _build

# ---------- 目标 ----------

help: ## 显示帮助
	@echo "MarkdownViewer 构建脚本（GNU Make）"
	@echo ""
	@echo "用法："
	@echo "  make           显示本帮助（默认）"
	@echo "  make debug     构建 Debug 版本"
	@echo "  make release   构建 Release 版本"
	@echo "  make all       依次构建 Debug + Release"
	@echo "  make restore   仅还原 NuGet 包"
	@echo "  make clean     清理构建产物"
	@echo ""
	@echo "环境变量覆盖："
	@echo "  MSBUILD=/path/to/MSBuild.exe   指定 MSBuild 路径"
	@echo "  NUGET=/path/to/nuget.exe       指定 nuget 路径"
	@echo "  CONFIG=Debug|Release           指定配置（默认 Debug）"

zip-setup: ## 准备 zip.exe（复制到 .build-tools）
	@mkdir -p $(TOOLS_DIR)
	@cp -f "$(ZIP_SRC)" "$(ZIP_EXE)"
	@echo ">> zip.exe 已就绪: $(ZIP_EXE)"

restore: ## 还原 NuGet 包
	@if [ -n "$(NUGET)" ]; then \
		echo ">> nuget restore $(SLN)"; \
		"$(NUGET)" restore $(SLN); \
	else \
		echo ">> 未找到 nuget.exe，改用 MSBuild 还原包"; \
		"$(MSBUILD)" $(SLN) -t:Restore "-p:Configuration=$(CONFIG)"; \
	fi

debug: restore ## 构建 Debug 版本
	@$(MAKE) _build CONFIG=Debug

release: restore ## 构建 Release 版本
	@$(MAKE) _build CONFIG=Release

all: restore ## 依次构建 Debug + Release
	@$(MAKE) _build CONFIG=Debug
	@$(MAKE) _build CONFIG=Release

_build: zip-setup
	@test -n "$(MSBUILD)" || { echo "错误：未找到 MSBuild.exe，请安装 VS 2017+ 或设置环境变量 MSBUILD"; exit 1; }
	@echo ">> MSBuild: $(MSBUILD)"
	@echo ">> 配置: $(CONFIG)  |  平台: Any CPU"
	@TOOLS_ABS=$$(cd "$(TOOLS_DIR)" && pwd); \
	export PATH="$$TOOLS_ABS:$$PATH"; \
	"$(MSBUILD)" $(SLN) "-p:Configuration=$(CONFIG)" "-p:Platform=Any CPU"
	@echo ">> 构建完成，产物: $(BIN_DIR)/$(CONFIG)/MarkdownViewer.zip"

clean: ## 清理构建产物
	@rm -rf $(BIN_DIR) $(OBJ_DIR) $(TOOLS_DIR)
	@echo ">> 已清理 bin / obj / .build-tools"
