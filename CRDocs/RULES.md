# CRDocs 回写与交接规则（兼容索引）

完整规则以 [`README.md`](README.md) 为准；本文件保留为固定入口，供执行者和审核者快速确认回写契约。

## 必须回写的文件

| 文件 | 写入内容 |
| --- | --- |
| [`STATUS.md`](STATUS.md) | 当前任务、状态、日期、范围、结论、边界、下一步和最近 3 轮索引 |
| [`REVIEW.md`](REVIEW.md) | 当前任务最近 3 轮独立审核/整改结论、分级问题和证据 |
| [`IMPLEMENTATION.md`](IMPLEMENTATION.md) | 当前任务最近 3 轮实际改动、验证命令和未验证边界 |
| [`DEFERRED.md`](DEFERRED.md) | 仍有效的延期项和验收条件 |
| [`archive/INDEX.md`](archive/INDEX.md) | 所有历史审核归档目录的入口 |

## 三轮保留与归档

活动区每个任务最多保留最近 3 轮；第 4 轮写入前，先把最旧轮次完整归档到 `archive/YYYY-MM-DD_<审核内容概要>/`，并在该目录和根索引中登记。归档文件不得删除，历史问题 ID、结论、证据和边界不得因压缩而改变。

## 状态词

- 送审：`WAITING_FOR_GROK`
- 审核打回：`REJECTED`
- 独立审核通过：`APPROVED`
- 问题结论：`OPEN`、`RESOLVED`、`DEFERRED`、`WONT_FIX`

执行者不得把自己的结果写成 `APPROVED`；审核者必须独立检查代码和证据后再写入。所有 Linux/ELF、真实 Electron、真实 LAN、DPI/IME 或升级回退的未实测内容必须保持未验证或延期声明。
