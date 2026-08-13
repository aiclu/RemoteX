# ADR-0003: UI 数据加载异步化

服务器数据加载链路（`GlobalData.ReloadAll` → `GetServers` → `BuildView` → `CalcServerVisibleAndRefresh`）原本在 UI 线程同步全量执行，服务器数量大时启动/轮询刷新会卡住主线程。我们决定改为：后台 `Task` 完成数据源读取与轻量模型构建，带**版本号校验**丢弃过期结果，UI 线程只做一次**批量提交**；**不做分页/按需加载**。

原因：1000+ 台量级下 DB 查询本身是毫秒级，瓶颈在"UI 线程同步执行"而非数据量；后台全量读 + 批量提交即可达标，而分页需改动 SQLite/MySQL/PG 三套 DAO 的查询接口，成本高收益低。版本号校验解决轮询（5s `CHECK_UPDATE_PERIOD`）与手动 force 并发时旧结果覆盖新结果的问题。

**Status**: accepted

**Considered Options**:
- **保持现状（同步全量）**：零改动，但大数据量卡主线程，是性能阶段首要瓶颈。
- **后台 Task 全量 + 版本号 + UI 批量提交（选定）**：改动局部可控，保留现有轮询触发模型，数据源 DAO 层零改动。
- **分页/按需加载**：滚动到底才加载，内存最省，但需重构三套 DAO 查询接口与视图层，复杂度高、收益低。

**Consequences**:
- `ReloadAll` / `BuildView` 的并发语义改变，需回归清单兜底（编辑/搜索/连接/切视图）。
- 涉及有并发风险的唯一改动，必须配合 ADR-0004 的 DEV-only 插桩基线做量化验收。
- 数据源 schema、ULID、JSON 结构零改动，Store 上架版本不受影响，可随时回退。
