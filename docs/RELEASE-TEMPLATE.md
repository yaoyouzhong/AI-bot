# Release 说明模板 / Release notes template

正式发布标题只写产品或组件名和版本：产品总页 `AI-bot v产品版本号`、TAB5 组件页 `TAB5 固件版本号`、ESP8266 组件页 `ESP8266 固件版本号`、图库 `AI-bot Gallery YYYY.N`。标签沿用各组件既有规则；标题不追加组件说明或内部候选代号。内部交接保留 Draft 状态，交接及验收细节放在正文，不冒充正式发布。

面向用户的每次产品发布使用一个独立的 `AI-bot vX.Y.Z` 对外版本号，从当前 `0.6.20` 序列继续递增，例如下一次为 `0.6.21`。正文仅列出本次实际交付的组件，组件自身版本独立记录，不把组件版本直接当作产品版本。仅发布固件或仅发布桥接时，同样使用该次产品发布号。自动晋级会基于已验收候选创建或复用产品总页和对应组件页；总页只包含本次交付的安装／升级包，组件版本按各自实际包记录。历史组件发布页继续保留各自组件版本和附件。

正文先列下载入口，再按实际发布的组件说明更新，英文说明集中放在最后。不同版本只写本次实际交付的内容；历史版本整理保留原有功能、下载链接、来源与验收边界。

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
