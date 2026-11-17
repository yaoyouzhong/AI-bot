# Release 说明模板 / Release notes template

每次产品发布在 Releases 里只有一条 `AI-bot vX.Y.Z`。组件实际版本写在正文，标题省略内部候选代号。图库单独使用 `AI-bot Gallery YYYY.N`。内部交接保留 Draft 状态。

产品版本从 `0.6.20` 序列继续递增。正文仅列出本次实际交付的组件和各自版本。只发固件或只发桥接也使用同一套产品版本。自动晋级会基于已验收候选创建或复用产品总页。历史组件发布页和附件继续保留。

正文先列下载入口，再按实际发布的组件说明更新，英文说明集中放在最后。不同版本只写本次实际交付的内容；历史版本整理保留原有功能、下载链接、来源与验收边界。

给普通用户分享产品总页。一页列齐本次下载，在链接上直接写「Windows 安装器」「Mac 应用」「TAB5 首次安装」或「TAB5 升级」，让用户按手里的设备选择。

<a id="writing-the-change-list"></a>

### 更新明细写法 / Writing the change list

- 对照各平台或组件上一实际公开版本和本次发布内容，逐项核对新增功能、可感知修复和入口变化。
- 按用户关心的主题分组，每条先写看得见的变化或解决的问题。涉及操作时，使用界面上的实际名称，给出最短路径。只在有差异时标明适用平台、设备或版本。
- 优先用短句和常用词，合并重复内容。技术实现、完整操作步骤和验收记录放在相应文档，通过链接补充。中英文覆盖同一组变化和必要说明。

Compare each platform or component with its last public version. Use short, plain language about what users will notice. Give the exact menu path when an action has moved, and identify the platform when behavior differs. Group related changes and link to guides or engineering details as needed. Cover the same changes in both languages.

```markdown
## 下载

从本页 Assets 或[下载中心](下载中心链接)获取本次安装包。[首次安装图文](安装指南链接)

## 桌面桥接 X.Y.Z

- 本次实际更新。

## TAB5 X.Y.Z-ui

- 本次实际更新。

## 图库 YYYY.N

- 本次实际更新及下载／导入说明。

## English

English download and release notes for the same published components.
```

示例中的组件段落按发布范围增删；不要为凑齐模板虚构其他组件。下载入口必须对应实际附件，保留必要的校验、兼容与验收说明。自动生成的单组件 Release 采用相同的“下载 → 组件更新 → English”顺序。

Use the same title and section order in future releases. Preserve historical version details and acceptance limits; include only components actually delivered. GitHub Draft status remains in place for internal handoffs.
