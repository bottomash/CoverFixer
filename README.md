# CoverFixer

面向 Emby Server 4.9.5.0 的原生计划任务插件，用于补全电影、剧集、季和单集缺失的主封面。

## 行为

- 电影、剧集和季只处理缺少 `Primary` 图片的条目，不覆盖已有封面。
- 优先选择英文图片（`en` 及其地区变体）。
- 没有英文图片时回退简体中文（`zh-CN`、`zh-Hans`、`cmn-Hans`，并兼容 Emby/TMDB 返回的 `zh`）。
- 不选择繁体中文、其他语言或未标注语言的图片。
- 同语言内优先分辨率更高的图片，再比较社区评分和票数。
- 单集使用独立策略：接受 TMDB 常见的无语言横版剧照，优先 `TheMovieDb`，并过滤竖版剧集海报。
- 单集没有自己的 `Primary` 时会写入剧照，从而替换界面继承显示的剧集封面；已有竖版 `Primary` 也会被替换，已有横版剧照保持不变。
- 图片写入磁盘后立即刷新 Emby 条目的图片记录，避免文件已经保存但界面仍回退显示父级封面。
- 对旧版本已经下载到元数据目录、但尚未登记的图片，任务会优先直接登记，不重复下载。
- 串行处理，每个缺图条目间隔 250 毫秒，单条失败不会中断任务。
- 默认每天 04:00 运行，也可以在 Emby 控制台的“计划任务”中手动运行或修改计划。

## 构建

要求：PowerShell 7、.NET 8 SDK，以及本机 Emby Server 4.9.5.0 程序集。

```powershell
./build.ps1
```

如果 Emby 安装在其他目录：

```powershell
./build.ps1 -EmbyServerPath 'D:/emby/system'
```

构建产物位于 `dist/CoverFixer.dll`。

## 安装

1. 停止 Emby Server。
2. 删除插件目录中的旧版 `Emby.CoverBackfill.dll`，不要让新旧程序集同时存在。
3. 将 `dist/CoverFixer.dll` 复制到 Emby 的 `programdata/plugins/` 目录。
4. 启动 Emby Server。
5. 打开 Emby 控制台 → 计划任务 → 媒体库 → 补全缺失封面。
6. 首次先手动运行并检查日志与结果，再保留或调整默认计划。

升级 Emby 后应重新使用新版本服务器程序集构建并验证插件。卸载时停止 Emby，删除该 DLL，再启动 Emby。
