# 简体拼音词库来源

- 上游：https://github.com/rime/rime-pinyin-simp
- 固定版本：`0c6861ef7420ee780270ca6d993d18d4101049d0`
- 原文件：`pinyin_simp.dict.yaml`，保留原始内容、`AUTHORS`、Apache-2.0 `LICENSE`。
- SHA-256：`e341598343a0f0f2035bb1aafc34a7f3bb7887deeecb3f60796262aaa2983e6b`
- 上游说明该词库派生自 Android Pinyin IME。

TAB5 修改：`scripts/generate-pinyin-dict.py` 过滤当前 GB2312 字体不能显示的词、超过 8 字或 32 个拼音字母的词，合并重复项，保留词频及音节边界，并按拼音排序生成 `firmware/main/tab5_pinyin_dict.inc`。生成文件随源码保存，日常构建不联网。

词库在 Flash 中只读，查询不复制整个词库到 RAM。当前输入方案支持全拼词组和逐段选字，不包含简拼、模糊拼音、自动学习或联网预测。
