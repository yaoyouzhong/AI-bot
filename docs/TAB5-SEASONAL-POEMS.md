# TAB5 节气诗选 / Seasonal poems

本版选择 24 首完整的五言、七言短诗，一节气一首。尽量避开高频课本名篇，保留题名、作者和原文字词；不会把长诗截成四句冒充全篇。选配依据为节令物候与生活情趣，并非每首都是作者专咏该节气。花卉仍按公历月份更换，不将插画当作当地花期预测。

诗文原始数据在独立 TAB5 工程的 `firmware/assets/calendar-poems.json`，字体脚本同时生成 `calendar_poems.h`，预览和固件读取同一份内容。现代译注和赏析不进入固件。

## 切换与布局

- 正式屏保使用电脑桥接已有的年度节气日期表，在设备当地日期跨到下一节气时切换；同一节气期间持续展示对应诗篇。现有数据精度为日期，不声称按天文交节时刻切换。
- 当年小寒之前显示冬至诗。当年的完整日期表缺失或非法时，回退该月原短句，不猜节气日期。
- 预览点击当月显示今天的节气，浏览其他月份以当月 15 日为参考；下方 1–12 月按钮仅预览显示，正式屏保触摸退出逻辑保持。
- 左侧为 48 px 楷意月份、24 px 四列诗文、18 px 题名与作者、朱色节气印章。诗文按节气采用霞鹜文楷、马善政毛笔字、志莽行书三种字形，每首内部统一，含生僻字的篇目完整使用可覆盖原文的字体。花卉仍在背景绘制层，恢复到 90% 不透明度，并下移到诗文下方留出空隙；保留约 5% 缩小后的尺寸。右侧日历保持。
- 三种字体均为固定来源与 SHA-256 的 OFL 子集，完整许可随 TAB5 源码保存。日历字体与已有 72/96/144 px 数字字库使用 LVGL 内置无损位图压缩；数字外形、灰度和尺寸保持，不扩大分区，不改其他通用中文字库。

## 全部篇目与核对出处

以下仅录古代作品原文，竖排显示时省略标点。链接用于核对题名、作者与文本；作品的节气归类是本项目的编辑选择。

### 小寒 · 探梅

宋 · 杨万里 ｜ 显示字体：马善政

山间幽步不胜奇，政是深寒浅暮时。\
一树梅花开一朵，恼人偏在最高枝。

[文本出处](https://m.cidianwang.com/gushiwen/7/885c3231737.htm)。保留底本的政是。

### 大寒 · 雪梅·其二

宋 · 卢梅坡 ｜ 显示字体：霞鹜文楷

有梅无雪不精神，有雪无诗俗了人。\
日暮诗成天又雪，与梅并作十分春。

[文本出处](https://www.gushiwenku.cn/shiwen/e2fb4f8/)

### 立春 · 立春

宋 · 王镃 ｜ 显示字体：马善政

泥牛鞭散六街尘，生菜挑来叶叶春。\
从此雪消风自软，梅花合让柳条新。

[文本出处](https://m.gushiwen.cn/shiwenv_f411e100243c.aspx)

### 雨水 · 春雨

宋 · 周邦彦 ｜ 显示字体：志莽行书

耕人扶耒语林丘，花外时时落一鸥。\
欲验春来多少雨，野塘漫水可回舟。

[文本出处](https://www.gushiwen.cn/shiwenv_4d7fcda2c304.aspx)

### 惊蛰 · 春日

宋 · 秦观 ｜ 显示字体：霞鹜文楷

一夕轻雷落万丝，霁光浮瓦碧参差。\
有情芍药含春泪，无力蔷薇卧晓枝。

[文本出处](https://zh.wikisource.org/zh-hans/春日_(秦觀))。取轻雷、春雨意象，并非专咏惊蛰；花期随地域而异。

### 春分 · 淮中晚泊犊头

宋 · 苏舜钦 ｜ 显示字体：志莽行书

春阴垂野草青青，时有幽花一树明。\
晚泊孤舟古祠下，满川风雨看潮生。

[文本出处](https://m.gushiwen.cn/shiwenv_91da00bee5c9.aspx)

### 清明 · 苏堤清明即事

宋 · 吴惟信 ｜ 显示字体：马善政

梨花风起正清明，游子寻春半出城。\
日暮笙歌收拾去，万株杨柳属流莺。

[文本出处](https://dict.baidu.com/shici/detail?pid=89229f303c0a4635b230992b4f19445c)

### 谷雨 · 见二十弟倡和花字漫兴五首·其一

宋 · 黄庭坚 ｜ 显示字体：霞鹜文楷

落絮游丝三月候，风吹雨洗一城花。\
未知东郭清明酒，何似西窗谷雨茶。

[文本出处](https://m.gushiwen.cn/shiwenv_ca10eb0f4c7f.aspx)

### 立夏 · 初夏绝句

宋 · 陆游 ｜ 显示字体：志莽行书

纷纷红紫已成尘，布谷声中夏令新。\
夹路桑麻行不尽，始知身是太平人。

[文本出处](https://m.gushiwen.cn/shiwenv_1614e83f3f7e.aspx)

### 小满 · 初夏即事

宋 · 王安石 ｜ 显示字体：霞鹜文楷

石梁茅屋有弯碕，流水溅溅度两陂。\
晴日暖风生麦气，绿阴幽草胜花时。

[文本出处](https://zh.wikisource.org/zh/初夏即事)。保留碕、陂。

### 芒种 · 田上

唐 · 崔道融 ｜ 显示字体：马善政

雨足高田白，披蓑半夜耕。\
人牛力俱尽，东方殊未明。

[文本出处](https://www.gushiju.net/ju/424127)。以雨后抢耕对应农忙，并非专咏芒种。

### 夏至 · 夏夜追凉

宋 · 杨万里 ｜ 显示字体：志莽行书

夜热依然午热同，开门小立月明中。\
竹深树密虫鸣处，时有微凉不是风。

[文本出处](https://m.gushiwen.cn/shiwenv_2d549d9a09f4.aspx)

### 小暑 · 闲居初夏午睡起二绝句·其二

宋 · 杨万里 ｜ 显示字体：霞鹜文楷

松阴一架半弓苔，偶欲看书又懒开。\
戏掬清泉洒蕉叶，儿童误认雨声来。

[文本出处](https://www.pinshiwen.com/gsdq/sgjs/2019051444085.html)。以树阴、清泉的消暑意趣选配，标题仍保留初夏，不改作小暑诗。

### 大暑 · 纳凉

宋 · 秦观 ｜ 显示字体：马善政

携杖来追柳外凉，画桥南畔倚胡床。\
月明船笛参差起，风定池莲自在香。

[文本出处](https://www.gushiwen.cn/mingju/juv_ab1a0b87feee.aspx)

### 立秋 · 立秋

宋 · 刘翰 ｜ 显示字体：霞鹜文楷

乳鸦啼散玉屏空，一枕新凉一扇风。\
睡起秋声无觅处，满阶梧叶月明中。

[文本出处](https://www.gushiwen.cn/shiwenv.aspx?id=7492691aa257)。乳鸦，不作乳鸭。

### 处暑 · 初秋雨晴

宋 · 朱淑真 ｜ 显示字体：志莽行书

雨后风凉暑气收，庭梧叶叶报初秋。\
浮云尽逐黄昏去，楼角新蟾挂玉钩。

[文本出处](https://www.cidianwang.com/gushiwen/7/f12a0261417.htm)

### 白露 · 秋夜寄邱员外

唐 · 韦应物 ｜ 显示字体：霞鹜文楷

怀君属秋夜，散步咏凉天。\
空山松子落，幽人应未眠。

[文本出处](https://www.gushiwen.cn/shiwenv.aspx?id=0f23fdb7b5f9)。采用空山；异本作山空，题名另作秋夜寄丘二十二员外。

### 秋分 · 秋凉晚步

宋 · 杨万里 ｜ 显示字体：马善政

秋气堪悲未必然，轻寒正是可人天。\
绿池落尽红蕖却，荷叶犹开最小钱。

[文本出处](https://www.gushiwen.cn/shiwenv.aspx?id=c32bc0006712)

### 寒露 · 秋夜二首·其一

宋 · 朱淑真 ｜ 显示字体：志莽行书

夜久无眠秋气清，烛花频剪欲三更。\
铺床凉满梧桐月，月在梧桐缺处明。

[文本出处](https://www.pinshiwen.com/gsdq/ssjx/2019050518216.html)

### 霜降 · 立冬前一日霜对菊有感

宋 · 钱时 ｜ 显示字体：霞鹜文楷

昨夜清霜冷絮裯，纷纷红叶满阶头。\
园林尽扫西风去，惟有黄花不负秋。

[文本出处](https://m.gushiwen.cn/shiwenv_5f60b20ef6f5.aspx)。立冬前一日仍处于霜降节气区间。

### 立冬 · 立冬即事二首·其一

宋 · 仇远 ｜ 显示字体：志莽行书

细雨生寒未有霜，庭前木叶半青黄。\
小春此去无多日，何处梅花一绽香。

[文本出处](https://www.gushiwen.cn/shiwenv_1248929727d7.aspx)。作者跨宋元，此处按所引页面标宋。

### 小雪 · 小雪

唐 · 戴叔伦 ｜ 显示字体：马善政

花雪随风不厌看，更多还肯失林峦。\
愁人正在书窗下，一片飞来一片寒。

[文本出处](https://zh.wikisource.org/zh-hans/小雪_(戴叔倫))。采用还肯、书窗；不混入其他异文。

### 大雪 · 对雪

唐 · 高骈 ｜ 显示字体：志莽行书

六出飞花入户时，坐看青竹变琼枝。\
如今好上高楼望，盖尽人间恶路岐。

[文本出处](https://www.wuduge.com/shiwen/a/327.html)。岐另有作歧的版本，采用所引页面文本。

### 冬至 · 冬至日独游吉祥寺

宋 · 苏轼 ｜ 显示字体：霞鹜文楷

井底微阳回未回，萧萧寒雨湿枯荄。\
何人更似苏夫子，不是花时肯独来。

[文本出处](https://zh.wikisource.org/zh-hans/冬至日獨遊吉祥寺)

## English

The cover now presents 24 complete five- or seven-character quatrains, one per solar-term interval, with title and attribution. Selections favor less overused seasonal imagery. The pairings are editorial, not claims about the original occasion of every work. The dated bridge table drives local-date transitions; missing or malformed yearly data falls back to the original monthly phrase. January before Lesser Cold uses the winter-solstice poem. Other-month previews use the 15th; the actual month uses today. The sourced corpus, generated C table and font subsets are reproducible from the TAB5 project. No modern translations or commentary are embedded. The poem body now varies across three pinned OFL faces (LXGW WenKai, Ma Shan Zheng, Zhi Mang Xing), using one face per complete poem. Flower color is restored with 90% opacity; each flower sits below the verse with clear space around the text, retaining the approximately 5% size reduction. Existing large numeral bitmaps are losslessly compressed to recover application space without changing their appearance. Native previews and firmware build evidence are recorded separately from device acceptance.
