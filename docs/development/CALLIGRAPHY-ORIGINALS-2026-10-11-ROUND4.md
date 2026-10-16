# 书法原文第四轮与权威欣赏提示 · 2026-10-11

本轮新增13件审定原文，累计359/867件：220件作品正文核录、139件明确标注的文学参考全文；余508件待核。年度书法可读178/366件。原文没有全部补齐，参考全文也不冒称原作逐字释文。

欣赏提示执行用户要求：必须有对应作品的权威出处，否则留空。旧全库2612条关键词模板及20件无专门赏析出处的样本文字不再生成；新全库只保留7件已逐件核定的赏析，其余2625件为空。见[提示来源审计](ART-LOOKING-AUDIT-2026-10-11.md)与[逐件赏析证据](../art-guides/reviewed-looking.zh.json)。

## 新增原文

| 稳定作品编号 | 作品 | 核实范围 |
| --- | --- | --- |
| colbase-tnm-TB-1179 | 登承天万佛阁偈 | 参考全文 |
| colbase-tnm-TB-1440 | 无梦偈 | 作品正文 |
| colbase-tnm-TB-1414 | 行草书感怀语轴 | 作品正文 |
| colbase-tnm-TB-1724 | 行书七言律诗轴 | 作品正文 |
| colbase-tnm-TB-1725 | 草书五言律诗瑞芝咏轴 | 作品正文 |
| colbase-tnm-TB-1729 | 草书五言律诗轴 | 参考全文 |
| colbase-tnm-TB-1730 | 行草书画详轴 | 作品正文 |
| colbase-tnm-TB-1731 | 行书坳堂老友司臬贵州奉送小序轴 | 作品正文 |
| colbase-tnm-TB-243 | 杨淮表记（拓本） | 参考全文 |
| colbase-tnm-TB-233 | 石门颂（拓本） | 参考全文 |
| mia-117230 | Cherry-Blossom at Horinji | 作品正文 |
| mia-118917 | River Winds | 作品正文 |
| colbase-tnm-TB-1174 | 致圣一国师尺牍（赠板之墨迹） | 作品正文 |

赠板尺牍以馆方原图、附训点临写本及学术文献互校，保留“師范”“千板”和“乙百四十片”等原字；作者公式另段，后世题跋不混入正文。“乙通一”等说明保留在读者说明。石门颂、杨淮表记的残泐和文献补字没有当作本幅现存字迹。TB-1173仍有读字与重复符问题，暂不纳入。

来源、图像、逐字核对范围和保留意见均随[审定原文](../art-guides/reviewed-transcriptions.zh.json)记录。既有审定记录未覆盖改写，来源和图片SHA-256均核验；作品身份、图片字节及年度入选顺序不变。

## 本地交付与验证

资源版`2026.10.11.3`：

| 包 | 独立作品 | 横竖屏图片 | 可读书法原文 |
| --- | ---: | ---: | ---: |
| 年度名画 | 366 | 860 | — |
| 年度书法 | 366 | 1782 | 178 |
| 全量书法 | 867 | 5708 | 359 |

[年度包及校验值](../../artifacts/development/calligraphy-originals-20261010/round-20261011-4/curated/packs/packs.json)；[全量书法包及校验值](../../artifacts/development/calligraphy-originals-20261010/round-20261011-4/full-calligraphy/packs.json)；[完整验证记录](../../artifacts/development/calligraphy-originals-20261010/round-20261011-4/verification.json)。包内附许可、逐件来源和内部文件哈希，包外附SHA-256；工作区快照路径不进入包。

空提示通过打包和桥接源码的导入校验；非空提示由打包器要求专门的权威赏析证据。Release在本轮隔离目录构建，0警告、0错误，原文自检通过；生产导入器在隔离数据目录检查年度及全量包，逐件读回横竖屏文字与图片。40张本地预览通过静态排版检查并检查了接触表。

普通`--status-once`被现有`AppProfileGuard`阻止，原因是桌面执行环境重定向用户数据路径；保护没有绕过。浏览器本地文件预览被协议策略拒绝，也没有绕过，不能宣称浏览器交互验收。新增正文与现有未构建字库源码比较发现58个未覆盖字符，不能据此宣称安装固件的字库完整。

本任务未替换运行桥接、设备固件或用户图库，未提交、推送或发布。旧运行导入器仍要求非空提示，新包先留本地，待兼容程序部署后才能导入。当前固件的空“欣赏提示”标签尚未隐藏，未进行设备验收。共享工作区同期其他任务的变更另以验证记录的`externalChanges`登记。

Fourth round adds 13 reviewed originals, bringing coverage to 359/867 (220 object transcriptions and 139 labelled literary references); 508 remain, and annual coverage is 178/366. Edition `2026.10.11.3` includes annual painting/calligraphy and full calligraphy packs with unchanged artwork identity, images and annual order. Viewing notes now require authoritative analysis of the exact work: seven remain, with 2,625 omitted. Isolated Release build, exact-text checks and production imports passed; live software, firmware and galleries were not replaced. Normal status execution and browser preview were blocked by retained safeguards. The existing unbuilt font source lacks 58 characters from the additions, and hardware acceptance remains outstanding.
