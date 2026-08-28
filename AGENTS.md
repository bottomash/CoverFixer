# AGENTS.md

<INSTRUCTIONS>
- 始终使用中文回复。
- Windows 环境使用 PowerShell 7，路径使用 `/`；不使用 Docker 或 Docker 相关流程。
- 编辑代码使用 Visual Studio Code；不要使用 Notepad。
- 修改代码前先确认目标行为并检查相关代码；如果存在会明显改变结果的多种方案，先向用户确认。
- 修改代码后先执行适合当前改动的检查；确认无误后，在用户已授权的任务范围内提交并推送到当前远程分支。
- 不要提交与任务无关的文件、配置密钥或本地备份文件。
- CoverFixer DLL 不在本地环境构建；使用 `.github/workflows/build.yml` 在 GitHub Actions 中构建，并将生成的 `CoverFixer.dll` 提交到项目根目录。
</INSTRUCTIONS>
