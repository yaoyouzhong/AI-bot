# 书法原文增补与详情标签 / Calligraphy originals and reader tabs

2026-10-11 本轮内容包更新：新增 58 件原文，累计 270/867 件，余 597 件待补；年度书法可读 141/366 件。已纳入 `2026.10.11.1` 年度书画及全量书法 ZIP。此轮仅处理图库内容，没有构建或部署桥接/固件；共享工作区同期的其他部署另记。见[本轮来源、包与验证](CALLIGRAPHY-ORIGINALS-2026-10-11.md)。下方先前数字为历史阶段记录。

Content-only update: 58 additions, 270/867 readable originals, 597 remaining; annual coverage is 141/366. Resource edition `2026.10.11.1` includes annual packs and the full calligraphy pack. This round did not build or deploy a program. Earlier counts below are historical.

## 2026-10-11续轮结果 / Current continuation

全量867件书法目前有212件可读原文：134件核实作品正文，78件明确标注的参考全文。相对首轮87件新增125件，年度精选366件中国书法中99件可读。仍有655件缺口（637件未取得可靠完整正文、18件候选待复核），另有267件年度入选作品仍待补原文；馆录与首帧检查不等于完整多帧逐字核录，缺口不表示原文不存在。

新增125件分别来自ColBase早段16、后段18、MIA40、Met协调8、Met A10、Met B23及其他馆藏10。各组保留实际搜索、成功底本、访问失败、篇章与難字记录。最新证据是`artifacts/development/calligraphy-originals-20261010/verification-current.json`，剩余清单为`research-queue-current.json`；各组`continuation/*/audit.json`提供具体核查范围和缺口。

新增完整《多宝塔碑》7390 UTF-8字节、《文赋并序》5944字节等长文。参考原文与本幅实写异文分开说明，未截断正文。字体8540字形覆盖本轮全部可读文字；后续新包仍须检查设备已有字库与字段范围，不能外推为任意生僻字均已支持。

隔离Windows Release构建0警告/0错误；自检逐字对照全量审定文本与边界。实际生产导入器验证两类年度包全部732件作品的横竖屏导读与取图，2642张包内JPEG通过检查，全库9744张JPEG重新检查SHA-256。11类打包错误、坏包/取消保留旧版、相同长度/时间文字重读与744次认证传输检查通过；传输为真实本机HTTP与USB/BLE邮箱模拟。

原生Debug预览覆盖全部212件原文的字形、最长正文两种方向滚动到底、详情标签底色、返回及空原文页提示。已人工查看当天秦观诗与最长正文起末。主机编译有一项既有`strdup`弃用警告。缓存工作器149次解码、72次离线重开零RPC通过；新增纯文字内容版本刷新、活動传输取消后延后清理、受影响类别失效、另一类别保留及旧位图保留测试通过。

`--status-once`仍被现有数据目录保护阻止，退出码2，没有绕过。标准.167固件、已安装桥接DLL与全量图库目录哈希保持原值；未构建设备固件、部署桥接、刷机或发布，不能称为新增真机验收。TB1634分页顺序问题与Met研究发现4项署名修正建议仍另列待处理，本轮不改原图库图片与目录。

The continuation adds 125 records, reaching 212 of 867 readable originals (134 object bodies and 78 literary references), including 99 of 366 annual selections. The other 655 remain verification gaps. Actual imports of both annual ZIPs, exact text/image readback, 11 producer rejection cases, atomic/error/cancellation tests, 744 authenticated loopback/simulated requests, all 9744 image hashes and native font/reader/category-cache checks pass. Complete long texts remain intact. Desktop startup is still blocked by the existing profile guard; installed bridge and .167 firmware hashes are unchanged. No firmware build, deployment, installation or new hardware acceptance occurred. Current evidence is in `verification-current.json`; the following initial-round counts and checks are historical.

## 首轮87件记录（历史） / Historical initial round

本次为本地数据、桥接源码和相邻TAB5读页源码的修改。设备仍运行已验收的.167，原自动轮播保持恢复后的状态。没有构建新设备固件、部署桥接或重新安装；本次桌面原生预览、隔离桥接编译和模拟传输不能当作设备验收。

## 覆盖与核对范围 / Coverage

全量867件书法中，可读原文从原14件增至首批35件、第二批51件，再经3个并行研究Agent新增36件，合计87件；目前39件为`source_verified`作品正文，48件为`reference_verified`文学参考文本。年度精选366件中国书法中58件可读。仍有780件缺口，其中760件未取得可靠完整正文，20件候选文字有局部、款印或读字问题。这是本轮核实的阶段结果，不是可补数量上限，也没有逐件判定780件都无法查到。

- 正文以馆方明确录文为依据；需要纠错时对照实际作品，并保存图片SHA-256。梁启超对联上联图片为“永夜閒階臥桂影”，纠正馆藏文字的“鬧皆”误读。信封、后人题签、署名、印章与正文分开。
- 已识别诗文可以补文学全文，不限于馆藏释文，但明确显示“通行原文”，并在说明注明节录、版本和异文。未知篇名不自动按作者或模糊题名套入古文。
- 当天倪元璐所书《次韵子由题斗野亭》为秦观五言律诗，已补完整八句及来源，保留“古埭／上埭”传本差异说明。
- 长篇包括完整《千字文》、前后《赤壁赋》、《醉翁亭记》、《桃花源记》、卢仝茶歌及《前后出师表》，另补《蜀素帖》六题八首。没有裁剪开头、结尾来迁就字段上限。
- 新增参考资料优先使用故宫、政府公开页面、高校和大学出版社的资料，以及有明确底本的识典古籍；只录古代正文，现代介绍只作为核对证据。

Reviewed bodies and literary references are separate. Reference texts identify the actual composition, retain source/edition notes and do not purport to transcribe every character of an excerpted object. Three parallel research agents added 36 reviewed records, bringing coverage to 87 of 867 works, including 58 of 366 annual selections. The other 780 remain research gaps, not works proven impossible to complete.

| 并行范围 | 新增件数 | 作品正文 | 参考原文 |
| --- | ---: | ---: | ---: |
| 国立故宫、北京故宫及早期ColBase碑帖 | 10 | 4 | 6 |
| ColBase后段年度精选 | 17 | 6 | 11 |
| 其他馆藏与文学出处 | 9 | 2 | 7 |

Agent只写各自研究证据，协调者核对、合入共享数据。保存实际原图哈希、篇章识别、底本、异文及排除理由；不把同题异作或未确定的草书硬套通行篇章。《临河叙》明确采用《世说新语》注引短篇主体，《洛神赋》十三行只对应残段，诗翰与心经合卷暂只补已确定的心经。

同时校正详情题名《临皋闲题》（散文）与《乐游园歌》，并将三件已确定篇目的英文题名转为中文。只更新导读题名，既有图库与图像不变。额外发现TB1634六屏的第4、5屏颠倒，正确文学顺序为`00,01,02,04,03,05`；已列入待处理事项，本轮未修改图片或图库清单。

## 显示与传输 / Reader and protocol

简介、欣赏提示和原文标签均有不透明圆角底色、描边与按下反馈；选中项更深，文字更亮。书法为暖纸色，名画使用原深色主题。横竖屏及反向显示保留实际点击、阅读滚动和准确返回原页行为。

缺少原文时只保留“这幅作品的原文尚未核实。”，已删去“可先阅读简介与欣赏提示。”。

本地读页请求`guideVersion:2`，原文缓冲512→8192个UTF-8字节（含终止字节），其他字段不变。桥接和设备同步支持v2；旧端仍用v1，长文收到短升级提示而不是截断正文。完整清单控制在16 KiB以内，沿用32 KiB RPC回复上限。图片、作品ID绑定、日期、认证及分页契约不变。

Tabs gain visible selected/pressed backgrounds in both existing themes. Missing text keeps one direct sentence. Guide v2 carries full texts up to 8191 UTF-8 bytes, with a compatible v1 notice for long texts; it does not change image or authentication behavior.

来源、异文和节录范围附在“说明”下，不冒称为现代译文。最长正文为《前后出师表》4624 UTF-8字节，完整收尾可滚动阅读。

## 来源与证据 / Sources and evidence

- [秦观诗，搜韵](https://sou-yun.cn/Query.aspx?id=199374&type=poem1)，篇章识别依据[东京国立博物馆藏倪元璐诗轴](https://colbase.nich.go.jp/collection_items/tnm/TB-1261?locale=zh)。
- [《饮酒》其四，搜韵](https://www.sou-yun.cn/Query.aspx?id=1115&type=poem)，[张九龄《感遇》其六](https://sou-yun.cn/Query.aspx?id=22923&type=poem)。
- [卢仝茶歌，识典古籍《诗林广记》](https://www.shidianguji.com/mid-page/7373787488143491099)。
- [《千字文》，沈丘县政府](https://www.shenqiu.gov.cn/detail_19827.html)，[《后赤壁赋》，故宫博物院](https://www.dpm.org.cn/collection/handwriting/234152.html)。
- [古典篇章，江西服装学院](https://zsb.jift.edu.cn/info/1146/2642.htm)，[湖南女子学院公开经典篇目](https://www.hnwu.edu.cn/ewebeditor/uploadfile/20231006195220323.pdf)，[清华大学出版社诗词篇章](https://www.tup.tsinghua.edu.cn/upload/books/yz/103454-01.pdf)。

完整工作身份、核对范围、来源与日期保存在`docs/art-guides/reviewed-transcriptions.zh.json`和生成的`catalog.zh.json`，逐件缺漏在`coverage.json`。留存快照位于`artifacts/development/art-guides-full/sources/`；各来源的实际采集方法、旧快照日期和SHA-256均保留，失败网页不作为成功获取证据。

本轮研究、编译、预览和检查日志位于`artifacts/development/calligraphy-originals-20261010/`。具体检查结果与未执行的运行项在`verification.json`记录；`research-queue.json`按年度精选优先列出剩余作品，区分待搜索与有候选待辨认，不代表全部已经逐件研究。

## 本地验证 / Local validation

- 隔离Windows Release构建通过，0警告、0错误；未覆盖已安装桥接。审定文本自检验证全部2632件身份和字段边界、87件原文、原14件保留、两篇出师表与蜀素帖末尾，以及v1/v2兼容。
- 三通道模拟检查通过738次认证请求；完整导读清单均不超过16 KiB。既有全库9744张JPEG帧检查沿用相同图库哈希的通过记录。
- 原生Debug预览覆盖全部87件原文的字形（字体8191字形）、最长全文横竖屏滚动到底、四方向实际标签点击、选中和按下底色、返回原作品及空原文页的一句提示。已人工查看当天秦观诗、长文起末、书法暖色与名画深色截图。
- 缓存工作器通过8191字节完整接收、8192字节拒绝不截断、三通道低内存/失败/优先级检查，以及72次离线重开、零取图RPC。
- `--status-once`返回2，现有启动环境的数据目录保护阻止启动；未绕过保护、未写入日常配对和额度目录。上述检查不构成真实桥接运行或新增内容的真机验收。
- 两仓库`git diff --check`通过。标准.167固件、已安装桥接DLL、图库清单的SHA-256与开始前一致。没有新设备固件构建、刷机、安装、提交或发布。

The isolated bridge build, reviewed-text bounds, simulated transports and native reader/cache checks pass. `--status-once` remains blocked by the existing desktop profile guard; no bypass or deployment occurred. Firmware, installed bridge and gallery catalogue hashes are unchanged. These local results do not constitute new hardware acceptance.
