# CoverFixer

面向 Emby Server 4.9.5.0 的原生计划任务插件，用于补全电影、剧集、季和单集缺失的主封面。

## 行为

- 电影、剧集和季只处理缺少 `Primary` 图片的条目，不覆盖已有封面。
- 只查询执行时刻向前推一个自然月以内入库的媒体，以 Emby `DateCreated` 为准；更早入库的条目不处理。
- 使用 TMDB API 查询电影或剧集的 `original_language`；季和单集沿用所属剧集的原始语言。
- 选图优先级为：原始语言、英文、简体中文、未标注语言、任意可用语言。
- 简体中文兼容 `zh-CN`、`zh-Hans`、`cmn-Hans` 以及 Emby/TMDB 返回的 `zh`。
- 同语言内优先分辨率更高的图片，再比较社区评分和票数。
- 单集使用独立策略：接受 TMDB 常见的无语言横版剧照，优先 `TheMovieDb`，并过滤竖版剧集海报。
- 单集没有自己的 `Primary` 时会写入剧照，从而替换界面继承显示的剧集封面；已有竖版 `Primary` 也会被替换，已有横版剧照保持不变。
- 单集已有横版剧照时（如 Emby 自动截取的视频帧），如果远程存在更大分辨率的横版剧照会替换，自动截图会被 TheMovieDb 官方剧照取代；相同或更小分辨率的图不会替换，手动设置的图片不受影响。
- 图片写入磁盘后立即刷新 Emby 条目的图片记录，避免文件已经保存但界面仍回退显示父级封面。
- 对旧版本已经下载到元数据目录、但尚未登记的图片，任务会优先直接登记，不重复下载。
- 串行处理，每个缺图条目间隔 250 毫秒，单条失败不会中断任务。
- 默认每天 04:00 运行，也可以在 Emby 控制台的“计划任务”中手动运行或修改计划。
- CoverFixer 配置页用于填写 TMDB API Read Access Token。
- 配置页使用 Emby 4.9 的 `emby-scroller` 和独立 AMD 控制器，兼容插件列表弹层布局。
- 管理员打开剧集或具体单集详情页时，“三点”菜单会隐藏“随机播放”“添加到合集”“添加到播放列表”“收藏”“标记已播放”“下载到”“转换”和原“删除”命令，删除按钮改为与“三点”菜单平级的独立按钮。
- 单个剧集详情页的“三点”菜单还会提供“刷新 TMDB 封面”命令。
- 菜单命令自动使用当前 Series Item ID，只使用 TheMovieDb 候选图替换当前主封面，不锁定元数据。
- 封面刷新成功后会触发 Emby 原生的条目更新通知，使当前详情页立即重新获取封面，无需再标记已看或手动刷新页面。
- 菜单通过运行时扩展 Emby Web 的 `shortcuts.js` 立即注册，不修改 Emby Web 的磁盘文件；从其他页面首次进入剧集或单集详情时也会直接应用精简后的菜单。

## 构建

DLL 不在本地环境构建。推送到 `main` 分支后，GitHub Actions 会在 Windows runner 中：

1. 下载并校验官方 Emby Server 4.9.5.0 Windows x64 程序包中的程序集；
2. 构建插件并运行测试；
3. 将 `CoverFixer.dll` 写入项目根目录；
4. 使用 `github-actions[bot]` 自动提交并推送 DLL。

Emby 4.9.5.0 的固定构建依赖来自[官方发布页](https://github.com/MediaBrowser/Emby.Releases/releases/tag/4.9.5.0)。也可以在 GitHub Actions 页面手动运行 `Build CoverFixer DLL`。

仓库设置需要允许 GitHub Actions 使用 `Contents: write` 权限；workflow 已声明该权限。

构建成功后，仓库根目录中的 `CoverFixer.dll` 就是安装包：

```text
./CoverFixer.dll
```

## 安装

1. 停止 Emby Server。
2. 删除插件目录中的旧版 `Emby.CoverBackfill.dll`，不要让新旧程序集同时存在。
3. 将项目根目录的 `CoverFixer.dll` 复制到 Emby 的 `programdata/plugins/` 目录。
4. 启动 Emby Server。
5. 打开 Emby 控制台 → 计划任务 → 媒体库 → 补全缺失封面。
6. 在 CoverFixer 配置页填写 TMDB API Read Access Token，以启用准确的原始语言优先策略。
7. 如需强制刷新单个剧集，在其详情页打开右上角“三点”菜单，点击“刷新 TMDB 封面”。
8. 首次先手动运行并检查日志与结果，再保留或调整默认计划。

升级 Emby 后应同步修改 `.github/workflows/build.yml` 中的 `EMBY_VERSION`、下载地址和 SHA256，再运行 GitHub Actions 构建并验证插件。卸载时停止 Emby，删除该 DLL，再启动 Emby。
